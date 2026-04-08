using System.Text.Json;

namespace Fleet.Server.Agents.Tools;

/// <summary>
/// Agent tool that generates Infrastructure as Code templates (Terraform, Bicep,
/// CloudFormation) and writes them to the repository sandbox. The LLM uses this
/// tool during agent pipeline execution to produce deployment configurations.
/// </summary>
public class GenerateIaCTool : IAgentTool
{
    public string Name => "generate_iac";

    public string Description =>
        "Generate Infrastructure as Code (IaC) deployment templates and write them to the repo. " +
        "Supports Terraform (.tf), Bicep (.bicep), CloudFormation (.yaml), and GitHub Actions (.yml) formats. " +
        "The tool creates properly structured files with correct naming and placement conventions. " +
        "Use this when deploying the project to cloud infrastructure or setting up CI/CD pipelines.";

    public string ParametersJsonSchema => """
        {
            "type": "object",
            "properties": {
                "format": {
                    "type": "string",
                    "description": "The IaC format to generate.",
                    "enum": ["terraform", "bicep", "cloudformation", "github-actions"]
                },
                "path": {
                    "type": "string",
                    "description": "File path relative to repo root where the template should be written. If omitted, uses conventional paths (e.g. 'infra/main.tf', 'infra/main.bicep', '.github/workflows/deploy.yml')."
                },
                "content": {
                    "type": "string",
                    "description": "The full IaC template content to write."
                },
                "cloud_provider": {
                    "type": "string",
                    "description": "Target cloud provider.",
                    "enum": ["aws", "azure", "gcp"]
                },
                "description": {
                    "type": "string",
                    "description": "Brief description of what this template provisions or deploys."
                }
            },
            "required": ["format", "content"]
        }
        """;

    public Task<string> ExecuteAsync(string argumentsJson, AgentToolContext context, CancellationToken cancellationToken)
    {
        var args = JsonSerializer.Deserialize<JsonElement>(argumentsJson);

        var format = args.TryGetProperty("format", out var formatProp) ? formatProp.GetString() : null;
        if (string.IsNullOrWhiteSpace(format))
            return Task.FromResult("Error: 'format' parameter is required.");

        var content = args.TryGetProperty("content", out var contentProp) ? contentProp.GetString() : null;
        if (string.IsNullOrWhiteSpace(content))
            return Task.FromResult("Error: 'content' parameter is required.");

        var path = args.TryGetProperty("path", out var pathProp) ? pathProp.GetString() : null;
        var description = args.TryGetProperty("description", out var descProp) ? descProp.GetString() : null;

        // Default paths per format
        path ??= format switch
        {
            "terraform" => "infra/main.tf",
            "bicep" => "infra/main.bicep",
            "cloudformation" => "infra/cloudformation.yaml",
            "github-actions" => ".github/workflows/deploy.yml",
            _ => $"infra/template.{format}"
        };

        try
        {
            context.Sandbox.WriteFile(path, content);

            var result = $"Successfully wrote {format} template to '{path}' ({content.Length:N0} characters).";
            if (!string.IsNullOrWhiteSpace(description))
                result += $" Description: {description}";

            return Task.FromResult(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Task.FromResult($"Error: {ex.Message}");
        }
    }
}
