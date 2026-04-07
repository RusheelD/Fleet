using Fleet.Server.LLM;

namespace Fleet.Server.Tests.Services;

[TestClass]
public class ToolCallBatchPlannerTests
{
    [TestMethod]
    public void PartitionByReadOnly_SplitsMixedBatchesIntoParallelAndSerialSegments()
    {
        var toolCalls = new[]
        {
            new LLMToolCall("1", "read_a", "{}"),
            new LLMToolCall("2", "read_b", "{}"),
            new LLMToolCall("3", "write_a", "{}"),
            new LLMToolCall("4", "read_c", "{}"),
            new LLMToolCall("5", "write_b", "{}"),
        };

        var batches = ToolCallBatchPlanner.PartitionByReadOnly(
            toolCalls,
            toolCall => toolCall.Name.StartsWith("read_", StringComparison.Ordinal));

        Assert.AreEqual(4, batches.Count);
        CollectionAssert.AreEqual(new[] { "read_a", "read_b" }, batches[0].ToolCalls.Select(call => call.Name).ToArray());
        Assert.IsTrue(batches[0].CanRunInParallel);
        CollectionAssert.AreEqual(new[] { "write_a" }, batches[1].ToolCalls.Select(call => call.Name).ToArray());
        Assert.IsFalse(batches[1].CanRunInParallel);
        CollectionAssert.AreEqual(new[] { "read_c" }, batches[2].ToolCalls.Select(call => call.Name).ToArray());
        Assert.IsTrue(batches[2].CanRunInParallel);
        CollectionAssert.AreEqual(new[] { "write_b" }, batches[3].ToolCalls.Select(call => call.Name).ToArray());
        Assert.IsFalse(batches[3].CanRunInParallel);
    }
}
