using System.Collections.Concurrent;
using System.Text.Json;
using System.Threading.Channels;
using Microsoft.AspNetCore.Http.Features;

namespace Fleet.Server.Realtime;

public sealed class ServerEventPublisher(
    ILogger<ServerEventPublisher> logger) : IServerEventPublisher
{
    private static readonly TimeSpan KeepAliveInterval = TimeSpan.FromSeconds(10);
    private static readonly JsonSerializerOptions CamelCaseOptions = new(JsonSerializerDefaults.Web);
    private readonly ConcurrentDictionary<Guid, Subscription> _subscriptions = new();

    public async Task SubscribeAsync(
        int userId,
        string? projectId,
        HttpResponse response,
        CancellationToken cancellationToken)
    {
        var normalizedProjectId = NormalizeProjectId(projectId);
        var channel = Channel.CreateBounded<ServerEventEnvelope>(new BoundedChannelOptions(256)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.DropOldest,
        });
        var subscription = new Subscription(
            Id: Guid.NewGuid(),
            UserId: userId,
            ProjectId: normalizedProjectId,
            Channel: channel);

        _subscriptions[subscription.Id] = subscription;

        ConfigureSseHeaders(response);

        try
        {
            await WriteEventAsync(
                response,
                new ServerEventEnvelope(
                    ServerEventTopics.Connected,
                    JsonSerializer.Serialize(new
                    {
                        userId,
                        projectId = normalizedProjectId,
                        connectedAtUtc = DateTime.UtcNow,
                    }, CamelCaseOptions)),
                cancellationToken);

            logger.LogInformation(
                "SSE client connected: userId={UserId}, projectId={ProjectId}, subscriptionId={SubscriptionId}",
                userId,
                normalizedProjectId ?? "(none)",
                subscription.Id);

            while (!cancellationToken.IsCancellationRequested)
            {
                using var keepAliveCts = new CancellationTokenSource(KeepAliveInterval);
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken,
                    keepAliveCts.Token);

                try
                {
                    var envelope = await subscription.Channel.Reader.ReadAsync(linkedCts.Token);
                    await WriteEventAsync(response, envelope, cancellationToken);
                }
                catch (OperationCanceledException) when (
                    !cancellationToken.IsCancellationRequested &&
                    keepAliveCts.IsCancellationRequested)
                {
                    await WriteEventAsync(
                        response,
                        new ServerEventEnvelope(
                            ServerEventTopics.Heartbeat,
                            JsonSerializer.Serialize(new
                            {
                                timestampUtc = DateTime.UtcNow,
                            }, CamelCaseOptions)),
                        cancellationToken);
                }
            }
        }
        catch (OperationCanceledException) when (IsClientDisconnect(response, cancellationToken))
        {
            // Client disconnected.
        }
        finally
        {
            _subscriptions.TryRemove(subscription.Id, out _);
            subscription.Channel.Writer.TryComplete();

            logger.LogInformation(
                "SSE client disconnected: userId={UserId}, projectId={ProjectId}, subscriptionId={SubscriptionId}",
                userId,
                normalizedProjectId ?? "(none)",
                subscription.Id);
        }
    }

    public Task PublishUserEventAsync(
        int userId,
        string eventName,
        object? payload = null,
        CancellationToken cancellationToken = default)
        => PublishInternalAsync(
            sub => sub.UserId == userId,
            eventName,
            payload,
            cancellationToken);

    public Task PublishProjectEventAsync(
        int userId,
        string projectId,
        string eventName,
        object? payload = null,
        CancellationToken cancellationToken = default)
    {
        var normalizedProjectId = NormalizeProjectId(projectId);
        if (normalizedProjectId is null)
            return PublishUserEventAsync(userId, eventName, payload, cancellationToken);

        return PublishInternalAsync(
            sub =>
                sub.UserId == userId &&
                (sub.ProjectId is null ||
                 string.Equals(sub.ProjectId, normalizedProjectId, StringComparison.OrdinalIgnoreCase)),
            eventName,
            payload,
            cancellationToken);
    }

    private Task PublishInternalAsync(
        Func<Subscription, bool> filter,
        string eventName,
        object? payload,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var envelope = new ServerEventEnvelope(
            eventName,
            JsonSerializer.Serialize(payload ?? new { }, CamelCaseOptions));

        foreach (var subscription in _subscriptions.Values)
        {
            if (!filter(subscription))
                continue;

            subscription.Channel.Writer.TryWrite(envelope);
        }

        return Task.CompletedTask;
    }

    private static void ConfigureSseHeaders(HttpResponse response)
    {
        response.HttpContext.Features.Get<IHttpResponseBodyFeature>()?.DisableBuffering();
        response.StatusCode = StatusCodes.Status200OK;
        response.ContentType = "text/event-stream";
        response.Headers["Cache-Control"] = "no-cache, no-store, must-revalidate";
        response.Headers["Connection"] = "keep-alive";
        response.Headers["X-Accel-Buffering"] = "no";
        response.Headers["Content-Encoding"] = "identity";
    }

    private static async Task WriteEventAsync(
        HttpResponse response,
        ServerEventEnvelope envelope,
        CancellationToken cancellationToken)
    {
        await response.WriteAsync($"event: {envelope.EventName}\n", cancellationToken);

        var normalized = envelope.JsonPayload.Replace("\r\n", "\n", StringComparison.Ordinal);
        var lines = normalized.Split('\n');
        foreach (var line in lines)
        {
            await response.WriteAsync($"data: {line}\n", cancellationToken);
        }

        await response.WriteAsync("\n", cancellationToken);
        await response.Body.FlushAsync(cancellationToken);
    }

    private static string? NormalizeProjectId(string? projectId)
        => string.IsNullOrWhiteSpace(projectId) ? null : projectId.Trim();

    private static bool IsClientDisconnect(HttpResponse response, CancellationToken cancellationToken)
        => cancellationToken.IsCancellationRequested ||
           response.HttpContext.RequestAborted.IsCancellationRequested;

    private sealed record ServerEventEnvelope(string EventName, string JsonPayload);

    private sealed record Subscription(
        Guid Id,
        int UserId,
        string? ProjectId,
        Channel<ServerEventEnvelope> Channel);
}
