using System.Text.Json;
using System.Text.Json.Nodes;
using Fleet.Server.Logging;
using Microsoft.Extensions.Options;

namespace Fleet.Server.LLM;

/// <summary>
/// LLM client for Anthropic Claude via the Messages API.
/// Translates the normalized LLM models to/from Claude's wire format.
/// </summary>
public class ClaudeClient(
    IHttpClientFactory httpClientFactory,
    IOptions<LLMOptions> options,
    ILogger<ClaudeClient> logger) : ILLMClient
{
    private const string ApiBaseUrl = "https://api.anthropic.com/v1/messages";
    private const string AnthropicVersion = "2023-06-01";
    private const int MaxTokens = 16384;

    public async Task<LLMResponse> CompleteAsync(LLMRequest request, CancellationToken cancellationToken = default)
    {
        var config = options.Value;

        if (string.IsNullOrWhiteSpace(config.ApiKey))
        {
            throw new InvalidOperationException(
                "Claude API key is not configured. Set LLM:ApiKey or environment variable ANTHROPIC_API_KEY.");
        }

        // Use model override from request if provided, otherwise fall back to config
        var model = request.ModelOverride ?? config.Model;

        var body = BuildRequestBody(request, model);
        logger.LlmClaudeRequest(body.ToJsonString().SanitizeForLogging());

        using var httpClient = httpClientFactory.CreateClient("LLM");
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, ApiBaseUrl)
        {
            Content = new StringContent(body.ToJsonString(), System.Text.Encoding.UTF8, "application/json"),
        };
        httpRequest.Headers.Add("x-api-key", config.ApiKey);
        httpRequest.Headers.Add("anthropic-version", AnthropicVersion);
        // Enable prompt caching — caches system prompt, tool schemas, and marked user content
        httpRequest.Headers.Add("anthropic-beta", "prompt-caching-2024-07-31");

        var (statusCode, responseBody) = await IdleTimeoutHandler.SendWithIdleTimeoutAsync(
            httpClient, httpRequest, cancellationToken);

        if ((int)statusCode < 200 || (int)statusCode >= 300)
        {
            logger.LlmClaudeApiError((int)statusCode, responseBody.SanitizeForLogging());
            throw new InvalidOperationException($"Claude API returned {statusCode}: {responseBody}");
        }

        logger.LlmClaudeResponse(responseBody.SanitizeForLogging());
        return ParseResponse(responseBody);
    }

    // ── Build Claude request body ──────────────────────────────

    private static JsonObject BuildRequestBody(LLMRequest request, string model)
    {
        var body = new JsonObject
        {
            ["model"] = model,
            ["max_tokens"] = request.MaxTokens ?? MaxTokens,
        };

        // System prompt — use structured format with cache_control for prompt caching
        if (!string.IsNullOrWhiteSpace(request.SystemPrompt))
        {
            body["system"] = new JsonArray
            {
                new JsonObject
                {
                    ["type"] = "text",
                    ["text"] = request.SystemPrompt,
                    ["cache_control"] = new JsonObject { ["type"] = "ephemeral" },
                },
            };
        }

        // Messages
        body["messages"] = BuildMessages(request.Messages, request.CacheFirstUserMessage);

        // Tools
        if (request.Tools is { Count: > 0 })
        {
            var tools = new JsonArray();
            foreach (var tool in request.Tools)
            {
                var toolObj = new JsonObject
                {
                    ["name"] = tool.Name,
                    ["description"] = tool.Description,
                };

                if (!string.IsNullOrWhiteSpace(tool.ParametersJsonSchema))
                {
                    var schema = JsonNode.Parse(tool.ParametersJsonSchema);
                    toolObj["input_schema"] = CleanSchema(schema);
                }
                else
                {
                    toolObj["input_schema"] = new JsonObject { ["type"] = "object", ["properties"] = new JsonObject() };
                }

                tools.Add(toolObj);
            }

            // Mark the last tool definition with cache_control so the entire tool
            // schema block is cached on subsequent calls within the TTL.
            if (tools.Count > 0 && tools[^1] is JsonObject lastTool)
            {
                lastTool["cache_control"] = new JsonObject { ["type"] = "ephemeral" };
            }

            body["tools"] = tools;
        }

        return body;
    }

    /// <summary>
    /// Converts normalized messages to Claude's message format.
    /// Claude requires strictly alternating user/assistant roles.
    /// Tool results are sent as user messages with tool_result content blocks.
    /// After building the message list, the 2 largest tool_result blocks are marked
    /// with cache_control to use the remaining 2 of 4 allowed cache breakpoints
    /// (the first 2 are system prompt and tools).
    /// </summary>
    private static JsonArray BuildMessages(IReadOnlyList<LLMMessage> messages, bool cacheFirstUser = false)
    {
        // cacheFirstUser kept for API compat but no longer used as a breakpoint;
        // the 2 biggest tool results are more valuable since they grow over the conversation.
        _ = cacheFirstUser;

        var result = new JsonArray();
        JsonObject? current = null;
        string? currentRole = null;

        foreach (var msg in messages)
        {
            var (role, contentBlocks) = ConvertMessage(msg);

            // Skip messages that produced no content blocks (e.g. empty user text)
            if (contentBlocks.Count == 0) continue;

            // Claude requires alternating roles — merge consecutive same-role messages
            if (role == currentRole && current is not null)
            {
                var existingContent = current["content"]!.AsArray();
                foreach (var block in contentBlocks)
                {
                    existingContent.Add(block);
                }
            }
            else
            {
                current = new JsonObject
                {
                    ["role"] = role,
                    ["content"] = new JsonArray(contentBlocks.ToArray()),
                };
                currentRole = role;
                result.Add(current);
            }
        }

        // Mark the 2 largest tool_result blocks with cache_control.
        // This uses the remaining 2 cache breakpoints (system prompt = 1, tools = 2).
        MarkLargestToolResults(result, maxBreakpoints: 2);

        return result;
    }

    /// <summary>
    /// Finds all tool_result content blocks across the message array,
    /// sorts them by content length descending, and marks the top N
    /// with cache_control: ephemeral.
    /// </summary>
    private static void MarkLargestToolResults(JsonArray messages, int maxBreakpoints)
    {
        var candidates = new List<(JsonObject Block, int Length)>();

        foreach (var msgNode in messages)
        {
            if (msgNode is not JsonObject msg) continue;
            if (msg["content"] is not JsonArray content) continue;

            foreach (var blockNode in content)
            {
                if (blockNode is not JsonObject block) continue;
                if (block["type"]?.GetValue<string>() != "tool_result") continue;

                var contentText = block["content"]?.ToString() ?? "";
                candidates.Add((block, contentText.Length));
            }
        }

        // Sort descending by content length and mark the top N
        foreach (var (block, _) in candidates.OrderByDescending(c => c.Length).Take(maxBreakpoints))
        {
            block["cache_control"] = new JsonObject { ["type"] = "ephemeral" };
        }
    }

    private static (string role, List<JsonNode>) ConvertMessage(LLMMessage msg)
    {
        var blocks = new List<JsonNode>();

        switch (msg.Role)
        {
            case "user":
                if (!string.IsNullOrEmpty(msg.Content))
                {
                    blocks.Add(new JsonObject
                    {
                        ["type"] = "text",
                        ["text"] = msg.Content,
                    });
                }
                return ("user", blocks);

            case "assistant":
                if (!string.IsNullOrEmpty(msg.Content))
                {
                    blocks.Add(new JsonObject
                    {
                        ["type"] = "text",
                        ["text"] = msg.Content,
                    });
                }
                if (msg.ToolCalls is { Count: > 0 })
                {
                    foreach (var tc in msg.ToolCalls)
                    {
                        var input = string.IsNullOrWhiteSpace(tc.ArgumentsJson)
                            ? new JsonObject()
                            : JsonNode.Parse(tc.ArgumentsJson)?.AsObject() ?? new JsonObject();
                        blocks.Add(new JsonObject
                        {
                            ["type"] = "tool_use",
                            ["id"] = tc.Id,
                            ["name"] = tc.Name,
                            ["input"] = input,
                        });
                    }
                }
                return ("assistant", blocks);

            case "tool":
                blocks.Add(new JsonObject
                {
                    ["type"] = "tool_result",
                    ["tool_use_id"] = msg.ToolCallId ?? "",
                    ["content"] = msg.Content ?? "",
                });
                return ("user", blocks);

            default:
                if (!string.IsNullOrEmpty(msg.Content))
                {
                    blocks.Add(new JsonObject
                    {
                        ["type"] = "text",
                        ["text"] = msg.Content,
                    });
                }
                return ("user", blocks);
        }
    }

    /// <summary>
    /// Cleans a JSON Schema for Claude's tool input_schema.
    /// Claude accepts standard JSON Schema, so we just deep-clone and strip
    /// unsupported keywords like additionalProperties at the root level.
    /// </summary>
    private static JsonNode? CleanSchema(JsonNode? node)
    {
        if (node is not JsonObject obj) return node?.DeepClone();

        var result = new JsonObject();

        foreach (var kvp in obj.ToList())
        {
            switch (kvp.Key)
            {
                // Standard JSON Schema keywords Claude supports
                case "type":
                case "properties":
                case "required":
                case "description":
                case "enum":
                case "items":
                case "format":
                case "minimum":
                case "maximum":
                case "minItems":
                case "maxItems":
                case "nullable":
                case "anyOf":
                case "oneOf":
                case "allOf":
                    if (kvp.Key == "properties" && kvp.Value is JsonObject props)
                    {
                        var cleaned = new JsonObject();
                        foreach (var prop in props.ToList())
                        {
                            cleaned[prop.Key] = CleanSchema(prop.Value);
                        }
                        result["properties"] = cleaned;
                    }
                    else if (kvp.Key == "items")
                    {
                        result["items"] = CleanSchema(kvp.Value);
                    }
                    else
                    {
                        result[kvp.Key] = kvp.Value?.DeepClone();
                    }
                    break;

                    // Skip unsupported keywords ($ref, additionalProperties, etc.)
            }
        }

        return result;
    }

    // ── Parse Claude response ──────────────────────────────────

    private static LLMResponse ParseResponse(string responseBody)
    {
        var doc = JsonDocument.Parse(responseBody);
        var root = doc.RootElement;

        if (!root.TryGetProperty("content", out var contentArr) || contentArr.GetArrayLength() == 0)
        {
            return new LLMResponse("I wasn't able to generate a response. Please try again.", null);
        }

        string? textContent = null;
        List<LLMToolCall>? toolCalls = null;

        foreach (var block in contentArr.EnumerateArray())
        {
            var type = block.TryGetProperty("type", out var typeProp)
                ? typeProp.GetString()
                : null;

            switch (type)
            {
                case "text":
                    var text = block.GetProperty("text").GetString();
                    textContent = textContent is null ? text : textContent + "\n" + text;
                    break;

                case "tool_use":
                    toolCalls ??= [];
                    var id = block.GetProperty("id").GetString() ?? Guid.NewGuid().ToString();
                    var name = block.GetProperty("name").GetString() ?? "unknown";
                    var input = block.TryGetProperty("input", out var inputProp)
                        ? inputProp.GetRawText()
                        : "{}";
                    toolCalls.Add(new LLMToolCall(Id: id, Name: name, ArgumentsJson: input));
                    break;
            }
        }

        // Claude uses "stop_reason": "end_turn" | "max_tokens" | "stop_sequence" | "tool_use"
        var stopReason = root.TryGetProperty("stop_reason", out var stopProp)
            ? stopProp.GetString()
            : null;

        return new LLMResponse(textContent, toolCalls, ParseUsage(root), stopReason);
    }

    private static LLMUsage? ParseUsage(JsonElement root)
    {
        if (!root.TryGetProperty("usage", out var usageProp) || usageProp.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        int inputTokens = usageProp.TryGetProperty("input_tokens", out var inp) ? inp.GetInt32() : 0;
        int outputTokens = usageProp.TryGetProperty("output_tokens", out var outp) ? outp.GetInt32() : 0;
        int? cachedTokens = usageProp.TryGetProperty("cache_read_input_tokens", out var cached)
            ? cached.GetInt32()
            : usageProp.TryGetProperty("cache_creation_input_tokens", out var cacheCreate)
                ? cacheCreate.GetInt32()
                : null;

        return new LLMUsage(inputTokens, outputTokens, cachedTokens);
    }
}
