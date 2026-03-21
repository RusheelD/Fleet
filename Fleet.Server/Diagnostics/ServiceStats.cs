using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;

namespace Fleet.Server.Diagnostics;

public sealed class ServiceStats(IHostEnvironment hostEnvironment)
{
    private readonly DateTimeOffset _startedAtUtc = DateTimeOffset.UtcNow;
    private readonly string _applicationName = hostEnvironment.ApplicationName;
    private readonly string _environmentName = hostEnvironment.EnvironmentName;
    private readonly string _version = Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "unknown";
    private readonly ConcurrentDictionary<string, long> _requestMethods = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, long> _statusCodes = new(StringComparer.OrdinalIgnoreCase);

    private long _requestsStarted;
    private long _requestsCompleted;
    private long _requestsFailed;
    private long _requestsInFlight;
    private long _totalRequestDurationMs;

    public void RecordRequestStarted(string method)
    {
        Interlocked.Increment(ref _requestsStarted);
        Interlocked.Increment(ref _requestsInFlight);
        _requestMethods.AddOrUpdate(method, 1, static (_, current) => current + 1);
    }

    public void RecordRequestCompleted(int statusCode, long elapsedMilliseconds, bool failed)
    {
        Interlocked.Increment(ref _requestsCompleted);
        Interlocked.Decrement(ref _requestsInFlight);
        Interlocked.Add(ref _totalRequestDurationMs, elapsedMilliseconds);

        if (failed)
        {
            Interlocked.Increment(ref _requestsFailed);
        }

        _statusCodes.AddOrUpdate(statusCode.ToString(CultureInfo.InvariantCulture), 1, static (_, current) => current + 1);
    }

    public ServiceStatsSnapshot CreateSnapshot()
    {
        var currentUtc = DateTimeOffset.UtcNow;
        var requestsCompleted = Interlocked.Read(ref _requestsCompleted);
        var averageRequestDurationMs = requestsCompleted == 0
            ? 0
            : Math.Round((double)Interlocked.Read(ref _totalRequestDurationMs) / requestsCompleted, 2);

        return new ServiceStatsSnapshot
        {
            ApplicationName = _applicationName,
            EnvironmentName = _environmentName,
            Version = _version,
            ProcessId = Environment.ProcessId,
            StartedAtUtc = _startedAtUtc,
            CurrentUtc = currentUtc,
            UptimeSeconds = (long)(currentUtc - _startedAtUtc).TotalSeconds,
            RequestsStarted = Interlocked.Read(ref _requestsStarted),
            RequestsCompleted = requestsCompleted,
            RequestsFailed = Interlocked.Read(ref _requestsFailed),
            RequestsInFlight = Interlocked.Read(ref _requestsInFlight),
            AverageRequestDurationMs = averageRequestDurationMs,
            RequestMethods = _requestMethods
                .OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(pair => pair.Key, pair => pair.Value),
            StatusCodes = _statusCodes
                .OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(pair => pair.Key, pair => pair.Value),
        };
    }
}

public sealed class ServiceStatsSnapshot
{
    public required string ApplicationName { get; init; }
    public required string EnvironmentName { get; init; }
    public required string Version { get; init; }
    public required int ProcessId { get; init; }
    public required DateTimeOffset StartedAtUtc { get; init; }
    public required DateTimeOffset CurrentUtc { get; init; }
    public required long UptimeSeconds { get; init; }
    public required long RequestsStarted { get; init; }
    public required long RequestsCompleted { get; init; }
    public required long RequestsFailed { get; init; }
    public required long RequestsInFlight { get; init; }
    public required double AverageRequestDurationMs { get; init; }
    public required IReadOnlyDictionary<string, long> RequestMethods { get; init; }
    public required IReadOnlyDictionary<string, long> StatusCodes { get; init; }
}
