namespace Fleet.Server.Models;

public record DynamicIterationOptionsRequest(
    bool? Enabled = null,
    string? ExecutionPolicy = null,
    string? TargetBranch = null);

public record SendMessageRequest(
    string Content,
    bool GenerateWorkItems = false,
    DynamicIterationOptionsRequest? DynamicIteration = null);

public sealed record ChatSendOptions(
    bool GenerateWorkItems = false,
    DynamicIterationOptionsRequest? DynamicIteration = null);
