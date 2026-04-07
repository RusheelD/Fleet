using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Fleet.Server.Logging;
using Microsoft.Extensions.Options;

namespace Fleet.Server.LLM;

/// <summary>
/// LLM client for Azure OpenAI Responses API.
/// </summary>
public class AzureOpenAiClient(
    IHttpClientFactory httpClientFactory,
    IOptions<LLMOptions> options,
    ILogger<AzureOpenAiClient> logger) : ILLMClient
{
    private const int DefaultMaxOutputTokens = 4096;

    public async Task<LLMResponse> CompleteAsync(LLMRequest request, CancellationToken cancellationToken = default)
    {
        var config = options.Value;
        if (string.IsNullOrWhiteSpace(config.ApiKey))
        {
            throw new InvalidOperationException(
                "Azure OpenAI API key is not configured. Set LLM:ApiKey or AZURE_OPENAI_API_KEY.");
        }

        if (string.IsNullOrWhiteSpace(config.Endpoint))
        {
            throw new InvalidOperationException(
                "Azure OpenAI endpoint is not configured. Set LLM:Endpoint or AZURE_OPENAI_ENDPOINT.");
        }

        var model = request.ModelOverride ?? config.Model;
        var body = BuildRequestBody(request, model);
        logger.LogDebug("Azure OpenAI request body. body={Body}", body.ToJsonString().SanitizeForLogging());

        using var httpClient = httpClientFactory.CreateClient("LLM");
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, config.Endpoint)
        {
            Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json"),
        };
        httpRequest.Headers.Add("api-key", config.ApiKey);

        var (statusCode, responseBody) = await IdleTimeoutHandler.SendWithIdleTimeoutAsync(
            httpClient, httpRequest, cancellationToken);

        if ((int)statusCode < 200 || (int)statusCode >= 300)
        {
            logger.LogError(
                "Azure OpenAI API error. status={StatusCode} body={Body}",
                (int)statusCode,
                responseBody.SanitizeForLogging());
            throw new InvalidOperationException(
                $"Azure OpenAI Responses API returned {statusCode}: {responseBody}");
        }

        logger.LogDebug("Azure OpenAI response body. body={Body}", responseBody.SanitizeForLogging());
        return ParseResponse(responseBody);
    }

    private static JsonObject BuildRequestBody(LLMRequest request, string model)
    {
        var body = new JsonObject
        {
            ["model"] = model,
            ["max_output_tokens"] = request.MaxTokens ?? DefaultMaxOutputTokens,
            ["input"] = BuildInputItems(request.Messages),
        };

        if (!string.IsNullOrWhiteSpace(request.SystemPrompt))
        {
            body["instructions"] = request.SystemPrompt;
        }

        if (request.Tools is { Count: > 0 })
        {
            var tools = new JsonArray();
            foreach (var tool in request.Tools)
            {
                JsonNode parameters = new JsonObject
                {
                    ["type"] = "object",
                    ["properties"] = new JsonObject(),
                };

                if (!string.IsNullOrWhiteSpace(tool.ParametersJsonSchema))
                {
                    parameters = JsonNode.Parse(tool.ParametersJsonSchema) ?? parameters;
                }

                tools.Add(new JsonObject
                {
                    ["type"] = "function",
                    ["name"] = tool.Name,
                    ["description"] = tool.Description,
                    ["parameters"] = parameters,
                });
            }

            body["tools"] = tools;
            body["tool_choice"] = "auto";
        }

        return body;
    }

    private static JsonArray BuildInputItems(IReadOnlyList<LLMMessage> messages)
    {
        var items = new JsonArray();

        foreach (var msg in messages)
        {
            switch (msg.Role)
            {
                case "user":
                    if (!string.IsNullOrWhiteSpace(msg.Content))
                    {
                        items.Add(new JsonObject
                        {
                            ["role"] = "user",
                            ["content"] = msg.Content,
                        });
                    }
                    break;

                case "assistant":
                    if (!string.IsNullOrWhiteSpace(msg.Content))
                    {
                        items.Add(new JsonObject
                        {
                            ["role"] = "assistant",
                            ["content"] = msg.Content,
                        });
                    }

                    if (msg.ToolCalls is { Count: > 0 })
                    {
                        foreach (var call in msg.ToolCalls)
                        {
                            items.Add(new JsonObject
                            {
                                ["type"] = "function_call",
                                ["call_id"] = call.Id,
                                ["name"] = call.Name,
                                ["arguments"] = string.IsNullOrWhiteSpace(call.ArgumentsJson) ? "{}" : call.ArgumentsJson,
                            });
                        }
                    }
                    break;

                case "tool":
                    items.Add(new JsonObject
                    {
                        ["type"] = "function_call_output",
                        ["call_id"] = msg.ToolCallId ?? Guid.NewGuid().ToString("N"),
                        ["output"] = msg.Content ?? string.Empty,
                    });
                    break;

                default:
                    if (!string.IsNullOrWhiteSpace(msg.Content))
                    {
                        items.Add(new JsonObject
                        {
                            ["role"] = "user",
                            ["content"] = msg.Content,
                        });
                    }
                    break;
            }
        }

        return items;
    }

    private static LLMResponse ParseResponse(string responseBody)
    {
        using var doc = JsonDocument.Parse(responseBody);
        var root = doc.RootElement;

        string? content = null;
        List<LLMToolCall>? toolCalls = null;

        if (root.TryGetProperty("output_text", out var outputTextProp) &&
            outputTextProp.ValueKind == JsonValueKind.String)
        {
            content = outputTextProp.GetString();
        }

        if (root.TryGetProperty("output", out var outputProp) &&
            outputProp.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in outputProp.EnumerateArray())
            {
                var type = item.TryGetProperty("type", out var typeProp)
                    ? typeProp.GetString()
                    : null;

                if (string.Equals(type, "function_call", StringComparison.OrdinalIgnoreCase))
                {
                    var callId = item.TryGetProperty("call_id", out var callIdProp)
                        ? callIdProp.GetString()
                        : null;
                    var id = !string.IsNullOrWhiteSpace(callId)
                        ? callId!
                        : item.TryGetProperty("id", out var idProp) ? idProp.GetString() : Guid.NewGuid().ToString("N");

                    var name = item.TryGetProperty("name", out var nameProp)
                        ? nameProp.GetString() ?? "unknown"
                        : "unknown";
                    var arguments = item.TryGetProperty("arguments", out var argsProp)
                        ? argsProp.GetString()
                        : "{}";

                    toolCalls ??= [];
                    toolCalls.Add(new LLMToolCall(id!, name, string.IsNullOrWhiteSpace(arguments) ? "{}" : arguments!));
                    continue;
                }

                if (string.Equals(type, "message", StringComparison.OrdinalIgnoreCase) &&
                    item.TryGetProperty("content", out var messageContentProp) &&
                    messageContentProp.ValueKind == JsonValueKind.Array)
                {
                    foreach (var part in messageContentProp.EnumerateArray())
                    {
                        var partType = part.TryGetProperty("type", out var partTypeProp)
                            ? partTypeProp.GetString()
                            : null;
                        if (string.Equals(partType, "output_text", StringComparison.OrdinalIgnoreCase) &&
                            part.TryGetProperty("text", out var textProp))
                        {
                            var text = textProp.GetString();
                            if (!string.IsNullOrWhiteSpace(text))
                            {
                                content = string.IsNullOrWhiteSpace(content)
                                    ? text
                                    : $"{content}\n{text}";
                            }
                        }
                    }
                }
            }
        }

        var stopReason = root.TryGetProperty("status", out var statusProp)
            ? statusProp.GetString()
            : null;
        // Map Responses API "incomplete" with "max_output_tokens" reason to "max_tokens"
        if (string.Equals(stopReason, "incomplete", StringComparison.OrdinalIgnoreCase) &&
            root.TryGetProperty("incomplete_details", out var details) &&
            details.TryGetProperty("reason", out var reasonProp))
        {
            var reason = reasonProp.GetString();
            if (string.Equals(reason, "max_output_tokens", StringComparison.OrdinalIgnoreCase))
                stopReason = "max_tokens";
        }

        return new LLMResponse(content, toolCalls, ParseUsage(root), stopReason);
    }

    private static LLMUsage? ParseUsage(JsonElement root)
    {
        if (!root.TryGetProperty("usage", out var usageProp) || usageProp.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        int inputTokens = usageProp.TryGetProperty("input_tokens", out var inp)
            ? inp.GetInt32()
            : usageProp.TryGetProperty("prompt_tokens", out var prompt)
                ? prompt.GetInt32()
                : 0;

        int outputTokens = usageProp.TryGetProperty("output_tokens", out var outp)
            ? outp.GetInt32()
            : usageProp.TryGetProperty("completion_tokens", out var comp)
                ? comp.GetInt32()
                : 0;

        int? cachedTokens = null;
        if (usageProp.TryGetProperty("input_tokens_details", out var details) &&
            details.TryGetProperty("cached_tokens", out var cached))
        {
            cachedTokens = cached.GetInt32();
        }
        else if (usageProp.TryGetProperty("prompt_tokens_details", out var promptDetails) &&
                 promptDetails.TryGetProperty("cached_tokens", out var cachedPrompt))
        {
            cachedTokens = cachedPrompt.GetInt32();
        }

        return new LLMUsage(inputTokens, outputTokens, cachedTokens);
    }
}
