using Fleet.Server.Data;
using Fleet.Server.Data.Entities;
using Fleet.Server.Models;
using Microsoft.EntityFrameworkCore;

namespace Fleet.Server.Copilot;

public class ChatSessionRepository(FleetDbContext context) : IChatSessionRepository
{
    private static readonly string[] DefaultSuggestions =
    [
        "Assign agents to auth work items",
        "Generate spec document",
        "Add more work items",
        "Show repo structure",
    ];

    public async Task<IReadOnlyList<ChatSessionDto>> GetSessionsByProjectIdAsync(string projectId)
    {
        var entities = await context.ChatSessions
            .AsNoTracking()
            .Where(s => s.ProjectId == projectId)
            .ToListAsync();

        return entities.Select(s => new ChatSessionDto(s.Id, s.Title, s.LastMessage, s.Timestamp, s.IsActive)).ToList();
    }

    public async Task<IReadOnlyList<ChatMessageDto>> GetMessagesBySessionIdAsync(string projectId, string sessionId)
    {
        var entities = await context.ChatMessages
            .AsNoTracking()
            .Where(m => m.ChatSessionId == sessionId)
            .ToListAsync();

        return entities.Select(m => new ChatMessageDto(m.Id, m.Role, m.Content, m.Timestamp)).ToList();
    }

    public Task<string[]> GetSuggestionsAsync(string projectId)
        => Task.FromResult(DefaultSuggestions);

    public async Task<ChatSessionDto> CreateSessionAsync(string projectId, string title)
    {
        var entity = new ChatSession
        {
            Id = Guid.NewGuid().ToString(),
            Title = title,
            LastMessage = "",
            Timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            IsActive = true,
            ProjectId = projectId
        };

        // Deactivate other sessions for this project
        var activeSessions = await context.ChatSessions
            .Where(s => s.ProjectId == projectId && s.IsActive)
            .ToListAsync();
        foreach (var s in activeSessions)
        {
            s.IsActive = false;
        }

        context.ChatSessions.Add(entity);
        await context.SaveChangesAsync();

        return new ChatSessionDto(entity.Id, entity.Title, entity.LastMessage, entity.Timestamp, entity.IsActive);
    }

    public async Task<bool> DeleteSessionAsync(string sessionId)
    {
        var entity = await context.ChatSessions.FindAsync(sessionId);
        if (entity is null) return false;

        context.ChatSessions.Remove(entity);
        await context.SaveChangesAsync();
        return true;
    }

    public async Task<ChatMessageDto> AddMessageAsync(string projectId, string sessionId, string role, string content)
    {
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

        // Update session's last message
        var session = await context.ChatSessions.FindAsync(sessionId);
        if (session is not null)
        {
            session.LastMessage = content.Length > 100 ? content[..100] + "..." : content;
            session.Timestamp = now;
        }

        await context.SaveChangesAsync();

        return new ChatMessageDto(entity.Id, entity.Role, entity.Content, entity.Timestamp);
    }

    // ── Attachments ──────────────────────────────────────────

    public async Task<ChatAttachmentDto> AddAttachmentAsync(string sessionId, string fileName, string content)
    {
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

    public async Task<IReadOnlyList<ChatAttachmentDto>> GetAttachmentsBySessionIdAsync(string sessionId)
    {
        var entities = await context.ChatAttachments
            .AsNoTracking()
            .Where(a => a.ChatSessionId == sessionId)
            .ToListAsync();

        return entities.Select(a => new ChatAttachmentDto(a.Id, a.FileName, a.Content.Length, a.UploadedAt)).ToList();
    }

    public async Task<string?> GetAttachmentContentAsync(string attachmentId)
    {
        var entity = await context.ChatAttachments
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == attachmentId);

        return entity?.Content;
    }

    public async Task<bool> DeleteAttachmentAsync(string attachmentId)
    {
        var entity = await context.ChatAttachments.FindAsync(attachmentId);
        if (entity is null) return false;

        context.ChatAttachments.Remove(entity);
        await context.SaveChangesAsync();
        return true;
    }
}
