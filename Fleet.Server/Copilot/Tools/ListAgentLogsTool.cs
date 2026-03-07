using System.Text.Json;
using Fleet.Server.Agents;
using Fleet.Server.Projects;

namespace Fleet.Server.Copilot.Tools;

/// <summary>Lists recent agent logs across one or more projects.</summary>
public class ListAgentLogsTool(
    IProjectService projectService,
    IAgentService agentService) : IChatTool
{
    public string Name => "list_agent_logs";

    public string Description =>
        "List recent agent logs by project. In global chat, this can return logs across all projects.";

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
                "level": {
                    "type": "string",
                    "description": "Optional log level filter (info/warn/error/success)."
                },
                "limit": {
                    "type": "integer",
                    "description": "Maximum number of logs to return (default 100)."
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
            var logs = await agentService.GetLogsAsync(project.Id);
            foreach (var log in logs)
            {
                if (!string.IsNullOrWhiteSpace(args.Level) &&
                    !log.Level.Equals(args.Level, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                records.Add(new
                {
                    ProjectId = project.Id,
                    ProjectSlug = project.Slug,
                    ProjectTitle = project.Title,
                    log.Time,
                    log.Agent,
                    log.Level,
                    log.Message,
                    log.IsDetailed,
                });
            }
        }

        var limited = records.Take(args.Limit).ToList();
        return limited.Count == 0
            ? "No agent logs found."
            : JsonSerializer.Serialize(limited, new JsonSerializerOptions { WriteIndented = true });
    }

    private static ListAgentLogsArgs ParseArgs(string argumentsJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(argumentsJson);
            var root = doc.RootElement;
            var projectId = root.TryGetProperty("projectId", out var projectIdEl) ? projectIdEl.GetString() : null;
            var projectSlug = root.TryGetProperty("projectSlug", out var projectSlugEl) ? projectSlugEl.GetString() : null;
            var level = root.TryGetProperty("level", out var levelEl) ? levelEl.GetString() : null;
            var limit = root.TryGetProperty("limit", out var limitEl) && limitEl.TryGetInt32(out var limitValue)
                ? limitValue
                : 100;
            return new ListAgentLogsArgs(projectId, projectSlug, level, Math.Clamp(limit, 1, 300));
        }
        catch
        {
            return new ListAgentLogsArgs(null, null, null, 100);
        }
    }

    private sealed record ListAgentLogsArgs(
        string? ProjectId,
        string? ProjectSlug,
        string? Level,
        int Limit);
}
