namespace Fleet.Server.Copilot;

public interface IChatAttachmentStorage
{
    Task<StoredChatAttachment> SaveAsync(
        string attachmentId,
        string fileName,
        string? contentType,
        byte[] content,
        CancellationToken cancellationToken = default);

    Task<byte[]?> ReadAsync(string storagePath, CancellationToken cancellationToken = default);

    Task DeleteAsync(string? storagePath, CancellationToken cancellationToken = default);
}

public sealed record StoredChatAttachment(
    string StoragePath,
    int ContentLength,
    string ContentType,
    string ExtractedText
);
