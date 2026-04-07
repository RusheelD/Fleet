using System.Text;
using System.Text.RegularExpressions;
using Fleet.Server.Data.Entities;
using Fleet.Server.Models;

namespace Fleet.Server.Memories;

public partial class MemoryService(IMemoryRepository repository, ILogger<MemoryService> logger) : IMemoryService
{
    private const int IndexEntryLimit = 24;
    private const int SelectedMemoryLimit = 5;
    private const int SelectedMemoryContentLimit = 1_500;
    private static readonly TimeSpan ReviewSoonThreshold = TimeSpan.FromDays(14);
    private static readonly TimeSpan StaleThreshold = TimeSpan.FromDays(45);

    public async Task<IReadOnlyList<MemoryEntryDto>> GetUserMemoriesAsync(int userId, CancellationToken cancellationToken = default)
        => (await repository.GetUserMemoriesAsync(userId, cancellationToken))
            .Select(ToDto)
            .ToList();

    public async Task<IReadOnlyList<MemoryEntryDto>> GetProjectMemoriesAsync(int userId, string projectId, CancellationToken cancellationToken = default)
        => (await repository.GetProjectMemoriesAsync(userId, projectId, cancellationToken))
            .Select(ToDto)
            .ToList();

    public Task<MemoryEntryDto> CreateUserMemoryAsync(int userId, UpsertMemoryEntryRequest request, CancellationToken cancellationToken = default)
        => CreateAsync(userId, null, request, cancellationToken);

    public async Task<MemoryEntryDto> UpdateUserMemoryAsync(int userId, int memoryId, UpsertMemoryEntryRequest request, CancellationToken cancellationToken = default)
    {
        var memory = await repository.GetUserMemoryAsync(userId, memoryId, cancellationToken)
            ?? throw new KeyNotFoundException("Memory not found.");
        return await UpdateAsync(memory, request, cancellationToken);
    }

    public async Task DeleteUserMemoryAsync(int userId, int memoryId, CancellationToken cancellationToken = default)
    {
        var memory = await repository.GetUserMemoryAsync(userId, memoryId, cancellationToken)
            ?? throw new KeyNotFoundException("Memory not found.");
        await repository.DeleteAsync(memory, cancellationToken);
    }

    public Task<MemoryEntryDto> CreateProjectMemoryAsync(int userId, string projectId, UpsertMemoryEntryRequest request, CancellationToken cancellationToken = default)
        => CreateAsync(userId, projectId, request, cancellationToken);

    public async Task<MemoryEntryDto> UpdateProjectMemoryAsync(int userId, string projectId, int memoryId, UpsertMemoryEntryRequest request, CancellationToken cancellationToken = default)
    {
        var memory = await repository.GetProjectMemoryAsync(userId, projectId, memoryId, cancellationToken)
            ?? throw new KeyNotFoundException("Memory not found.");
        return await UpdateAsync(memory, request, cancellationToken);
    }

    public async Task DeleteProjectMemoryAsync(int userId, string projectId, int memoryId, CancellationToken cancellationToken = default)
    {
        var memory = await repository.GetProjectMemoryAsync(userId, projectId, memoryId, cancellationToken)
            ?? throw new KeyNotFoundException("Memory not found.");
        await repository.DeleteAsync(memory, cancellationToken);
    }

    public async Task<string> BuildPromptBlockAsync(int userId, string? projectId, string? query, CancellationToken cancellationToken = default)
    {
        var memories = await repository.GetPromptMemoriesAsync(userId, projectId, cancellationToken);
        if (memories.Count == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        builder.AppendLine("## Memory");
        builder.AppendLine("Use these saved memories as working notes. They are helpful context, but they can be stale and should be verified against current project state when precision matters.");
        builder.AppendLine();
        builder.AppendLine("### Memory Index");

        foreach (var memory in memories.Take(IndexEntryLimit))
        {
            builder.AppendLine(BuildIndexLine(memory));
        }

        if (memories.Count > IndexEntryLimit)
        {
            builder.AppendLine($"- ... and {memories.Count - IndexEntryLimit} more saved memories not shown in the index.");
        }

        var selected = SelectRelevantMemories(memories, projectId, query)
            .Take(SelectedMemoryLimit)
            .ToList();

        if (selected.Count == 0)
        {
            return builder.ToString().TrimEnd();
        }

        builder.AppendLine();
        builder.AppendLine("### Selected Memories");
        foreach (var memory in selected)
        {
            builder.AppendLine();
            builder.AppendLine($"#### {memory.Name} [{memory.Type}, {GetScope(memory)}]");
            builder.AppendLine(memory.Description);

            var stalenessMessage = GetStalenessMessage(memory.UpdatedAtUtc);
            if (!string.IsNullOrWhiteSpace(stalenessMessage))
            {
                builder.AppendLine($"Staleness note: {stalenessMessage}");
            }

            builder.AppendLine(TrimForPrompt(memory.Content, SelectedMemoryContentLimit));
        }

        return builder.ToString().TrimEnd();
    }

    private async Task<MemoryEntryDto> CreateAsync(int userId, string? projectId, UpsertMemoryEntryRequest request, CancellationToken cancellationToken)
    {
        var normalized = NormalizeRequest(request);
        var memory = new MemoryEntry
        {
            UserProfileId = userId,
            ProjectId = string.IsNullOrWhiteSpace(projectId) ? null : projectId,
            Name = normalized.Name,
            Description = normalized.Description,
            Type = normalized.Type,
            Content = normalized.Content,
            AlwaysInclude = normalized.AlwaysInclude,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
        };

        await repository.AddAsync(memory, cancellationToken);
        logger.LogInformation("Created {Scope} memory '{MemoryName}' for user {UserId}", GetScope(memory), memory.Name, userId);
        return ToDto(memory);
    }

    private async Task<MemoryEntryDto> UpdateAsync(MemoryEntry memory, UpsertMemoryEntryRequest request, CancellationToken cancellationToken)
    {
        var normalized = NormalizeRequest(request);
        memory.Name = normalized.Name;
        memory.Description = normalized.Description;
        memory.Type = normalized.Type;
        memory.Content = normalized.Content;
        memory.AlwaysInclude = normalized.AlwaysInclude;
        memory.UpdatedAtUtc = DateTime.UtcNow;

        await repository.SaveChangesAsync(cancellationToken);
        return ToDto(memory);
    }

    private static UpsertMemoryEntryRequest NormalizeRequest(UpsertMemoryEntryRequest request)
    {
        var name = request.Name?.Trim() ?? string.Empty;
        var description = request.Description?.Trim() ?? string.Empty;
        var content = request.Content?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new InvalidOperationException("Memory name is required.");
        }

        if (string.IsNullOrWhiteSpace(description))
        {
            throw new InvalidOperationException("Memory description is required.");
        }

        if (string.IsNullOrWhiteSpace(content))
        {
            throw new InvalidOperationException("Memory content is required.");
        }

        if (!MemoryEntryTypes.IsValid(request.Type))
        {
            throw new InvalidOperationException($"Memory type must be one of: {string.Join(", ", MemoryEntryTypes.All)}.");
        }

        return request with
        {
            Name = name,
            Description = description,
            Type = MemoryEntryTypes.Normalize(request.Type),
            Content = content,
        };
    }

    private static IEnumerable<MemoryEntry> SelectRelevantMemories(
        IReadOnlyList<MemoryEntry> memories,
        string? projectId,
        string? query)
    {
        var queryTokens = Tokenize(query);
        return memories
            .Select(memory => new
            {
                Memory = memory,
                Score = ScoreMemory(memory, queryTokens, projectId),
            })
            .Where(item => item.Score > 0)
            .OrderByDescending(item => item.Score)
            .ThenByDescending(item => item.Memory.AlwaysInclude)
            .ThenByDescending(item => item.Memory.ProjectId == projectId)
            .ThenByDescending(item => item.Memory.UpdatedAtUtc)
            .Select(item => item.Memory);
    }

    private static int ScoreMemory(MemoryEntry memory, HashSet<string> queryTokens, string? projectId)
    {
        var score = memory.AlwaysInclude ? 500 : 0;
        if (memory.ProjectId == projectId && !string.IsNullOrWhiteSpace(projectId))
        {
            score += 40;
        }

        if (queryTokens.Count == 0)
        {
            return score > 0 ? score : memory.ProjectId == projectId ? 5 : 0;
        }

        var searchableText = $"{memory.Name} {memory.Description} {memory.Type} {memory.Content}";
        var matchedTokens = queryTokens.Count(token =>
            searchableText.Contains(token, StringComparison.OrdinalIgnoreCase));

        score += matchedTokens * 25;

        if (matchedTokens > 0 && searchableText.Contains(queryTokens.First(), StringComparison.OrdinalIgnoreCase))
        {
            score += 10;
        }

        return score;
    }

    private static HashSet<string> Tokenize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        return [.. WordRegex().Matches(text)
            .Select(match => match.Value.ToLowerInvariant())
            .Where(token => token.Length >= 3)];
    }

    private static string BuildIndexLine(MemoryEntry memory)
    {
        var pinned = memory.AlwaysInclude ? ", pinned" : string.Empty;
        return $"- [{memory.Type}] {memory.Name} ({GetScope(memory)}{pinned}) - {memory.Description}";
    }

    private static string GetScope(MemoryEntry memory)
        => string.IsNullOrWhiteSpace(memory.ProjectId) ? "personal" : "project";

    private static MemoryEntryDto ToDto(MemoryEntry memory)
    {
        var stalenessMessage = GetStalenessMessage(memory.UpdatedAtUtc);
        return new MemoryEntryDto(
            memory.Id,
            memory.Name,
            memory.Description,
            memory.Type,
            memory.Content,
            memory.AlwaysInclude,
            GetScope(memory),
            memory.ProjectId,
            memory.CreatedAtUtc,
            memory.UpdatedAtUtc,
            !string.IsNullOrWhiteSpace(stalenessMessage),
            stalenessMessage);
    }

    private static string? GetStalenessMessage(DateTime updatedAtUtc)
    {
        var age = DateTime.UtcNow - updatedAtUtc;
        if (age >= StaleThreshold)
        {
            return $"Last updated on {updatedAtUtc:yyyy-MM-dd}. Verify it before relying on exact details.";
        }

        if (age >= ReviewSoonThreshold)
        {
            return $"Last updated on {updatedAtUtc:yyyy-MM-dd}. Reconfirm it if the project has changed recently.";
        }

        return null;
    }

    private static string TrimForPrompt(string content, int maxLength)
    {
        if (content.Length <= maxLength)
        {
            return content;
        }

        return $"{content[..maxLength].TrimEnd()}\n\n[Memory content truncated]";
    }

    [GeneratedRegex("[A-Za-z0-9_\\-/]+", RegexOptions.Compiled)]
    private static partial Regex WordRegex();
}
