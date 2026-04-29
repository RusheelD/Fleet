using Fleet.Server.Models;

namespace Fleet.Server.Copilot;

public interface IChatService
{
    Task<ChatDataDto> GetChatDataAsync(string projectId);
    Task<IReadOnlyList<ChatMessageDto>> GetMessagesAsync(string projectId, string sessionId);
    Task<ChatSessionDto> CreateSessionAsync(string projectId, string title);
    Task<bool> RenameSessionAsync(string projectId, string sessionId, string title);
    Task<bool> UpdateSessionDynamicIterationAsync(string projectId, string sessionId, bool isEnabled, string? branch, string? policyJson);
    Task<bool> DeleteSessionAsync(string projectId, string sessionId);
    Task<bool> CancelGenerationAsync(string projectId, string sessionId);
    Task<SendMessageResponseDto> SendMessageAsync(
        string projectId,
        string sessionId,
        string content,
        ChatSendOptions? options = null,
        CancellationToken cancellationToken = default);

    // Attachments
    Task<ChatAttachmentDto> UploadAttachmentAsync(
        string projectId,
        string sessionId,
        string fileName,
        string? contentType,
        byte[] content,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ChatAttachmentDto>> GetAttachmentsAsync(string sessionId);
    Task<IReadOnlyList<ChatAttachmentDto>> GetAttachmentsAsync(string projectId, string sessionId);
    Task<ChatAttachmentContentResult?> GetAttachmentContentAsync(string attachmentId, CancellationToken cancellationToken = default);
    Task<bool> DeleteAttachmentAsync(string attachmentId);
    Task<bool> DeleteAttachmentAsync(string projectId, string sessionId, string attachmentId);
}
