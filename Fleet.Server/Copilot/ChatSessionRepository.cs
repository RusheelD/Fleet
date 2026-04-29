using Fleet.Server.Auth;
using Fleet.Server.Data;
using Fleet.Server.Data.Entities;
using Fleet.Server.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Fleet.Server.Copilot;

public class ChatSessionRepository(FleetDbContext context, IAuthService authService) : IChatSessionRepository
{
    private static readonly JsonSerializerOptions RecentActivityJsonOptions = new(JsonSerializerDefaults.Web);
    private const int MaxRecentActivityEntries = 16;

    private static readonly string[] ProjectScopedSuggestions =
    [
        "Assign agents to auth work items",
        "Generate spec document",
        "Add more work items",
        "Show repo structure",
    ];

    private static readonly string[] GlobalSuggestions =
    [
        "List all my projects",
        "Show my GitHub repositories",
        "Find active work items across projects",
        "Summarize project status",
    ];

    public async Task<IReadOnlyList<ChatSessionDto>> GetSessionsByProjectIdAsync(string projectId)
    {
        var ownerId = await GetCurrentOwnerIdAsync();
        var scopeProjectId = NormalizeProjectId(projectId);

        var entities = await ScopedSessions(ownerId, scopeProjectId)
            .AsNoTracking()
            .ToListAsync();

        return entities
            .Select(ToSessionDto)
            .ToList();
    }

    public async Task<IReadOnlyList<ChatMessageDto>> GetMessagesBySessionIdAsync(string projectId, string sessionId, string? ownerId = null)
    {
        var effectiveOwnerId = await ResolveOwnerIdAsync(ownerId);
        var scopeProjectId = NormalizeProjectId(projectId);

        var entities = await context.ChatMessages
            .AsNoTracking()
            .Where(m =>
                m.ChatSessionId == sessionId &&
                m.ChatSession.OwnerId == effectiveOwnerId &&
                (scopeProjectId == null
                    ? m.ChatSession.ProjectId == null
                    : m.ChatSession.ProjectId == scopeProjectId))
            .OrderBy(m => m.Timestamp)
            .ToListAsync();

        var messageIds = entities
            .Select(m => m.Id)
            .ToArray();

        Dictionary<string, ChatAttachmentDto[]> attachmentsByMessageId = [];
        if (messageIds.Length > 0)
        {
            attachmentsByMessageId = await context.ChatAttachments
                .AsNoTracking()
                .Where(a =>
                    a.ChatMessageId != null &&
                    messageIds.Contains(a.ChatMessageId) &&
                    a.ChatSession.OwnerId == effectiveOwnerId &&
                    (scopeProjectId == null
                        ? a.ChatSession.ProjectId == null
                        : a.ChatSession.ProjectId == scopeProjectId))
                .OrderBy(a => a.UploadedAt)
                .GroupBy(a => a.ChatMessageId!)
                .ToDictionaryAsync(
                    group => group.Key,
                    group => group.Select(ToAttachmentDto).ToArray());
        }

        return entities.Select(m => new ChatMessageDto(m.Id, m.Role, m.Content, m.Timestamp)
        {
            Attachments = attachmentsByMessageId.GetValueOrDefault(m.Id, [])
        }).ToList();
    }

    public Task<string[]> GetSuggestionsAsync(string projectId)
    {
        var suggestions = IsGlobalScope(projectId) ? GlobalSuggestions : ProjectScopedSuggestions;
        return Task.FromResult(suggestions);
    }

    public async Task<ChatSessionDto> CreateSessionAsync(string projectId, string title)
    {
        var ownerId = await GetCurrentOwnerIdAsync();
        var scopeProjectId = NormalizeProjectId(projectId);

        var entity = new ChatSession
        {
            Id = Guid.NewGuid().ToString(),
            OwnerId = ownerId,
            Title = title,
            LastMessage = "",
            Timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            IsActive = true,
            ProjectId = scopeProjectId
        };

        var activeSessions = await ScopedSessions(ownerId, scopeProjectId)
            .Where(s => s.IsActive)
            .ToListAsync();
        foreach (var session in activeSessions)
        {
            session.IsActive = false;
        }

        context.ChatSessions.Add(entity);
        await context.SaveChangesAsync();

        return ToSessionDto(entity);
    }

    public Task<bool> RenameSessionAsync(string sessionId, string title)
        => RenameSessionAsync(string.Empty, sessionId, title);

    public async Task<bool> RenameSessionAsync(string projectId, string sessionId, string title, string? ownerId = null)
    {
        var effectiveOwnerId = await ResolveOwnerIdAsync(ownerId);
        var scopeProjectId = NormalizeProjectId(projectId);

        var entity = await ScopedSessions(effectiveOwnerId, scopeProjectId)
            .FirstOrDefaultAsync(s => s.Id == sessionId);
        if (entity is null) return false;

        entity.Title = title;
        await context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> UpdateDynamicIterationAsync(
        string projectId,
        string sessionId,
        bool isEnabled,
        string? branch,
        string? policyJson,
        string? ownerId = null)
    {
        var effectiveOwnerId = await ResolveOwnerIdAsync(ownerId);
        var scopeProjectId = NormalizeProjectId(projectId);

        var entity = await ScopedSessions(effectiveOwnerId, scopeProjectId)
            .FirstOrDefaultAsync(s => s.Id == sessionId);
        if (entity is null)
            return false;

        entity.IsDynamicIterationEnabled = isEnabled;
        entity.DynamicIterationBranch = isEnabled ? NormalizeNullableString(branch) : null;
        entity.DynamicIterationPolicyJson = isEnabled ? NormalizeNullableString(policyJson) : null;
        await context.SaveChangesAsync();
        return true;
    }

    public Task<bool> DeleteSessionAsync(string sessionId)
        => DeleteSessionAsync(string.Empty, sessionId);

    public async Task<bool> DeleteSessionAsync(string projectId, string sessionId)
    {
        var ownerId = await GetCurrentOwnerIdAsync();
        var scopeProjectId = NormalizeProjectId(projectId);

        var entity = await ScopedSessions(ownerId, scopeProjectId)
            .FirstOrDefaultAsync(s => s.Id == sessionId);
        if (entity is null)
        {
            // Scope can drift on the client during project/global transitions.
            // Allow delete by owner + id as a safe fallback to avoid false 404s.
            entity = await context.ChatSessions
                .FirstOrDefaultAsync(s => s.OwnerId == ownerId && s.Id == sessionId);
        }
        if (entity is null) return false;

        context.ChatSessions.Remove(entity);
        await context.SaveChangesAsync();
        return true;
    }

    public async Task<ChatMessageDto> AddMessageAsync(string projectId, string sessionId, string role, string content, string? ownerId = null)
    {
        var effectiveOwnerId = await ResolveOwnerIdAsync(ownerId);
        var scopeProjectId = NormalizeProjectId(projectId);

        var session = await ScopedSessions(effectiveOwnerId, scopeProjectId)
            .FirstOrDefaultAsync(s => s.Id == sessionId);
        if (session is null)
            throw new KeyNotFoundException("Chat session not found.");

        var now = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
        var entity = new ChatMessage
        {
            Id = Guid.NewGuid().ToString(),
            Role = role,
            Content = content,
            Timestamp = now,
            ChatSessionId = sessionId
        };

        context.ChatMessages.Add(entity);

        session.LastMessage = content.Length > 100 ? content[..100] + "..." : content;
        session.Timestamp = now;

        await context.SaveChangesAsync();

        return new ChatMessageDto(entity.Id, entity.Role, entity.Content, entity.Timestamp);
    }

    public Task SetSessionGeneratingAsync(string sessionId, bool isGenerating)
        => SetSessionGeneratingAsync(string.Empty, sessionId, isGenerating);

    public async Task SetSessionGeneratingAsync(string projectId, string sessionId, bool isGenerating)
    {
        await UpdateSessionGenerationStateAsync(
            projectId,
            sessionId,
            isGenerating,
            isGenerating ? ChatGenerationStates.Running : ChatGenerationStates.Idle,
            null);
    }

    public async Task UpdateSessionGenerationStateAsync(
        string projectId,
        string sessionId,
        bool isGenerating,
        string generationState,
        string? generationStatus,
        ChatSessionActivityDto? activity = null,
        string? ownerId = null)
    {
        var effectiveOwnerId = await ResolveOwnerIdAsync(ownerId);
        var scopeProjectId = NormalizeProjectId(projectId);

        var session = await ScopedSessions(effectiveOwnerId, scopeProjectId)
            .FirstOrDefaultAsync(s => s.Id == sessionId);
        if (session is null)
            return;

        session.IsGenerating = isGenerating;
        session.GenerationState = generationState;
        session.GenerationStatus = generationStatus;
        session.GenerationUpdatedAtUtc = DateTime.UtcNow;
        if (activity is not null)
        {
            session.RecentActivityJson = SerializeRecentActivity(
                AppendRecentActivity(DeserializeRecentActivity(session.RecentActivityJson), activity));
        }
        await context.SaveChangesAsync();
    }

    public async Task<int> MarkStaleGeneratingSessionsAsync(
        string projectId,
        DateTime cutoffUtc,
        string generationState,
        string generationStatus)
    {
        var ownerId = await GetCurrentOwnerIdAsync();
        var scopeProjectId = NormalizeProjectId(projectId);

        var staleSessions = await ScopedSessions(ownerId, scopeProjectId)
            .Where(s => s.IsGenerating && (!s.GenerationUpdatedAtUtc.HasValue || s.GenerationUpdatedAtUtc < cutoffUtc))
            .ToListAsync();

        if (staleSessions.Count == 0)
            return 0;

        foreach (var session in staleSessions)
        {
            session.IsGenerating = false;
            session.GenerationState = generationState;
            session.GenerationStatus = generationStatus;
            session.GenerationUpdatedAtUtc = DateTime.UtcNow;
            session.RecentActivityJson = SerializeRecentActivity(AppendRecentActivity(
                DeserializeRecentActivity(session.RecentActivityJson),
                new ChatSessionActivityDto(
                    Guid.NewGuid().ToString(),
                    "status",
                    generationStatus,
                    DateTime.UtcNow.ToString("O"))));
        }

        await context.SaveChangesAsync();
        return staleSessions.Count;
    }

    public async Task AppendSessionActivityAsync(string projectId, string sessionId, ChatSessionActivityDto activity, string? ownerId = null)
    {
        var effectiveOwnerId = await ResolveOwnerIdAsync(ownerId);
        var scopeProjectId = NormalizeProjectId(projectId);

        var session = await ScopedSessions(effectiveOwnerId, scopeProjectId)
            .FirstOrDefaultAsync(s => s.Id == sessionId);
        if (session is null)
            return;

        session.RecentActivityJson = SerializeRecentActivity(
            AppendRecentActivity(DeserializeRecentActivity(session.RecentActivityJson), activity));
        await context.SaveChangesAsync();
    }

    public Task<ChatAttachmentDto> AddAttachmentAsync(
        string attachmentId,
        string sessionId,
        string fileName,
        string content,
        string contentType,
        int contentLength,
        string? storagePath)
        => AddAttachmentAsync(attachmentId, string.Empty, sessionId, fileName, content, contentType, contentLength, storagePath);

    public async Task<ChatAttachmentDto> AddAttachmentAsync(
        string attachmentId,
        string projectId,
        string sessionId,
        string fileName,
        string content,
        string contentType,
        int contentLength,
        string? storagePath)
    {
        var ownerId = await GetCurrentOwnerIdAsync();
        var scopeProjectId = NormalizeProjectId(projectId);

        var sessionExists = await ScopedSessions(ownerId, scopeProjectId)
            .AsNoTracking()
            .AnyAsync(s => s.Id == sessionId);
        if (!sessionExists)
            throw new KeyNotFoundException("Chat session not found.");

        var entity = new ChatAttachment
        {
            Id = attachmentId,
            FileName = fileName,
            Content = content,
            ContentType = NormalizeContentType(contentType),
            ContentLength = contentLength > 0 ? contentLength : content.Length,
            StoragePath = storagePath,
            UploadedAt = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            ChatSessionId = sessionId
        };

        context.ChatAttachments.Add(entity);
        await context.SaveChangesAsync();

        return ToAttachmentDto(entity);
    }

    public Task<IReadOnlyList<ChatAttachmentDto>> GetAttachmentsBySessionIdAsync(string sessionId)
        => GetAttachmentsBySessionIdAsync(string.Empty, sessionId);

    public async Task<IReadOnlyList<ChatAttachmentDto>> GetAttachmentsBySessionIdAsync(string projectId, string sessionId)
    {
        var ownerId = await GetCurrentOwnerIdAsync();
        var scopeProjectId = NormalizeProjectId(projectId);

        var entities = await context.ChatAttachments
            .AsNoTracking()
            .Where(a =>
                a.ChatSessionId == sessionId &&
                a.ChatMessageId == null &&
                a.ChatSession.OwnerId == ownerId &&
                (scopeProjectId == null
                    ? a.ChatSession.ProjectId == null
                    : a.ChatSession.ProjectId == scopeProjectId))
            .OrderByDescending(a => a.UploadedAt)
            .ToListAsync();

        return entities.Select(ToAttachmentDto).ToList();
    }

    public async Task<IReadOnlyList<ChatAttachmentDto>> GetAttachmentsByMessageIdAsync(string projectId, string messageId, string? ownerId = null)
    {
        var effectiveOwnerId = await ResolveOwnerIdAsync(ownerId);
        var scopeProjectId = NormalizeProjectId(projectId);

        var entities = await context.ChatAttachments
            .AsNoTracking()
            .Where(a =>
                a.ChatMessageId == messageId &&
                a.ChatSession.OwnerId == effectiveOwnerId &&
                (scopeProjectId == null
                    ? a.ChatSession.ProjectId == null
                    : a.ChatSession.ProjectId == scopeProjectId))
            .OrderBy(a => a.UploadedAt)
            .ToListAsync();

        return entities.Select(ToAttachmentDto).ToList();
    }

    public async Task<IReadOnlyList<ChatAttachmentDto>> GetAllAttachmentsBySessionIdAsync(string projectId, string sessionId, string? ownerId = null)
    {
        var effectiveOwnerId = await ResolveOwnerIdAsync(ownerId);
        var scopeProjectId = NormalizeProjectId(projectId);

        var entities = await context.ChatAttachments
            .AsNoTracking()
            .Where(a =>
                a.ChatSessionId == sessionId &&
                a.ChatSession.OwnerId == effectiveOwnerId &&
                (scopeProjectId == null
                    ? a.ChatSession.ProjectId == null
                    : a.ChatSession.ProjectId == scopeProjectId))
            .OrderBy(a => a.UploadedAt)
            .ToListAsync();

        return entities.Select(ToAttachmentDto).ToList();
    }

    public async Task<IReadOnlyList<ChatAttachmentRecord>> GetAttachmentRecordsBySessionIdAsync(
        string projectId,
        string sessionId,
        string? ownerId = null)
    {
        var effectiveOwnerId = await ResolveOwnerIdAsync(ownerId);
        var scopeProjectId = NormalizeProjectId(projectId);

        var entities = await context.ChatAttachments
            .AsNoTracking()
            .Where(a =>
                a.ChatSessionId == sessionId &&
                a.ChatSession.OwnerId == effectiveOwnerId &&
                (scopeProjectId == null
                    ? a.ChatSession.ProjectId == null
                    : a.ChatSession.ProjectId == scopeProjectId))
            .OrderBy(a => a.UploadedAt)
            .ToListAsync();

        return entities.Select(ToAttachmentRecord).ToList();
    }

    public async Task<ChatAttachmentRecord?> GetAttachmentRecordAsync(string attachmentId, string? ownerId = null)
    {
        var effectiveOwnerId = await ResolveOwnerIdAsync(ownerId);

        var entity = await context.ChatAttachments
            .AsNoTracking()
            .FirstOrDefaultAsync(a =>
                a.Id == attachmentId &&
                a.ChatSession.OwnerId == effectiveOwnerId);

        return entity is null ? null : ToAttachmentRecord(entity);
    }

    public Task<string?> GetAttachmentContentAsync(string attachmentId)
        => GetAttachmentContentAsync(string.Empty, attachmentId);

    public async Task<string?> GetAttachmentContentAsync(string projectId, string attachmentId, string? ownerId = null)
    {
        var effectiveOwnerId = await ResolveOwnerIdAsync(ownerId);
        var scopeProjectId = NormalizeProjectId(projectId);

        var entity = await context.ChatAttachments
            .AsNoTracking()
            .FirstOrDefaultAsync(a =>
                a.Id == attachmentId &&
                a.ChatSession.OwnerId == effectiveOwnerId &&
                (scopeProjectId == null
                    ? a.ChatSession.ProjectId == null
                    : a.ChatSession.ProjectId == scopeProjectId));

        return entity?.Content;
    }

    public async Task AssignPendingAttachmentsToMessageAsync(string projectId, string sessionId, string messageId)
    {
        var ownerId = await GetCurrentOwnerIdAsync();
        var scopeProjectId = NormalizeProjectId(projectId);

        var entities = await context.ChatAttachments
            .Where(a =>
                a.ChatSessionId == sessionId &&
                a.ChatMessageId == null &&
                a.ChatSession.OwnerId == ownerId &&
                (scopeProjectId == null
                    ? a.ChatSession.ProjectId == null
                    : a.ChatSession.ProjectId == scopeProjectId))
            .ToListAsync();

        if (entities.Count == 0)
            return;

        foreach (var entity in entities)
        {
            entity.ChatMessageId = messageId;
        }

        await context.SaveChangesAsync();
    }

    public Task<bool> DeleteAttachmentAsync(string attachmentId)
        => DeleteAttachmentAsync(string.Empty, string.Empty, attachmentId);

    public async Task<bool> DeleteAttachmentAsync(string projectId, string sessionId, string attachmentId)
    {
        var ownerId = await GetCurrentOwnerIdAsync();
        var scopeProjectId = NormalizeProjectId(projectId);

        var entity = await context.ChatAttachments
            .FirstOrDefaultAsync(a =>
                a.Id == attachmentId &&
                a.ChatSession.OwnerId == ownerId &&
                (scopeProjectId == null
                    ? a.ChatSession.ProjectId == null
                    : a.ChatSession.ProjectId == scopeProjectId) &&
                (string.IsNullOrWhiteSpace(sessionId) || a.ChatSessionId == sessionId));
        if (entity is null) return false;

        context.ChatAttachments.Remove(entity);
        await context.SaveChangesAsync();
        return true;
    }

    private IQueryable<ChatSession> ScopedSessions(string ownerId, string? scopeProjectId)
    {
        var query = context.ChatSessions.Where(s => s.OwnerId == ownerId);
        return scopeProjectId == null
            ? query.Where(s => s.ProjectId == null)
            : query.Where(s => s.ProjectId == scopeProjectId);
    }

    private static ChatSessionDto ToSessionDto(ChatSession entity)
        => new(
            entity.Id,
            entity.Title,
            entity.LastMessage,
            entity.Timestamp,
            entity.IsActive,
            entity.IsGenerating,
            entity.GenerationState,
            entity.GenerationStatus,
            entity.GenerationUpdatedAtUtc?.ToString("O"),
            DeserializeRecentActivity(entity.RecentActivityJson),
            entity.IsDynamicIterationEnabled,
            entity.DynamicIterationBranch,
            entity.DynamicIterationPolicyJson);

    private static ChatAttachmentDto ToAttachmentDto(ChatAttachment entity)
    {
        var contentType = NormalizeContentType(entity.ContentType);
        var contentUrl = BuildAttachmentContentUrl(entity.Id);
        var isImage = IsImageContentType(contentType);
        return new(
            entity.Id,
            entity.FileName,
            entity.ContentLength > 0 ? entity.ContentLength : entity.Content.Length,
            entity.UploadedAt,
            contentType,
            contentUrl,
            BuildAttachmentMarkdownReference(entity.FileName, contentUrl, isImage),
            isImage);
    }

    private static ChatAttachmentRecord ToAttachmentRecord(ChatAttachment entity)
        => new(
            entity.Id,
            entity.FileName,
            entity.ContentLength > 0 ? entity.ContentLength : entity.Content.Length,
            entity.UploadedAt,
            NormalizeContentType(entity.ContentType),
            entity.Content,
            entity.StoragePath,
            entity.ChatSessionId,
            entity.ChatMessageId);

    private static ChatSessionActivityDto[] DeserializeRecentActivity(string? recentActivityJson)
    {
        if (string.IsNullOrWhiteSpace(recentActivityJson))
            return [];

        try
        {
            var activities = JsonSerializer.Deserialize<ChatSessionActivityDto[]>(recentActivityJson, RecentActivityJsonOptions) ?? [];
            return activities
                .Where(activity => activity is not null)
                .Select((activity, index) => NormalizeRecentActivity(activity, index))
                .ToArray();
        }
        catch
        {
            return [];
        }
    }

    private static string SerializeRecentActivity(ChatSessionActivityDto[] recentActivity)
        => JsonSerializer.Serialize(recentActivity, RecentActivityJsonOptions);

    private static ChatSessionActivityDto[] AppendRecentActivity(
        ChatSessionActivityDto[] existing,
        ChatSessionActivityDto activity)
    {
        activity = NormalizeRecentActivity(activity, existing.Length);
        if (existing.Length > 0)
        {
            var last = existing[^1];
            if (string.Equals(last.Kind, activity.Kind, StringComparison.Ordinal)
                && string.Equals(last.Message, activity.Message, StringComparison.Ordinal)
                && string.Equals(last.ToolName, activity.ToolName, StringComparison.Ordinal)
                && last.Succeeded == activity.Succeeded)
            {
                return existing;
            }
        }

        var next = existing.Concat([activity]).TakeLast(MaxRecentActivityEntries).ToArray();
        return next;
    }

    private static ChatSessionActivityDto NormalizeRecentActivity(ChatSessionActivityDto activity, int index)
    {
        var kind = activity.Kind is "tool" or "error" ? activity.Kind : "status";
        var message = string.IsNullOrWhiteSpace(activity.Message) ? "Session update" : activity.Message;
        var timestampUtc = activity.TimestampUtc ?? string.Empty;
        var id = string.IsNullOrWhiteSpace(activity.Id)
            ? $"{kind}-{timestampUtc}-{index}"
            : activity.Id;
        var toolName = string.IsNullOrWhiteSpace(activity.ToolName) ? null : activity.ToolName;

        return new ChatSessionActivityDto(
            id,
            kind,
            message,
            timestampUtc,
            toolName,
            activity.Succeeded);
    }

    private static string? NormalizeProjectId(string projectId)
        => IsGlobalScope(projectId) ? null : projectId.Trim();

    private static string BuildAttachmentContentUrl(string attachmentId)
        => $"/api/chat/attachments/{Uri.EscapeDataString(attachmentId)}/content";

    private static string BuildAttachmentMarkdownReference(string fileName, string contentUrl, bool isImage)
    {
        var safeLabel = fileName
            .Replace("[", "\\[", StringComparison.Ordinal)
            .Replace("]", "\\]", StringComparison.Ordinal);

        return isImage
            ? $"![{safeLabel}]({contentUrl})"
            : $"[{safeLabel}]({contentUrl})";
    }

    private static bool IsImageContentType(string contentType)
        => contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase);

    private static string NormalizeContentType(string? contentType)
        => string.IsNullOrWhiteSpace(contentType)
            ? "application/octet-stream"
            : contentType.Trim();

    private static string? NormalizeNullableString(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static bool IsGlobalScope(string projectId)
        => string.IsNullOrWhiteSpace(projectId);

    private async Task<string> ResolveOwnerIdAsync(string? ownerId)
    {
        if (!string.IsNullOrWhiteSpace(ownerId))
            return ownerId;

        return await GetCurrentOwnerIdAsync();
    }

    private async Task<string> GetCurrentOwnerIdAsync()
        => (await authService.GetCurrentUserIdAsync()).ToString();
}
