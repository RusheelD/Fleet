namespace Fleet.Server.LLM;

public sealed record ToolCallBatch(IReadOnlyList<LLMToolCall> ToolCalls, bool CanRunInParallel);

public static class ToolCallBatchPlanner
{
    public static IReadOnlyList<ToolCallBatch> PartitionByReadOnly(
        IReadOnlyList<LLMToolCall> toolCalls,
        Func<LLMToolCall, bool> isReadOnly)
    {
        ArgumentNullException.ThrowIfNull(toolCalls);
        ArgumentNullException.ThrowIfNull(isReadOnly);

        if (toolCalls.Count == 0)
        {
            return [];
        }

        var batches = new List<ToolCallBatch>();
        var currentBatch = new List<LLMToolCall>();
        bool? currentBatchIsReadOnly = null;

        foreach (var toolCall in toolCalls)
        {
            var nextIsReadOnly = isReadOnly(toolCall);
            if (currentBatchIsReadOnly is null || currentBatchIsReadOnly == nextIsReadOnly)
            {
                currentBatch.Add(toolCall);
                currentBatchIsReadOnly = nextIsReadOnly;
                continue;
            }

            batches.Add(new ToolCallBatch([.. currentBatch], currentBatchIsReadOnly.Value));
            currentBatch = [toolCall];
            currentBatchIsReadOnly = nextIsReadOnly;
        }

        if (currentBatch.Count > 0 && currentBatchIsReadOnly is not null)
        {
            batches.Add(new ToolCallBatch([.. currentBatch], currentBatchIsReadOnly.Value));
        }

        return batches;
    }
}
