using Fleet.Server.Copilot;
using Fleet.Server.Data;
using Fleet.Server.Data.Entities;
using Fleet.Server.Logging;
using Fleet.Server.Models;
using Microsoft.EntityFrameworkCore;

namespace Fleet.Server.WorkItems;

public class WorkItemAttachmentService(
    FleetDbContext context,
    IChatAttachmentStorage attachmentStorage,
    ILogger<WorkItemAttachmentService> logger) : IWorkItemAttachmentService
{
    public async Task<IReadOnlyList<WorkItemAttachmentDto>> GetByWorkItemNumberAsync(
        string projectId,
        int workItemNumber,
        CancellationToken cancellationToken = default)
    {
        var workItemId = await context.WorkItems
            .AsNoTracking()
            .Where(w => w.ProjectId == projectId && w.WorkItemNumber == workItemNumber)
            .Select(w => (int?)w.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (workItemId is null)
            return [];

        var attachments = await context.Set<WorkItemAttachment>()
            .AsNoTracking()
            .Where(a => a.WorkItemId == workItemId.Value)
            .OrderBy(a => a.FileName)
            .ThenBy(a => a.UploadedAt)
            .ToListAsync(cancellationToken);

        return attachments
            .Select(attachment => MapToDto(projectId, workItemNumber, attachment))
            .ToArray();
    }

    public async Task<WorkItemAttachmentDto?> UploadAsync(
        string projectId,
        int workItemNumber,
        string fileName,
        string? contentType,
        byte[] content,
        CancellationToken cancellationToken = default)
    {
        var workItem = await context.WorkItems
            .FirstOrDefaultAsync(w => w.ProjectId == projectId && w.WorkItemNumber == workItemNumber, cancellationToken);

        if (workItem is null)
            return null;

        var attachmentId = Guid.NewGuid().ToString("N");
        var storedAttachment = await attachmentStorage.SaveAsync(
            attachmentId,
            fileName,
            contentType,
            content,
            cancellationToken);
        var uploadedAt = DateTime.UtcNow.ToString("o");

        var attachment = new WorkItemAttachment
        {
            Id = attachmentId,
            FileName = string.IsNullOrWhiteSpace(fileName) ? "attachment" : fileName.Trim(),
            ContentType = storedAttachment.ContentType,
            ContentLength = storedAttachment.ContentLength,
            StoragePath = storedAttachment.StoragePath,
            UploadedAt = uploadedAt,
            WorkItemId = workItem.Id,
        };

        context.Set<WorkItemAttachment>().Add(attachment);
        await context.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Stored work item attachment {AttachmentId} for project {ProjectId} work item #{WorkItemNumber}",
            attachmentId,
            projectId.SanitizeForLogging(),
            workItemNumber);

        return MapToDto(projectId, workItemNumber, attachment);
    }

    public async Task<ChatAttachmentContentResult?> GetContentAsync(
        string projectId,
        int workItemNumber,
        string attachmentId,
        CancellationToken cancellationToken = default)
    {
        var attachment = await context.Set<WorkItemAttachment>()
            .AsNoTracking()
            .Where(a => a.Id == attachmentId &&
                a.WorkItem.ProjectId == projectId &&
                a.WorkItem.WorkItemNumber == workItemNumber)
            .Select(a => new
            {
                a.FileName,
                a.ContentType,
                a.StoragePath,
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (attachment is null)
            return null;

        var content = string.IsNullOrWhiteSpace(attachment.StoragePath)
            ? null
            : await attachmentStorage.ReadAsync(attachment.StoragePath, cancellationToken);

        if (content is null)
            return null;

        return new ChatAttachmentContentResult(
            attachment.FileName,
            string.IsNullOrWhiteSpace(attachment.ContentType) ? "application/octet-stream" : attachment.ContentType,
            content);
    }

    public async Task<bool> DeleteAsync(
        string projectId,
        int workItemNumber,
        string attachmentId,
        CancellationToken cancellationToken = default)
    {
        var attachment = await context.Set<WorkItemAttachment>()
            .Include(a => a.WorkItem)
            .FirstOrDefaultAsync(
                a => a.Id == attachmentId &&
                    a.WorkItem.ProjectId == projectId &&
                    a.WorkItem.WorkItemNumber == workItemNumber,
                cancellationToken);

        if (attachment is null)
            return false;

        context.Set<WorkItemAttachment>().Remove(attachment);
        await context.SaveChangesAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(attachment.StoragePath))
            await attachmentStorage.DeleteAsync(attachment.StoragePath, cancellationToken);

        return true;
    }

    public async Task DeleteAllAsync(
        string projectId,
        int workItemNumber,
        CancellationToken cancellationToken = default)
    {
        var attachments = await context.Set<WorkItemAttachment>()
            .Include(a => a.WorkItem)
            .Where(a => a.WorkItem.ProjectId == projectId && a.WorkItem.WorkItemNumber == workItemNumber)
            .ToListAsync(cancellationToken);

        if (attachments.Count == 0)
            return;

        context.Set<WorkItemAttachment>().RemoveRange(attachments);
        await context.SaveChangesAsync(cancellationToken);

        foreach (var attachment in attachments)
        {
            if (string.IsNullOrWhiteSpace(attachment.StoragePath))
                continue;

            await attachmentStorage.DeleteAsync(attachment.StoragePath, cancellationToken);
        }
    }

    public async Task<WorkItemAttachmentRecord?> GetAttachmentRecordAsync(
        string projectId,
        string attachmentId,
        CancellationToken cancellationToken = default)
    {
        return await context.Set<WorkItemAttachment>()
            .AsNoTracking()
            .Where(a => a.Id == attachmentId && a.WorkItem.ProjectId == projectId)
            .Select(a => new WorkItemAttachmentRecord(
                a.Id,
                a.FileName,
                a.ContentType,
                a.ContentLength,
                a.StoragePath,
                a.UploadedAt))
            .FirstOrDefaultAsync(cancellationToken);
    }

    private static WorkItemAttachmentDto MapToDto(string projectId, int workItemNumber, WorkItemAttachment attachment)
    {
        var contentUrl = $"/api/projects/{projectId}/work-items/{workItemNumber}/attachments/{attachment.Id}/content";
        var isImage = IsImageContentType(attachment.ContentType);
        var markdownReference = isImage
            ? $"![{attachment.FileName}]({contentUrl})"
            : $"[{attachment.FileName}]({contentUrl})";

        return new WorkItemAttachmentDto(
            attachment.Id,
            attachment.FileName,
            attachment.ContentLength,
            attachment.UploadedAt,
            attachment.ContentType,
            contentUrl,
            markdownReference,
            isImage);
    }

    private static bool IsImageContentType(string? contentType)
        => !string.IsNullOrWhiteSpace(contentType) &&
           contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase);
}
