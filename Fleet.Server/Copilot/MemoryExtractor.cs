using Fleet.Server.LLM;
using Fleet.Server.Memories;
using Fleet.Server.Models;

namespace Fleet.Server.Copilot;

/// <summary>
/// Extracts key facts from chat conversations and saves them as memories.
/// Runs in the background after each chat exchange to build an evolving
/// knowledge base without blocking the user.
/// Inspired by Claude Code's background memory extraction pattern.
/// </summary>
public interface IMemoryExtractor
{
    /// <summary>
    /// Analyze recent chat messages and extract key facts worth remembering.
    /// Saves to project-scoped memories (or user-scoped if no project).
    /// </summary>
    Task ExtractAndSaveAsync(
        int userId,
        string? projectId,
        IReadOnlyList<LLMMessage> recentMessages,
        CancellationToken cancellationToken = default);
}

/// <inheritdoc />
public class MemoryExtractor(
    ILLMClient llmClient,
    IMemoryService memoryService,
    IModelCatalog modelCatalog,
    ILogger<MemoryExtractor> logger) : IMemoryExtractor
{
    private const string ExtractionPrompt = """
        You are a memory extraction assistant. Analyze the recent conversation and extract
        key facts that would be useful to remember for future interactions. Focus on:

        1. User preferences (coding style, tool preferences, naming conventions)
        2. Project decisions (architecture choices, technology stack, design patterns)
        3. Important context (project goals, constraints, deadlines)
        4. Corrections or feedback (things the user corrected or emphasized)

        Return ONLY a JSON array of objects, each with:
        - "name": short title (max 50 chars)
        - "content": the factual information to remember (max 200 chars)
        - "type": one of "preference", "decision", "context", "feedback"

        If nothing is worth extracting, return an empty array [].
        Only return facts that are genuinely useful for future context.
        Do not extract trivial or obvious information.
        """;

    /// <summary>Minimum messages before extraction is worthwhile.</summary>
    private const int MinMessagesForExtraction = 4;

    /// <summary>Max recent messages to analyze (keeps LLM call cheap).</summary>
    private const int MaxRecentMessages = 10;

    public async Task ExtractAndSaveAsync(
        int userId,
        string? projectId,
        IReadOnlyList<LLMMessage> recentMessages,
        CancellationToken cancellationToken = default)
    {
        if (recentMessages.Count < MinMessagesForExtraction)
            return;

        try
        {
            // Take only the last N messages to keep the extraction cheap
            var messagesToAnalyze = recentMessages
                .TakeLast(MaxRecentMessages)
                .Select(m => new LLMMessage
                {
                    Role = m.Role == "tool" ? "user" : m.Role,
                    Content = m.Role == "tool"
                        ? $"[Tool result from {m.ToolName}]: {Truncate(m.Content, 200)}"
                        : Truncate(m.Content, 500),
                })
                .ToList();

            // Use a fast/cheap model for extraction
            var model = modelCatalog.Get("Haiku");
            var request = new LLMRequest(
                ExtractionPrompt,
                messagesToAnalyze,
                ModelOverride: model,
                MaxTokens: 1024);

            var response = await llmClient.CompleteAsync(request, cancellationToken);

            if (string.IsNullOrWhiteSpace(response.Content))
                return;

            // Parse the JSON array of extracted facts
            var facts = ParseExtractedFacts(response.Content);
            if (facts.Count == 0)
                return;

            foreach (var fact in facts.Take(3)) // Cap at 3 memories per extraction
            {
                var memoryRequest = new UpsertMemoryEntryRequest(
                    Name: fact.Name,
                    Description: $"Auto-extracted from chat on {DateTime.UtcNow:yyyy-MM-dd}",
                    Type: fact.Type,
                    Content: fact.Content,
                    AlwaysInclude: false);

                if (string.IsNullOrWhiteSpace(projectId))
                {
                    await memoryService.CreateUserMemoryAsync(userId, memoryRequest, cancellationToken);
                }
                else
                {
                    await memoryService.CreateProjectMemoryAsync(userId, projectId, memoryRequest, cancellationToken);
                }
            }

            logger.LogInformation(
                "Extracted {Count} memories from chat for user {UserId}, project {ProjectId}",
                facts.Count, userId, projectId);
        }
        catch (Exception ex)
        {
            // Memory extraction is non-critical — log and move on
            logger.LogWarning(ex, "Background memory extraction failed for user {UserId}", userId);
        }
    }

    private static List<ExtractedFact> ParseExtractedFacts(string content)
    {
        var results = new List<ExtractedFact>();

        try
        {
            // Find the JSON array in the response (model may include extra text)
            var start = content.IndexOf('[');
            var end = content.LastIndexOf(']');
            if (start < 0 || end < 0 || end <= start)
                return results;

            var jsonArray = content[start..(end + 1)];
            using var doc = System.Text.Json.JsonDocument.Parse(jsonArray);

            foreach (var element in doc.RootElement.EnumerateArray())
            {
                var name = element.TryGetProperty("name", out var nameProp) ? nameProp.GetString() : null;
                var factContent = element.TryGetProperty("content", out var contentProp) ? contentProp.GetString() : null;
                var type = element.TryGetProperty("type", out var typeProp) ? typeProp.GetString() : "context";

                if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(factContent))
                {
                    results.Add(new ExtractedFact(name!, factContent!, type ?? "context"));
                }
            }
        }
        catch
        {
            // JSON parsing failure — model returned malformed output
        }

        return results;
    }

    private static string? Truncate(string? text, int maxLength)
    {
        if (text is null || text.Length <= maxLength)
            return text;
        return text[..maxLength] + "…";
    }

    private record ExtractedFact(string Name, string Content, string Type);
}
