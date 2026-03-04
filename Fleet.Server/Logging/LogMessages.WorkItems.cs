using Microsoft.Extensions.Logging;

namespace Fleet.Server.Logging;

public static partial class LogMessages
{
    [LoggerMessage(EventId = 3000, Level = LogLevel.Information, Message = "Retrieving work items. projectId={projectId}")]
    public static partial void WorkItemsRetrieving(this ILogger logger, string projectId);

    [LoggerMessage(EventId = 3001, Level = LogLevel.Information, Message = "Retrieving work item. projectId={projectId} workItemId={workItemId}")]
    public static partial void WorkItemsRetrievingById(this ILogger logger, string projectId, int workItemId);

    [LoggerMessage(EventId = 3002, Level = LogLevel.Information, Message = "Creating work item. projectId={projectId} title={title}")]
    public static partial void WorkItemsCreating(this ILogger logger, string projectId, string title);

    [LoggerMessage(EventId = 3003, Level = LogLevel.Information, Message = "Updating work item. projectId={projectId} workItemId={workItemId}")]
    public static partial void WorkItemsUpdating(this ILogger logger, string projectId, int workItemId);

    [LoggerMessage(EventId = 3004, Level = LogLevel.Information, Message = "Deleting work item. projectId={projectId} workItemId={workItemId}")]
    public static partial void WorkItemsDeleting(this ILogger logger, string projectId, int workItemId);
}
