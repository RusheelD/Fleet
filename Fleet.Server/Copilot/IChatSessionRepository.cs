using Fleet.Server.Models;

namespace Fleet.Server.Copilot;

public interface IChatSessionRepository
{
    Task<IReadOnlyList<ChatSessionDto>> GetSessionsByProjectIdAsync(string projectId);
    Task<IReadOnlyList<ChatMessageDto>> GetMessagesBySessionIdAsync(string projectId, string sessionId);
    Task<string[]> GetSuggestionsAsync(string projectId);
    Task<ChatSessionDto> CreateSessionAsync(string projectId, string title);
    Task<ChatMessageDto> AddMessageAsync(string projectId, string sessionId, string role, string content);

    // Attachments
    Task<ChatAttachmentDto> AddAttachmentAsync(string sessionId, string fileName, string content);
    Task<IReadOnlyList<ChatAttachmentDto>> GetAttachmentsBySessionIdAsync(string sessionId);
    Task<string?> GetAttachmentContentAsync(string attachmentId);
    Task<bool> DeleteAttachmentAsync(string attachmentId);
}
