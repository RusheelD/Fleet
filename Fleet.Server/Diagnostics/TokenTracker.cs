using System.Diagnostics.Metrics;
using Fleet.Server.LLM;

namespace Fleet.Server.Diagnostics;

/// <summary>
/// Accumulates LLM token usage within a DI scope and emits OpenTelemetry counters.
/// </summary>
public class TokenTracker : ITokenTracker
{
    private static readonly Meter Meter = new("Fleet.UsageMetrics");
    private static readonly Counter<long> InputTokenCounter = Meter.CreateCounter<long>(
        "fleet.llm.input_tokens", "tokens", "Total LLM input tokens consumed");
    private static readonly Counter<long> OutputTokenCounter = Meter.CreateCounter<long>(
        "fleet.llm.output_tokens", "tokens", "Total LLM output tokens generated");
    private static readonly Counter<long> CachedTokenCounter = Meter.CreateCounter<long>(
        "fleet.llm.cached_tokens", "tokens", "Total LLM cached input tokens");

    public int TotalInputTokens { get; private set; }
    public int TotalOutputTokens { get; private set; }
    public int TotalCachedTokens { get; private set; }

    public void Record(LLMUsage? usage)
    {
        if (usage is null) return;

        TotalInputTokens += usage.InputTokens;
        TotalOutputTokens += usage.OutputTokens;
        TotalCachedTokens += usage.CachedInputTokens ?? 0;

        InputTokenCounter.Add(usage.InputTokens);
        OutputTokenCounter.Add(usage.OutputTokens);
        if (usage.CachedInputTokens.HasValue)
        {
            CachedTokenCounter.Add(usage.CachedInputTokens.Value);
        }
    }
}
