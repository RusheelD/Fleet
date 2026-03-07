using System.Text.Json;
using Fleet.Server.Agents;
using Fleet.Server.Projects;

namespace Fleet.Server.Copilot.Tools;

/// <summary>Lists agent executions across one or more projects.</summary>
public class ListAgentExecutionsTool(
    IProjectService projectService,
    IAgentService agentService) : IChatTool
{
    public string Name => "list_agent_executions";

    public string Description =>
        "List agent execution runs. In global chat, this can return executions across all projects.";

    public string ParametersJsonSchema => """
        {
            "type": "object",
            "properties": {
                "projectId": {
                    "type": "string",
                    "description": "Optional project id filter (global chat only)."
                },
                "projectSlug": {
                    "type": "string",
                    "description": "Optional project slug filter (global chat only)."
                },
                "status": {
                    "type": "string",
                    "description": "Optional status filter (running/completed/failed/paused)."
                },
                "limit": {
                    "type": "integer",
                    "description": "Maximum number of executions to return (default 50)."
                }
            },
            "required": []
        }
        """;

    public async Task<string> ExecuteAsync(string argumentsJson, ChatToolContext context, CancellationToken cancellationToken = default)
    {
        var args = ParseArgs(argumentsJson);
        var projects = await ChatToolProjectResolver.ResolveProjectsAsync(
            projectService,
            context,
            args.ProjectId,
            args.ProjectSlug);
        if (projects.Count == 0)
            return "No projects found for the requested scope.";

        var records = new List<object>();
        foreach (var project in projects)
        {
            var executions = await agentService.GetExecutionsAsync(project.Id);
            foreach (var execution in executions)
            {
                if (!string.IsNullOrWhiteSpace(args.Status) &&
                    !execution.Status.Equals(args.Status, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                records.Add(new
                {
                    ProjectId = project.Id,
                    ProjectSlug = project.Slug,
                    ProjectTitle = project.Title,
                    execution.Id,
                    execution.WorkItemId,
                    execution.WorkItemTitle,
                    execution.Status,
                    execution.StartedAt,
                    execution.Duration,
                    execution.Progress,
                    execution.BranchName,
                    execution.PullRequestUrl,
                    execution.CurrentPhase,
                    Agents = execution.Agents.Select(agent => new
                    {
                        agent.Role,
                        agent.Status,
                        agent.CurrentTask,
                        agent.Progress,
                    }),
                });
            }
        }

        var limited = records.Take(args.Limit).ToList();
        return limited.Count == 0
            ? "No agent executions found."
            : JsonSerializer.Serialize(limited, new JsonSerializerOptions { WriteIndented = true });
    }

    private static ListAgentExecutionsArgs ParseArgs(string argumentsJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(argumentsJson);
            var root = doc.RootElement;
            var projectId = root.TryGetProperty("projectId", out var projectIdEl) ? projectIdEl.GetString() : null;
            var projectSlug = root.TryGetProperty("projectSlug", out var projectSlugEl) ? projectSlugEl.GetString() : null;
            var status = root.TryGetProperty("status", out var statusEl) ? statusEl.GetString() : null;
            var limit = root.TryGetProperty("limit", out var limitEl) && limitEl.TryGetInt32(out var limitValue)
                ? limitValue
                : 50;
            return new ListAgentExecutionsArgs(projectId, projectSlug, status, Math.Clamp(limit, 1, 200));
        }
        catch
        {
            return new ListAgentExecutionsArgs(null, null, null, 50);
        }
    }

    private sealed record ListAgentExecutionsArgs(
        string? ProjectId,
        string? ProjectSlug,
        string? Status,
        int Limit);
}
