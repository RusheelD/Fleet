using System.Text.Json;
using System.Text.Json.Nodes;
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
        var url = $"https://generativelanguage.googleapis.com/v1beta/models/{config.Model}:generateContent?key={config.ApiKey}";

        var body = BuildRequestBody(request);
        logger.LogDebug("Gemini request body: {Body}", body.ToJsonString());

        using var httpClient = httpClientFactory.CreateClient();
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(body.ToJsonString(), System.Text.Encoding.UTF8, "application/json"),
        };

        using var httpResponse = await httpClient.SendAsync(httpRequest, cancellationToken);
        var responseBody = await httpResponse.Content.ReadAsStringAsync(cancellationToken);

        if (!httpResponse.IsSuccessStatusCode)
        {
            logger.LogError("Gemini API error {Status}: {Body}", httpResponse.StatusCode, responseBody);
            throw new InvalidOperationException($"Gemini API returned {httpResponse.StatusCode}: {responseBody}");
        }

        logger.LogDebug("Gemini response: {Body}", responseBody);
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

        // Contents (conversation history)
        var contents = new JsonArray();
        foreach (var msg in request.Messages)
        {
            contents.Add(ConvertMessageToGemini(msg));
        }
        body["contents"] = contents;

        // Tools
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
                    declaration["parameters"] = JsonNode.Parse(tool.ParametersJsonSchema);
                }
                functionDeclarations.Add(declaration);
            }
            body["tools"] = new JsonArray
            {
                new JsonObject { ["functionDeclarations"] = functionDeclarations },
            };
        }

        return body;
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
                parts.Add(new JsonObject
                {
                    ["functionResponse"] = new JsonObject
                    {
                        ["name"] = msg.ToolName ?? "unknown",
                        ["response"] = new JsonObject
                        {
                            ["result"] = msg.Content ?? "",
                        },
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

        if (!root.TryGetProperty("candidates", out var candidates) || candidates.GetArrayLength() == 0)
        {
            return new LLMResponse("I wasn't able to generate a response. Please try again.", null);
        }

        var candidate = candidates[0];
        if (!candidate.TryGetProperty("content", out var content) ||
            !content.TryGetProperty("parts", out var parts))
        {
            return new LLMResponse("I wasn't able to generate a response. Please try again.", null);
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

        return new LLMResponse(textContent, toolCalls);
    }
}
