namespace Fleet.Server.Models;

/// <summary>Metadata for an uploaded document attached to a chat session.</summary>
public record ChatAttachmentDto(
    string Id,
    string FileName,
    int ContentLength,
    string UploadedAt,
    string ContentType,
    string ContentUrl,
    string MarkdownReference,
    bool IsImage
);
