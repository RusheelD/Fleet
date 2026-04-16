using Fleet.Server.LLM;
using Microsoft.Extensions.Options;

namespace Fleet.Server.Agents;

/// <summary>
/// Coordinates the app-wide ceiling for concurrent agent model turns.
/// Runs wait here instead of failing when Fleet is already at capacity.
/// </summary>
public sealed class AgentCallCapacityManager
{
    private const int DefaultMaxConcurrentAgentCalls = 8;
    private readonly SemaphoreSlim _semaphore;
    private int _inUseCount;
    private int _waitingCount;

    public AgentCallCapacityManager(IOptions<LLMOptions> llmOptions)
        : this(llmOptions.Value.MaxConcurrentAgentCalls)
    {
    }

    internal AgentCallCapacityManager(int configuredCapacity)
    {
        Capacity = NormalizeConfiguredCapacity(configuredCapacity);
        _semaphore = new SemaphoreSlim(Capacity, Capacity);
    }

    public int Capacity { get; }

    internal int InUseCount => Math.Max(0, Volatile.Read(ref _inUseCount));

    internal int WaitingCount => Math.Max(0, Volatile.Read(ref _waitingCount));

    internal static int NormalizeConfiguredCapacity(int configuredCapacity)
        => configuredCapacity > 0 ? configuredCapacity : DefaultMaxConcurrentAgentCalls;

    internal bool TryAcquire(out Lease? lease)
    {
        if (_semaphore.Wait(0))
        {
            Interlocked.Increment(ref _inUseCount);
            lease = new Lease(this);
            return true;
        }

        lease = null;
        return false;
    }

    internal async Task<Lease> AcquireAsync(CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _waitingCount);
        try
        {
            await _semaphore.WaitAsync(cancellationToken);
        }
        finally
        {
            Interlocked.Decrement(ref _waitingCount);
        }

        Interlocked.Increment(ref _inUseCount);
        return new Lease(this);
    }

    private void Release()
    {
        Interlocked.Decrement(ref _inUseCount);
        _semaphore.Release();
    }

    internal sealed class Lease : IDisposable
    {
        private AgentCallCapacityManager? _owner;

        internal Lease(AgentCallCapacityManager owner)
        {
            _owner = owner;
        }

        public void Dispose()
        {
            Interlocked.Exchange(ref _owner, null)?.Release();
        }
    }
}
