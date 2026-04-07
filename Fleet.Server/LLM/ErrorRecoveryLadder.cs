namespace Fleet.Server.LLM;

/// <summary>
/// Progressive error recovery for tool-calling loops.
/// Tracks consecutive tool failures and escalates recovery actions:
///   Level 0: Normal operation
///   Level 1 (3+ errors): Inject recovery hint asking model to change approach
///   Level 2 (6+ errors): Aggressively compress context to free up token budget
///   Level 3 (10+ errors): Abort the loop — the model is stuck
/// Inspired by Claude Code's error recovery escalation ladder.
/// </summary>
public class ErrorRecoveryLadder
{
    /// <summary>Consecutive errors before injecting a recovery hint.</summary>
    public const int HintThreshold = 3;

    /// <summary>Consecutive errors before aggressive context compression.</summary>
    public const int CompressThreshold = 6;

    /// <summary>Consecutive errors before aborting the loop.</summary>
    public const int AbortThreshold = 10;

    private int _consecutiveErrors;

    /// <summary>Current consecutive error count.</summary>
    public int ConsecutiveErrors => _consecutiveErrors;

    /// <summary>Record a tool result. Returns the current recovery level.</summary>
    public RecoveryLevel RecordResult(bool isError)
    {
        if (!isError)
        {
            _consecutiveErrors = 0;
            return RecoveryLevel.None;
        }

        _consecutiveErrors++;

        if (_consecutiveErrors >= AbortThreshold)
            return RecoveryLevel.Abort;
        if (_consecutiveErrors >= CompressThreshold)
            return RecoveryLevel.CompressContext;
        if (_consecutiveErrors >= HintThreshold)
            return RecoveryLevel.InjectHint;

        return RecoveryLevel.None;
    }

    /// <summary>Generate a recovery hint message for the model.</summary>
    public static LLMMessage CreateRecoveryHint(int errorCount)
    {
        return new LLMMessage
        {
            Role = "user",
            Content = $"[System notice: {errorCount} consecutive tool errors detected. " +
                      "You appear to be stuck in an error loop. Please: " +
                      "1) Stop repeating the same tool calls that are failing, " +
                      "2) Try an alternative approach or different tool, " +
                      "3) If no alternative exists, explain the blocker to the user and proceed without the failing tool.]",
        };
    }
}

/// <summary>Progressive error recovery levels.</summary>
public enum RecoveryLevel
{
    /// <summary>Normal operation — no intervention needed.</summary>
    None,
    /// <summary>Inject a hint asking the model to change approach.</summary>
    InjectHint,
    /// <summary>Compress context aggressively to free token budget.</summary>
    CompressContext,
    /// <summary>Abort the loop — the model is stuck beyond recovery.</summary>
    Abort,
}
