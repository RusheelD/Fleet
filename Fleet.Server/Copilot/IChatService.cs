using Fleet.Server.Models;

namespace Fleet.Server.Copilot;

public interface IChatService
{
    Task<ChatDataDto> GetChatDataAsync(string projectId);
    Task<IReadOnlyList<ChatMessageDto>> GetMessagesAsync(string projectId, string sessionId);
    Task<ChatSessionDto> CreateSessionAsync(string projectId, string title);
    Task<bool> RenameSessionAsync(string projectId, string sessionId, string title);
    Task<bool> DeleteSessionAsync(string projectId, string sessionId);
    Task<SendMessageResponseDto> SendMessageAsync(
        string projectId,
        string sessionId,
        string content,
        bool generateWorkItems = false,
        CancellationToken cancellationToken = default);

    // Attachments
    Task<ChatAttachmentDto> UploadAttachmentAsync(string projectId, string sessionId, string fileName, string content);
    Task<IReadOnlyList<ChatAttachmentDto>> GetAttachmentsAsync(string sessionId);
    Task<IReadOnlyList<ChatAttachmentDto>> GetAttachmentsAsync(string projectId, string sessionId);
    Task<bool> DeleteAttachmentAsync(string attachmentId);
    Task<bool> DeleteAttachmentAsync(string projectId, string sessionId, string attachmentId);
}
