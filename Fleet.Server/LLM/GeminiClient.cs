using System.Text.Json;
using System.Text.Json.Nodes;
using Fleet.Server.Logging;
using Microsoft.Extensions.Options;

namespace Fleet.Server.LLM;

/// <summary>
/// LLM client for Google Gemini via the REST API.
/// Translates the normalized LLM models to/from Gemini's wire format.
/// </summary>
public class GeminiClient(
    IHttpClientFactory httpClientFactory,
    IOptions<LLMOptions> options,
    ILogger<GeminiClient> logger) : ILLMClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    public async Task<LLMResponse> CompleteAsync(LLMRequest request, CancellationToken cancellationToken = default)
    {
        var config = options.Value;

        if (string.IsNullOrWhiteSpace(config.ApiKey))
        {
            throw new InvalidOperationException(
                "Gemini API key is not configured. Set LLM:ApiKey or environment variable GEMINI_API_KEY.");
        }

        var url = $"https://generativelanguage.googleapis.com/v1beta/models/{config.Model}:generateContent?key={config.ApiKey}";

        var body = BuildRequestBody(request);
        logger.LlmGeminiRequest(body.ToJsonString().SanitizeForLogging());

        using var httpClient = httpClientFactory.CreateClient("LLM");
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(body.ToJsonString(), System.Text.Encoding.UTF8, "application/json"),
        };

        var (statusCode, responseBody) = await IdleTimeoutHandler.SendWithIdleTimeoutAsync(
            httpClient, httpRequest, cancellationToken);

        if ((int)statusCode < 200 || (int)statusCode >= 300)
        {
            logger.LlmGeminiApiError((int)statusCode, responseBody.SanitizeForLogging());
            throw new InvalidOperationException($"Gemini API returned {statusCode}: {responseBody}");
        }

        logger.LlmGeminiResponse(responseBody.SanitizeForLogging());
        return ParseResponse(responseBody);
    }

    // ── Build Gemini request body ──────────────────────────────

    private static JsonObject BuildRequestBody(LLMRequest request)
    {
        var body = new JsonObject();

        // System instruction
        if (!string.IsNullOrWhiteSpace(request.SystemPrompt))
        {
            body["systemInstruction"] = new JsonObject
            {
                ["parts"] = new JsonArray { new JsonObject { ["text"] = request.SystemPrompt } },
            };
        }

        // Contents — convert messages and merge consecutive same-role blocks.
        // Gemini requires strictly alternating user/model roles, and multiple
        // function responses for one model turn must share a single content block.
        body["contents"] = BuildMergedContents(request.Messages);

        // Tools — convert JSON Schema to Gemini's Schema format (uppercase types)
        if (request.Tools is { Count: > 0 })
        {
            var functionDeclarations = new JsonArray();
            foreach (var tool in request.Tools)
            {
                var declaration = new JsonObject
                {
                    ["name"] = tool.Name,
                    ["description"] = tool.Description,
                };
                if (!string.IsNullOrWhiteSpace(tool.ParametersJsonSchema))
                {
                    var schema = JsonNode.Parse(tool.ParametersJsonSchema);
                    declaration["parameters"] = ConvertToGeminiSchema(schema);
                }
                functionDeclarations.Add(declaration);
            }
            body["tools"] = new JsonArray
            {
                new JsonObject { ["functionDeclarations"] = functionDeclarations },
            };

            // Explicitly enable function calling
            body["toolConfig"] = new JsonObject
            {
                ["functionCallingConfig"] = new JsonObject { ["mode"] = "AUTO" },
            };
        }

        return body;
    }

    /// <summary>
    /// Converts messages to Gemini content blocks and merges consecutive
    /// same-role entries (required because Gemini disallows back-to-back
    /// content blocks with the same role).
    /// </summary>
    private static JsonArray BuildMergedContents(IReadOnlyList<LLMMessage> messages)
    {
        var merged = new JsonArray();
        JsonObject? current = null;
        string? currentRole = null;

        foreach (var msg in messages)
        {
            var geminiMsg = ConvertMessageToGemini(msg);
            var role = geminiMsg["role"]?.GetValue<string>();

            if (role == currentRole && current is not null)
            {
                // Merge parts into the existing content block
                var existingParts = current["parts"]!.AsArray();
                var newParts = geminiMsg["parts"]!.AsArray();
                // Must detach nodes from source array before adding to another
                var partsToMove = newParts.Select(p => p).ToList();
                foreach (var part in partsToMove)
                {
                    newParts.Remove(part);
                    existingParts.Add(part);
                }
            }
            else
            {
                current = geminiMsg;
                currentRole = role;
                merged.Add(geminiMsg);
            }
        }

        return merged;
    }

    /// <summary>
    /// Converts a JSON Schema node to Gemini's Schema format:
    /// - type values → UPPERCASE (STRING, OBJECT, INTEGER, NUMBER, BOOLEAN, ARRAY)
    /// - Recursively processes properties and array items
    /// - Passes through only Gemini-supported keywords
    /// </summary>
    private static JsonNode? ConvertToGeminiSchema(JsonNode? node)
    {
        if (node is not JsonObject obj) return node?.DeepClone();

        var result = new JsonObject();

        // Gemini only allows enum on STRING type, so detect if enum is present
        var hasEnum = obj.ContainsKey("enum");

        foreach (var kvp in obj.ToList())
        {
            switch (kvp.Key)
            {
                case "type":
                    // Force STRING when enum is present — Gemini rejects enum on INTEGER/NUMBER types
                    result["type"] = hasEnum
                        ? "STRING"
                        : kvp.Value?.GetValue<string>()?.ToUpperInvariant() ?? "STRING";
                    break;

                case "properties" when kvp.Value is JsonObject props:
                    var converted = new JsonObject();
                    foreach (var prop in props.ToList())
                    {
                        converted[prop.Key] = ConvertToGeminiSchema(prop.Value);
                    }
                    result["properties"] = converted;
                    break;

                case "items":
                    result["items"] = ConvertToGeminiSchema(kvp.Value);
                    break;

                // Gemini requires enum values to be strings
                case "enum" when kvp.Value is JsonArray enumArr:
                    var stringEnum = new JsonArray();
                    foreach (var item in enumArr)
                    {
                        stringEnum.Add(item?.ToString() ?? "");
                    }
                    result["enum"] = stringEnum;
                    break;

                // Pass through supported Gemini Schema keywords as-is
                case "description":
                case "required":
                case "nullable":
                case "format":
                case "minItems":
                case "maxItems":
                case "anyOf":
                    result[kvp.Key] = kvp.Value?.DeepClone();
                    break;

                    // Skip unsupported JSON Schema keywords (additionalProperties, $ref, etc.)
            }
        }

        return result;
    }

    private static JsonObject ConvertMessageToGemini(LLMMessage msg)
    {
        var parts = new JsonArray();

        switch (msg.Role)
        {
            case "user":
                if (!string.IsNullOrEmpty(msg.Content))
                    parts.Add(new JsonObject { ["text"] = msg.Content });
                return new JsonObject { ["role"] = "user", ["parts"] = parts };

            case "assistant":
                if (!string.IsNullOrEmpty(msg.Content))
                    parts.Add(new JsonObject { ["text"] = msg.Content });
                if (msg.ToolCalls is { Count: > 0 })
                {
                    foreach (var tc in msg.ToolCalls)
                    {
                        var args = string.IsNullOrWhiteSpace(tc.ArgumentsJson)
                            ? new JsonObject()
                            : JsonNode.Parse(tc.ArgumentsJson)?.AsObject() ?? new JsonObject();
                        parts.Add(new JsonObject
                        {
                            ["functionCall"] = new JsonObject
                            {
                                ["name"] = tc.Name,
                                ["args"] = args,
                            },
                        });
                    }
                }
                return new JsonObject { ["role"] = "model", ["parts"] = parts };

            case "tool":
                // Gemini expects function responses under role="user"
                // Parse tool result as structured JSON when possible
                JsonNode responseContent;
                try
                {
                    responseContent = JsonNode.Parse(msg.Content ?? "{}") ?? new JsonObject { ["result"] = msg.Content ?? "" };
                }
                catch
                {
                    responseContent = new JsonObject { ["result"] = msg.Content ?? "" };
                }

                parts.Add(new JsonObject
                {
                    ["functionResponse"] = new JsonObject
                    {
                        ["name"] = msg.ToolName ?? "unknown",
                        ["response"] = responseContent,
                    },
                });
                return new JsonObject { ["role"] = "user", ["parts"] = parts };

            default:
                parts.Add(new JsonObject { ["text"] = msg.Content ?? "" });
                return new JsonObject { ["role"] = "user", ["parts"] = parts };
        }
    }

    // ── Parse Gemini response ──────────────────────────────────

    private static LLMResponse ParseResponse(string responseBody)
    {
        var doc = JsonDocument.Parse(responseBody);
        var root = doc.RootElement;

        var usage = ParseUsage(root);

        if (!root.TryGetProperty("candidates", out var candidates) || candidates.GetArrayLength() == 0)
        {
            return new LLMResponse("I wasn't able to generate a response. Please try again.", null, usage);
        }

        var candidate = candidates[0];
        if (!candidate.TryGetProperty("content", out var content) ||
            !content.TryGetProperty("parts", out var parts))
        {
            return new LLMResponse("I wasn't able to generate a response. Please try again.", null, usage);
        }

        string? textContent = null;
        List<LLMToolCall>? toolCalls = null;

        foreach (var part in parts.EnumerateArray())
        {
            if (part.TryGetProperty("text", out var textProp))
            {
                textContent = (textContent is null ? "" : textContent + "\n") + textProp.GetString();
            }
            else if (part.TryGetProperty("functionCall", out var fc))
            {
                toolCalls ??= [];
                var name = fc.GetProperty("name").GetString() ?? "unknown";
                var args = fc.TryGetProperty("args", out var argsProp)
                    ? argsProp.GetRawText()
                    : "{}";
                toolCalls.Add(new LLMToolCall(
                    Id: Guid.NewGuid().ToString(),
                    Name: name,
                    ArgumentsJson: args
                ));
            }
        }

        return new LLMResponse(textContent, toolCalls, usage);
    }

    private static LLMUsage? ParseUsage(JsonElement root)
    {
        if (!root.TryGetProperty("usageMetadata", out var meta) || meta.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        int inputTokens = meta.TryGetProperty("promptTokenCount", out var prompt) ? prompt.GetInt32() : 0;
        int outputTokens = meta.TryGetProperty("candidatesTokenCount", out var cand) ? cand.GetInt32() : 0;
        int? cachedTokens = meta.TryGetProperty("cachedContentTokenCount", out var cached) ? cached.GetInt32() : null;

        return new LLMUsage(inputTokens, outputTokens, cachedTokens);
    }
}
