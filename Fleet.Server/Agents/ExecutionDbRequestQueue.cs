using System.Threading.Channels;
using Fleet.Server.Data;

namespace Fleet.Server.Agents;

internal sealed class ExecutionDbRequestQueue : IAsyncDisposable
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly Channel<IQueuedDbRequest> _channel = Channel.CreateUnbounded<IQueuedDbRequest>(new UnboundedChannelOptions
    {
        SingleReader = true,
        SingleWriter = false,
        AllowSynchronousContinuations = false,
    });

    private readonly Task _processorTask;

    public ExecutionDbRequestQueue(IServiceScopeFactory serviceScopeFactory)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _processorTask = Task.Run(ProcessAsync);
    }

    public async Task EnqueueAsync(Func<FleetDbContext, Task> action)
    {
        var request = new QueuedDbRequest<object?>(async () =>
        {
            await using var scope = _serviceScopeFactory.CreateAsyncScope();
            var queuedDb = scope.ServiceProvider.GetRequiredService<FleetDbContext>();
            await action(queuedDb);
            return null;
        });

        await _channel.Writer.WriteAsync(request);
        await request.Completion;
    }

    public async Task<T> ExecuteReadAsync<T>(Func<FleetDbContext, Task<T>> action)
    {
        await using var scope = _serviceScopeFactory.CreateAsyncScope();
        var queuedDb = scope.ServiceProvider.GetRequiredService<FleetDbContext>();
        return await action(queuedDb);
    }

    public async ValueTask DisposeAsync()
    {
        _channel.Writer.TryComplete();
        await _processorTask;
    }

    private async Task ProcessAsync()
    {
        await foreach (var request in _channel.Reader.ReadAllAsync())
        {
            await request.ExecuteAsync();
        }
    }

    private interface IQueuedDbRequest
    {
        Task ExecuteAsync();
    }

    private sealed class QueuedDbRequest<T>(Func<Task<T>> action) : IQueuedDbRequest
    {
        private readonly TaskCompletionSource<T> _completion =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<T> Completion => _completion.Task;

        public async Task ExecuteAsync()
        {
            try
            {
                var result = await action();
                _completion.TrySetResult(result);
            }
            catch (OperationCanceledException canceled) when (canceled.CancellationToken.CanBeCanceled)
            {
                _completion.TrySetCanceled(canceled.CancellationToken);
            }
            catch (OperationCanceledException)
            {
                _completion.TrySetCanceled();
            }
            catch (Exception ex)
            {
                _completion.TrySetException(ex);
            }
        }
    }
}
