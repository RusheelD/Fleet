namespace Fleet.Server.Copilot;

public class AgentAutoExecutionDispatchPolicyOptions
{
    public const string SectionName = "AgentAutoExecutionDispatchPolicy";

    public int MaxAutoStartPerMessage { get; set; } = 3;

    public int MaxActiveExecutionsPerSession { get; set; } = 3;

    public string[] AllowedLevels { get; set; } = ["Bug", "Task"];

    public int MaxDynamicAutoStartPerMessage { get; set; }

    public int MaxDynamicActiveExecutionsPerSession { get; set; }

    public string[] DynamicAllowedLevels { get; set; } = ["Feature", "Component", "Bug", "Task"];
}
