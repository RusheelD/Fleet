using Fleet.Server.Models;

namespace Fleet.Server.Copilot;

public interface IChatSessionRepository
{
    Task<IReadOnlyList<ChatSessionDto>> GetSessionsByProjectIdAsync(string projectId);
    Task<IReadOnlyList<ChatMessageDto>> GetMessagesBySessionIdAsync(string projectId, string sessionId);
    Task<string[]> GetSuggestionsAsync(string projectId);
    Task<ChatSessionDto> CreateSessionAsync(string projectId, string title);
    Task<bool> RenameSessionAsync(string sessionId, string title);
    Task<bool> RenameSessionAsync(string projectId, string sessionId, string title);
    Task<bool> DeleteSessionAsync(string sessionId);
    Task<bool> DeleteSessionAsync(string projectId, string sessionId);
    Task<ChatMessageDto> AddMessageAsync(string projectId, string sessionId, string role, string content);
    Task SetSessionGeneratingAsync(string sessionId, bool isGenerating);
    Task SetSessionGeneratingAsync(string projectId, string sessionId, bool isGenerating);
    Task UpdateSessionGenerationStateAsync(string projectId, string sessionId, bool isGenerating, string generationState, string? generationStatus, ChatSessionActivityDto? activity = null);
    Task<int> MarkStaleGeneratingSessionsAsync(string projectId, DateTime cutoffUtc, string generationState, string generationStatus);
    Task AppendSessionActivityAsync(string projectId, string sessionId, ChatSessionActivityDto activity);

    // Attachments
    Task<ChatAttachmentDto> AddAttachmentAsync(string sessionId, string fileName, string content);
    Task<ChatAttachmentDto> AddAttachmentAsync(string projectId, string sessionId, string fileName, string content);
    Task<IReadOnlyList<ChatAttachmentDto>> GetAttachmentsBySessionIdAsync(string sessionId);
    Task<IReadOnlyList<ChatAttachmentDto>> GetAttachmentsBySessionIdAsync(string projectId, string sessionId);
    Task<IReadOnlyList<ChatAttachmentDto>> GetAllAttachmentsBySessionIdAsync(string projectId, string sessionId);
    Task<string?> GetAttachmentContentAsync(string attachmentId);
    Task<string?> GetAttachmentContentAsync(string projectId, string attachmentId);
    Task AssignPendingAttachmentsToMessageAsync(string projectId, string sessionId, string messageId);
    Task<bool> DeleteAttachmentAsync(string attachmentId);
    Task<bool> DeleteAttachmentAsync(string projectId, string sessionId, string attachmentId);
}
