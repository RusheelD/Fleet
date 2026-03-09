using Fleet.Server.Auth;
using Fleet.Server.Data;
using Fleet.Server.Data.Entities;
using Fleet.Server.Models;
using Microsoft.EntityFrameworkCore;

namespace Fleet.Server.Copilot;

public class ChatSessionRepository(FleetDbContext context, IAuthService authService) : IChatSessionRepository
{
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
            .Select(s => new ChatSessionDto(s.Id, s.Title, s.LastMessage, s.Timestamp, s.IsActive, s.IsGenerating))
            .ToList();
    }

    public async Task<IReadOnlyList<ChatMessageDto>> GetMessagesBySessionIdAsync(string projectId, string sessionId)
    {
        var ownerId = await GetCurrentOwnerIdAsync();
        var scopeProjectId = NormalizeProjectId(projectId);

        var entities = await context.ChatMessages
            .AsNoTracking()
            .Where(m =>
                m.ChatSessionId == sessionId &&
                m.ChatSession.OwnerId == ownerId &&
                (scopeProjectId == null
                    ? m.ChatSession.ProjectId == null
                    : m.ChatSession.ProjectId == scopeProjectId))
            .ToListAsync();

        return entities.Select(m => new ChatMessageDto(m.Id, m.Role, m.Content, m.Timestamp)).ToList();
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

        return new ChatSessionDto(entity.Id, entity.Title, entity.LastMessage, entity.Timestamp, entity.IsActive, entity.IsGenerating);
    }

    public Task<bool> RenameSessionAsync(string sessionId, string title)
        => RenameSessionAsync(string.Empty, sessionId, title);

    public async Task<bool> RenameSessionAsync(string projectId, string sessionId, string title)
    {
        var ownerId = await GetCurrentOwnerIdAsync();
        var scopeProjectId = NormalizeProjectId(projectId);

        var entity = await ScopedSessions(ownerId, scopeProjectId)
            .FirstOrDefaultAsync(s => s.Id == sessionId);
        if (entity is null) return false;

        entity.Title = title;
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

    public async Task<ChatMessageDto> AddMessageAsync(string projectId, string sessionId, string role, string content)
    {
        var ownerId = await GetCurrentOwnerIdAsync();
        var scopeProjectId = NormalizeProjectId(projectId);

        var session = await ScopedSessions(ownerId, scopeProjectId)
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
        var ownerId = await GetCurrentOwnerIdAsync();
        var scopeProjectId = NormalizeProjectId(projectId);

        var session = await ScopedSessions(ownerId, scopeProjectId)
            .FirstOrDefaultAsync(s => s.Id == sessionId);
        if (session is null)
            return;

        session.IsGenerating = isGenerating;
        await context.SaveChangesAsync();
    }

    public Task<ChatAttachmentDto> AddAttachmentAsync(string sessionId, string fileName, string content)
        => AddAttachmentAsync(string.Empty, sessionId, fileName, content);

    public async Task<ChatAttachmentDto> AddAttachmentAsync(string projectId, string sessionId, string fileName, string content)
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
            Id = Guid.NewGuid().ToString(),
            FileName = fileName,
            Content = content,
            UploadedAt = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            ChatSessionId = sessionId
        };

        context.ChatAttachments.Add(entity);
        await context.SaveChangesAsync();

        return new ChatAttachmentDto(entity.Id, entity.FileName, entity.Content.Length, entity.UploadedAt);
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
                a.ChatSession.OwnerId == ownerId &&
                (scopeProjectId == null
                    ? a.ChatSession.ProjectId == null
                    : a.ChatSession.ProjectId == scopeProjectId))
            .ToListAsync();

        return entities.Select(a => new ChatAttachmentDto(a.Id, a.FileName, a.Content.Length, a.UploadedAt)).ToList();
    }

    public Task<string?> GetAttachmentContentAsync(string attachmentId)
        => GetAttachmentContentAsync(string.Empty, attachmentId);

    public async Task<string?> GetAttachmentContentAsync(string projectId, string attachmentId)
    {
        var ownerId = await GetCurrentOwnerIdAsync();
        var scopeProjectId = NormalizeProjectId(projectId);

        var entity = await context.ChatAttachments
            .AsNoTracking()
            .FirstOrDefaultAsync(a =>
                a.Id == attachmentId &&
                a.ChatSession.OwnerId == ownerId &&
                (scopeProjectId == null
                    ? a.ChatSession.ProjectId == null
                    : a.ChatSession.ProjectId == scopeProjectId));

        return entity?.Content;
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

    private static string? NormalizeProjectId(string projectId)
        => IsGlobalScope(projectId) ? null : projectId.Trim();

    private static bool IsGlobalScope(string projectId)
        => string.IsNullOrWhiteSpace(projectId);

    private async Task<string> GetCurrentOwnerIdAsync()
        => (await authService.GetCurrentUserIdAsync()).ToString();
}
