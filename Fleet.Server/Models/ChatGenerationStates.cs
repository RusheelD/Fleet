namespace Fleet.Server.Models;

public static class ChatGenerationStates
{
    public const string Idle = "idle";
    public const string Running = "running";
    public const string Canceling = "canceling";
    public const string Completed = "completed";
    public const string Failed = "failed";
    public const string Canceled = "canceled";
    public const string Interrupted = "interrupted";
}
