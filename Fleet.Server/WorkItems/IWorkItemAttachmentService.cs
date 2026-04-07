using Fleet.Server.Models;

namespace Fleet.Server.WorkItems;

public interface IWorkItemAttachmentService
{
    Task<IReadOnlyList<WorkItemAttachmentDto>> GetByWorkItemNumberAsync(string projectId, int workItemNumber, CancellationToken cancellationToken = default);
    Task<WorkItemAttachmentDto?> UploadAsync(string projectId, int workItemNumber, string fileName, string? contentType, byte[] content, CancellationToken cancellationToken = default);
    Task<ChatAttachmentContentResult?> GetContentAsync(string projectId, int workItemNumber, string attachmentId, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(string projectId, int workItemNumber, string attachmentId, CancellationToken cancellationToken = default);
    Task DeleteAllAsync(string projectId, int workItemNumber, CancellationToken cancellationToken = default);
    Task<WorkItemAttachmentRecord?> GetAttachmentRecordAsync(string projectId, string attachmentId, CancellationToken cancellationToken = default);
}
