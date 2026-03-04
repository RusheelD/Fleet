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

        using var httpResponse = await httpClient.SendAsync(httpRequest, cancellationToken);
        var responseBody = await httpResponse.Content.ReadAsStringAsync(cancellationToken);

        if (!httpResponse.IsSuccessStatusCode)
        {
            logger.LlmClaudeApiError((int)httpResponse.StatusCode, responseBody.SanitizeForLogging());
            throw new InvalidOperationException($"Claude API returned {httpResponse.StatusCode}: {responseBody}");
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
            ["max_tokens"] = MaxTokens,
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
        body["messages"] = BuildMessages(request.Messages);

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
            body["tools"] = tools;
        }

        return body;
    }

    /// <summary>
    /// Converts normalized messages to Claude's message format.
    /// Claude requires strictly alternating user/assistant roles.
    /// Tool results are sent as user messages with tool_result content blocks.
    /// </summary>
    private static JsonArray BuildMessages(IReadOnlyList<LLMMessage> messages)
    {
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

        return result;
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

        return new LLMResponse(textContent, toolCalls);
    }
}
