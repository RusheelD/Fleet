using Fleet.Server.Agents;

namespace Fleet.Server.Tests.Services;

[TestClass]
public class AgentCallCapacityManagerTests
{
    [TestMethod]
    public void NormalizeConfiguredCapacity_FallsBackToDefaultForNonPositiveValues()
    {
        Assert.AreEqual(8, AgentCallCapacityManager.NormalizeConfiguredCapacity(0));
        Assert.AreEqual(8, AgentCallCapacityManager.NormalizeConfiguredCapacity(-4));
        Assert.AreEqual(3, AgentCallCapacityManager.NormalizeConfiguredCapacity(3));
    }

    [TestMethod]
    public async Task AcquireAsync_WaitsUntilAPermitIsReleased()
    {
        var manager = new AgentCallCapacityManager(1);
        Assert.IsTrue(manager.TryAcquire(out var firstLease));
        Assert.IsNotNull(firstLease);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        var waitingAcquireTask = manager.AcquireAsync(cts.Token);

        await Task.Delay(100, CancellationToken.None);
        Assert.IsFalse(waitingAcquireTask.IsCompleted);
        Assert.AreEqual(1, manager.InUseCount);
        Assert.AreEqual(1, manager.WaitingCount);

        firstLease!.Dispose();

        using var secondLease = await waitingAcquireTask;
        Assert.AreEqual(1, manager.InUseCount);
        Assert.AreEqual(0, manager.WaitingCount);
    }

    [TestMethod]
    public void TryAcquire_ReflectsImmediateCapacityAvailability()
    {
        var manager = new AgentCallCapacityManager(1);

        Assert.IsTrue(manager.TryAcquire(out var firstLease));
        Assert.IsNotNull(firstLease);
        Assert.IsFalse(manager.TryAcquire(out var blockedLease));
        Assert.IsNull(blockedLease);

        firstLease!.Dispose();

        Assert.IsTrue(manager.TryAcquire(out var secondLease));
        Assert.IsNotNull(secondLease);
        secondLease!.Dispose();
    }
}
