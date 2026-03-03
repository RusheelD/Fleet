using Fleet.Server.Models;

namespace Fleet.Server.Copilot;

public interface IChatService
{
    Task<ChatDataDto> GetChatDataAsync(string projectId);
    Task<IReadOnlyList<ChatMessageDto>> GetMessagesAsync(string projectId, string sessionId);
    Task<ChatSessionDto> CreateSessionAsync(string projectId, string title);
    Task<SendMessageResponseDto> SendMessageAsync(string projectId, string sessionId, string content);

    // Attachments
    Task<ChatAttachmentDto> UploadAttachmentAsync(string projectId, string sessionId, string fileName, string content);
    Task<IReadOnlyList<ChatAttachmentDto>> GetAttachmentsAsync(string sessionId);
    Task<bool> DeleteAttachmentAsync(string attachmentId);
}
