namespace Fleet.Server.Models;

public record WorkItemAttachmentDto(
    string Id,
    string FileName,
    int ContentLength,
    string UploadedAt,
    string ContentType,
    string ContentUrl,
    string MarkdownReference,
    bool IsImage
);
