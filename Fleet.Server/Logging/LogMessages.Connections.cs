using Microsoft.Extensions.Logging;

namespace Fleet.Server.Logging;

public static partial class LogMessages
{
    [LoggerMessage(EventId = 4000, Level = LogLevel.Information, Message = "Link GitHub requested. userId={userId} redirectUri={redirectUri}")]
    public static partial void ConnectionsLinkGitHubRequested(this ILogger logger, int userId, string redirectUri);

    [LoggerMessage(EventId = 4001, Level = LogLevel.Error, Message = "GitHub token exchange failed. error={error}")]
    public static partial void ConnectionsGitHubTokenExchangeFailed(this ILogger logger, string error);

    [LoggerMessage(EventId = 4002, Level = LogLevel.Information, Message = "Linking GitHub account. userId={userId} login={login} githubId={githubId}")]
    public static partial void ConnectionsLinkingGitHubAccount(this ILogger logger, int userId, string login, long githubId);

    [LoggerMessage(EventId = 4003, Level = LogLevel.Information, Message = "Unlinking GitHub account. userId={userId} connectedAs={connectedAs}")]
    public static partial void ConnectionsUnlinkingGitHubAccount(this ILogger logger, int userId, string connectedAs);

    [LoggerMessage(EventId = 4004, Level = LogLevel.Information, Message = "Fetched GitHub repositories. userId={userId} count={count}")]
    public static partial void ConnectionsFetchedGitHubRepositories(this ILogger logger, int userId, int count);
}
