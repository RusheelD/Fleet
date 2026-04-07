namespace Fleet.Server.Models;

public record ChatAttachmentContentResult(
    string FileName,
    string ContentType,
    byte[] Content
);
