using Fleet.Server.Copilot;
using Fleet.Server.LLM;
using Microsoft.Extensions.DependencyInjection;

namespace Fleet.Server.Tests.Services;

[TestClass]
public class ChatServiceBackgroundScopeTests
{
    [TestMethod]
    public async Task RunBackgroundMemoryExtractionDetachedAsync_UsesFreshScopeThatOutlivesCallerScope()
    {
        var services = new ServiceCollection();
        services.AddSingleton<MemoryExtractionState>();
        services.AddScoped<ScopedMemoryProbe>();
        services.AddScoped<IMemoryExtractor, FakeMemoryExtractor>();

        await using var provider = services.BuildServiceProvider();

        IServiceScopeFactory scopeFactory;
        await using (var callerScope = provider.CreateAsyncScope())
        {
            scopeFactory = callerScope.ServiceProvider.GetRequiredService<IServiceScopeFactory>();
        }

        var messages = new List<LLMMessage>
        {
            new() { Role = "user", Content = "One" },
            new() { Role = "assistant", Content = "Two" },
            new() { Role = "user", Content = "Three" },
            new() { Role = "assistant", Content = "Four" },
        };

        await ChatService.RunBackgroundMemoryExtractionDetachedAsync(
            scopeFactory,
            userId: 7,
            projectId: "proj-1",
            snapshot: messages);

        var state = provider.GetRequiredService<MemoryExtractionState>();
        Assert.IsTrue(state.WasCalled);
        Assert.IsFalse(state.WasProbeDisposedDuringExecution);
        Assert.IsTrue(state.WasProbeDisposedAfterExecution);
        Assert.AreEqual(7, state.LastUserId);
        Assert.AreEqual("proj-1", state.LastProjectId);
    }

    private sealed class MemoryExtractionState
    {
        public bool WasCalled { get; set; }
        public bool WasProbeDisposedDuringExecution { get; set; }
        public bool WasProbeDisposedAfterExecution { get; set; }
        public int LastUserId { get; set; }
        public string? LastProjectId { get; set; }
    }

    private sealed class ScopedMemoryProbe(MemoryExtractionState state) : IDisposable
    {
        public bool IsDisposed { get; private set; }

        public void Dispose()
        {
            IsDisposed = true;
            state.WasProbeDisposedAfterExecution = true;
        }
    }

    private sealed class FakeMemoryExtractor(
        MemoryExtractionState state,
        ScopedMemoryProbe probe) : IMemoryExtractor
    {
        public async Task ExtractAndSaveAsync(
            int userId,
            string? projectId,
            IReadOnlyList<LLMMessage> recentMessages,
            CancellationToken cancellationToken = default)
        {
            state.WasCalled = true;
            state.LastUserId = userId;
            state.LastProjectId = projectId;
            state.WasProbeDisposedDuringExecution |= probe.IsDisposed;
            await Task.Delay(10, cancellationToken);
            state.WasProbeDisposedDuringExecution |= probe.IsDisposed;
        }
    }
}
