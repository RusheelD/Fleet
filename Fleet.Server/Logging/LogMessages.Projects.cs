using Microsoft.Extensions.Logging;

namespace Fleet.Server.Logging;

public static partial class LogMessages
{
    [LoggerMessage(EventId = 2000, Level = LogLevel.Information, Message = "Retrieving all projects")]
    public static partial void ProjectsRetrievingAll(this ILogger logger);

    [LoggerMessage(EventId = 2001, Level = LogLevel.Information, Message = "Checking slug availability. name={name}")]
    public static partial void ProjectsCheckingSlug(this ILogger logger, string name);

    [LoggerMessage(EventId = 2002, Level = LogLevel.Warning, Message = "Generated slug was empty. name={name}")]
    public static partial void ProjectsSlugEmpty(this ILogger logger, string name);

    [LoggerMessage(EventId = 2003, Level = LogLevel.Information, Message = "Slug availability checked. slug={slug} available={available}")]
    public static partial void ProjectsSlugAvailability(this ILogger logger, string slug, bool available);

    [LoggerMessage(EventId = 2004, Level = LogLevel.Information, Message = "Creating project. title={title}")]
    public static partial void ProjectsCreating(this ILogger logger, string title);

    [LoggerMessage(EventId = 2005, Level = LogLevel.Information, Message = "Updating project. projectId={projectId}")]
    public static partial void ProjectsUpdating(this ILogger logger, string projectId);

    [LoggerMessage(EventId = 2006, Level = LogLevel.Information, Message = "Deleting project. projectId={projectId}")]
    public static partial void ProjectsDeleting(this ILogger logger, string projectId);

    [LoggerMessage(EventId = 2007, Level = LogLevel.Information, Message = "Retrieving dashboard by slug. slug={slug}")]
    public static partial void ProjectsDashboardBySlug(this ILogger logger, string slug);

    [LoggerMessage(EventId = 2008, Level = LogLevel.Warning, Message = "Project not found by slug. slug={slug}")]
    public static partial void ProjectsNotFoundBySlug(this ILogger logger, string slug);

    [LoggerMessage(EventId = 2009, Level = LogLevel.Information, Message = "Retrieving dashboard by project ID. projectId={projectId}")]
    public static partial void ProjectsDashboardById(this ILogger logger, string projectId);

    [LoggerMessage(EventId = 2010, Level = LogLevel.Warning, Message = "Project not found. projectId={projectId}")]
    public static partial void ProjectsNotFoundById(this ILogger logger, string projectId);

    [LoggerMessage(EventId = 2011, Level = LogLevel.Warning, Message = "Failed to fetch GitHub stats. repo={repo}")]
    public static partial void ProjectsGitHubStatsFailed(this ILogger logger, Exception exception, string repo);
}
