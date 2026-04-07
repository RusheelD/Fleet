namespace Fleet.Server.Models;

public record WorkItemAttachmentRecord(
    string Id,
    string FileName,
    string ContentType,
    int ContentLength,
    string? StoragePath,
    string UploadedAt
);
