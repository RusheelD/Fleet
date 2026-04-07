namespace Fleet.Server.Data.Entities;

public class WorkItemAttachment
{
    public string Id { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = "application/octet-stream";
    public int ContentLength { get; set; }
    public string? StoragePath { get; set; }
    public string UploadedAt { get; set; } = string.Empty;

    public int WorkItemId { get; set; }
    public WorkItem WorkItem { get; set; } = null!;
}
