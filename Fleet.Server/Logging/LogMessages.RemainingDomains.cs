using Microsoft.Extensions.Logging;

namespace Fleet.Server.Logging;

public static partial class LogMessages
{
    [LoggerMessage(EventId = 5000, Level = LogLevel.Information, Message = "Retrieving work item levels. projectId={projectId}")]
    public static partial void WorkItemLevelsRetrieving(this ILogger logger, string projectId);

    [LoggerMessage(EventId = 5001, Level = LogLevel.Information, Message = "Retrieving work item level. projectId={projectId} levelId={levelId}")]
    public static partial void WorkItemLevelsRetrievingById(this ILogger logger, string projectId, int levelId);

    [LoggerMessage(EventId = 5002, Level = LogLevel.Information, Message = "Creating work item level. projectId={projectId} name={name}")]
    public static partial void WorkItemLevelsCreating(this ILogger logger, string projectId, string name);

    [LoggerMessage(EventId = 5003, Level = LogLevel.Information, Message = "Updating work item level. projectId={projectId} levelId={levelId}")]
    public static partial void WorkItemLevelsUpdating(this ILogger logger, string projectId, int levelId);

    [LoggerMessage(EventId = 5004, Level = LogLevel.Information, Message = "Deleting work item level. projectId={projectId} levelId={levelId}")]
    public static partial void WorkItemLevelsDeleting(this ILogger logger, string projectId, int levelId);

    [LoggerMessage(EventId = 5005, Level = LogLevel.Information, Message = "Ensuring default work item levels. projectId={projectId}")]
    public static partial void WorkItemLevelsEnsuringDefaults(this ILogger logger, string projectId);

    [LoggerMessage(EventId = 6000, Level = LogLevel.Information, Message = "Retrieving user settings. userId={userId}")]
    public static partial void UsersSettingsRetrieving(this ILogger logger, int userId);

    [LoggerMessage(EventId = 6001, Level = LogLevel.Information, Message = "Updating user profile. userId={userId}")]
    public static partial void UsersProfileUpdating(this ILogger logger, int userId);

    [LoggerMessage(EventId = 6002, Level = LogLevel.Information, Message = "Updating user preferences. userId={userId}")]
    public static partial void UsersPreferencesUpdating(this ILogger logger, int userId);

    [LoggerMessage(EventId = 6003, Level = LogLevel.Information, Message = "Updated user preferences. userId={userId}")]
    public static partial void UsersPreferencesUpdated(this ILogger logger, int userId);

    [LoggerMessage(EventId = 7000, Level = LogLevel.Information, Message = "Retrieving subscription data")]
    public static partial void SubscriptionsRetrieving(this ILogger logger);

    [LoggerMessage(EventId = 8000, Level = LogLevel.Information, Message = "Search started. query={query} type={type}")]
    public static partial void SearchStarted(this ILogger logger, string query, string type);

    [LoggerMessage(EventId = 8001, Level = LogLevel.Information, Message = "Search completed. resultCount={resultCount}")]
    public static partial void SearchCompleted(this ILogger logger, int resultCount);

    [LoggerMessage(EventId = 10000, Level = LogLevel.Warning, Message = "No GitHub token available; returning empty stats. userId={userId}")]
    public static partial void GitHubNoToken(this ILogger logger, int userId);

    [LoggerMessage(EventId = 10001, Level = LogLevel.Warning, Message = "GitHub API call failed; returning empty stats. repo={repo}")]
    public static partial void GitHubApiFailed(this ILogger logger, Exception exception, string repo);

    [LoggerMessage(EventId = 11000, Level = LogLevel.Information, Message = "Database migration completed successfully")]
    public static partial void DataMigrationCompleted(this ILogger logger);

    [LoggerMessage(EventId = 11001, Level = LogLevel.Critical, Message = "Database migration failed")]
    public static partial void DataMigrationFailed(this ILogger logger, Exception exception);

    [LoggerMessage(EventId = 12000, Level = LogLevel.Debug, Message = "Resolved Entra user. oid={oid} userId={userId}")]
    public static partial void AuthResolvedUser(this ILogger logger, string oid, int userId);

    [LoggerMessage(EventId = 12001, Level = LogLevel.Information, Message = "Auto-provisioned local user. userId={userId} oid={oid} email={email}")]
    public static partial void AuthAutoProvisionedUser(this ILogger logger, int userId, string oid, string email);

    [LoggerMessage(EventId = 13000, Level = LogLevel.Information, Message = "Retrieving agent executions. projectId={projectId}")]
    public static partial void AgentsExecutionsRetrieving(this ILogger logger, string projectId);

    [LoggerMessage(EventId = 13001, Level = LogLevel.Information, Message = "Retrieving agent logs. projectId={projectId}")]
    public static partial void AgentsLogsRetrieving(this ILogger logger, string projectId);

    [LoggerMessage(EventId = 14000, Level = LogLevel.Warning, Message = "Model validation failed. actionName={actionName} traceId={traceId} errors={errors}")]
    public static partial void ActionValidationFailed(this ILogger logger, string actionName, string traceId, string errors);
}
