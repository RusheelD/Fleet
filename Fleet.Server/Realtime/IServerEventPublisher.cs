using Microsoft.AspNetCore.Http;

namespace Fleet.Server.Realtime;

public interface IServerEventPublisher
{
    Task SubscribeAsync(
        int userId,
        string? projectId,
        HttpResponse response,
        CancellationToken cancellationToken);

    Task PublishUserEventAsync(
        int userId,
        string eventName,
        object? payload = null,
        CancellationToken cancellationToken = default);

    Task PublishProjectEventAsync(
        int userId,
        string projectId,
        string eventName,
        object? payload = null,
        CancellationToken cancellationToken = default);
}
