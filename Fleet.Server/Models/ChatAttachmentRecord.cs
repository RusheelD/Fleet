namespace Fleet.Server.Models;

public record ChatAttachmentRecord(
    string Id,
    string FileName,
    int ContentLength,
    string UploadedAt,
    string ContentType,
    string Content,
    string? StoragePath,
    string ChatSessionId,
    string? ChatMessageId
);
