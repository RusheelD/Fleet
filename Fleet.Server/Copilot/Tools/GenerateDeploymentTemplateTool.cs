using System.Net.Http.Headers;
using System.Text.Json;
using Fleet.Server.Connections;
using Fleet.Server.Projects;

namespace Fleet.Server.Copilot.Tools;

/// <summary>
/// Generates Infrastructure as Code (IaC) deployment templates for the project's
/// technology stack. Supports Terraform, Bicep, and CloudFormation formats.
/// The LLM analyses the repo tree and generates appropriate hosting configuration.
/// </summary>
public class GenerateDeploymentTemplateTool(
    IProjectService projectService,
    IConnectionService connectionService,
    IHttpClientFactory httpClientFactory) : IChatTool
{
    public string Name => "generate_deployment_template";

    public bool IsWriteTool => true;

    public string Description =>
        "Generate Infrastructure as Code (IaC) deployment templates for hosting and CI/CD pipelines. " +
        "Analyses the project's repository structure and technology stack, then produces production-ready " +
        "templates in the requested format: Terraform (AWS/Azure/GCP), Bicep (Azure), CloudFormation (AWS), " +
        "or GitHub Actions CI/CD pipelines. Returns the generated template content that can be committed to the repo.";

    public string ParametersJsonSchema => """
        {
            "type": "object",
            "properties": {
                "format": {
                    "type": "string",
                    "description": "The IaC format to generate.",
                    "enum": ["terraform", "bicep", "cloudformation", "github-actions"]
                },
                "target": {
                    "type": "string",
                    "description": "What to generate: 'infrastructure' for hosting resources, 'pipeline' for CI/CD, 'both' for full deployment stack.",
                    "enum": ["infrastructure", "pipeline", "both"],
                    "default": "both"
                },
                "cloud_provider": {
                    "type": "string",
                    "description": "Target cloud provider (required for terraform and cloudformation).",
                    "enum": ["aws", "azure", "gcp"]
                },
                "app_name": {
                    "type": "string",
                    "description": "Application name for resource naming. Defaults to the project slug."
                },
                "environment": {
                    "type": "string",
                    "description": "Target environment for the deployment.",
                    "enum": ["development", "staging", "production"],
                    "default": "production"
                },
                "include_database": {
                    "type": "boolean",
                    "description": "Whether to include database resources. Auto-detected from repo if omitted."
                },
                "include_cache": {
                    "type": "boolean",
                    "description": "Whether to include cache (Redis) resources. Auto-detected from repo if omitted."
                },
                "containerized": {
                    "type": "boolean",
                    "description": "Whether to deploy as containers (Docker/Kubernetes) vs. platform services. Default: true.",
                    "default": true
                }
            },
            "required": ["format"]
        }
        """;

    public async Task<string> ExecuteAsync(string argumentsJson, ChatToolContext context, CancellationToken cancellationToken = default)
    {
        if (!context.TryGetProjectId(out var projectId))
            return ChatToolContext.ProjectScopeRequiredMessage;

        var args = JsonSerializer.Deserialize<JsonElement>(argumentsJson);
        var format = args.TryGetProperty("format", out var formatProp) ? formatProp.GetString() : null;
        if (string.IsNullOrWhiteSpace(format))
            return "Error: 'format' parameter is required. Use 'terraform', 'bicep', 'cloudformation', or 'github-actions'.";

        var target = args.TryGetProperty("target", out var targetProp) ? targetProp.GetString() ?? "both" : "both";
        var cloudProvider = args.TryGetProperty("cloud_provider", out var cloudProp) ? cloudProp.GetString() : null;
        var appName = args.TryGetProperty("app_name", out var appProp) ? appProp.GetString() : null;
        var environment = args.TryGetProperty("environment", out var envProp) ? envProp.GetString() ?? "production" : "production";
        var containerized = !args.TryGetProperty("containerized", out var containerProp) || containerProp.GetBoolean();

        // Validate cloud provider requirement
        if (format is "terraform" or "cloudformation" && string.IsNullOrWhiteSpace(cloudProvider))
            return $"Error: 'cloud_provider' is required for {format}. Use 'aws', 'azure', or 'gcp'.";

        if (format == "bicep")
            cloudProvider = "azure";

        if (format == "cloudformation")
            cloudProvider = "aws";

        // Get project info
        var projects = await projectService.GetAllProjectsAsync();
        var project = projects.FirstOrDefault(p => p.Id == projectId);
        if (project is null)
            return "Error: project not found.";

        appName ??= project.Slug;

        // Fetch repo tree for stack analysis
        string? repoTree = null;
        if (!string.IsNullOrWhiteSpace(project.Repo))
        {
            repoTree = await FetchRepoTreeAsync(project.Repo, context.UserId, cancellationToken);
        }

        // Build a comprehensive analysis prompt for the LLM
        var analysis = AnalyzeStack(repoTree);

        return JsonSerializer.Serialize(new
        {
            format,
            target,
            cloudProvider,
            appName,
            environment,
            containerized,
            projectTitle = project.Title,
            repo = project.Repo,
            stackAnalysis = analysis,
            instruction = BuildInstruction(format, target, cloudProvider!, appName, environment, containerized, analysis),
        }, new JsonSerializerOptions { WriteIndented = true });
    }

    private static StackAnalysis AnalyzeStack(string? repoTree)
    {
        if (string.IsNullOrWhiteSpace(repoTree))
            return new StackAnalysis([], false, false, false, false, false, false, false);

        var tree = repoTree.ToLowerInvariant();
        var frameworks = new List<string>();

        // Detect frameworks
        if (tree.Contains("package.json")) frameworks.Add("node");
        if (tree.Contains("next.config")) frameworks.Add("nextjs");
        if (tree.Contains("vite.config")) frameworks.Add("vite");
        if (tree.Contains("angular.json")) frameworks.Add("angular");
        if (tree.Contains(".csproj")) frameworks.Add("dotnet");
        if (tree.Contains("requirements.txt") || tree.Contains("pyproject.toml")) frameworks.Add("python");
        if (tree.Contains("go.mod")) frameworks.Add("go");
        if (tree.Contains("cargo.toml")) frameworks.Add("rust");
        if (tree.Contains("pom.xml") || tree.Contains("build.gradle")) frameworks.Add("java");
        if (tree.Contains("gemfile")) frameworks.Add("ruby");

        return new StackAnalysis(
            Frameworks: frameworks,
            HasDockerfile: tree.Contains("dockerfile"),
            HasDatabase: tree.Contains("migrations") || tree.Contains("prisma") || tree.Contains("dbcontext"),
            HasRedis: tree.Contains("redis") || tree.Contains("cache"),
            HasTests: tree.Contains("test") || tree.Contains("spec"),
            IsMonorepo: tree.Contains("packages/") || tree.Contains("apps/"),
            HasFrontend: tree.Contains("src/app") || tree.Contains("src/pages") || tree.Contains("public/index.html"),
            HasApi: tree.Contains("controllers") || tree.Contains("api/") || tree.Contains("routes/")
        );
    }

    private static string BuildInstruction(
        string format, string target, string cloudProvider, string appName,
        string environment, bool containerized, StackAnalysis analysis)
    {
        var frameworks = analysis.Frameworks.Count > 0
            ? string.Join(", ", analysis.Frameworks)
            : "unknown (generate a generic template)";

        var parts = new List<string>
        {
            $"Generate a production-ready {format} template for deploying '{appName}' to {cloudProvider}.",
            $"Target environment: {environment}.",
            $"Detected stack: {frameworks}.",
        };

        if (target is "infrastructure" or "both")
        {
            parts.Add(containerized
                ? "Use container-based deployment (Docker/container apps/ECS/Cloud Run)."
                : "Use platform services (App Service/Elastic Beanstalk/Cloud Functions).");

            if (analysis.HasDatabase)
                parts.Add("Include managed database resources (PostgreSQL recommended).");
            if (analysis.HasRedis)
                parts.Add("Include managed Redis/cache resources.");
        }

        if (target is "pipeline" or "both")
        {
            parts.Add("Include a CI/CD pipeline (GitHub Actions workflow) that builds, tests, and deploys.");
        }

        parts.Add("Return ONLY the template content, ready to be saved to the appropriate file.");
        parts.Add(format switch
        {
            "terraform" => "Use HCL format with proper variable definitions, outputs, and modules.",
            "bicep" => "Use Bicep format with parameters, modules, and outputs.",
            "cloudformation" => "Use YAML format with proper Parameters, Resources, and Outputs sections.",
            "github-actions" => "Use YAML format for .github/workflows/ with build, test, and deploy jobs.",
            _ => ""
        });

        return string.Join(" ", parts);
    }

    private async Task<string?> FetchRepoTreeAsync(string repo, string userId, CancellationToken cancellationToken)
    {
        try
        {
            if (!int.TryParse(userId, out var parsedUserId))
                return null;

            var token = await connectionService.GetPrimaryGitHubAccessTokenAsync(parsedUserId, cancellationToken);
            if (string.IsNullOrWhiteSpace(token))
                return null;

            using var client = httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.UserAgent.Clear();
            client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Fleet", "1.0"));
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await client.GetAsync(
                $"https://api.github.com/repos/{repo}/git/trees/HEAD?recursive=1",
                cancellationToken);

            if (!response.IsSuccessStatusCode)
                return null;

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("tree", out var tree))
                return null;

            var paths = tree.EnumerateArray()
                .Where(e => e.TryGetProperty("path", out _))
                .Select(e => e.GetProperty("path").GetString())
                .Where(p => p is not null)
                .Take(500);

            return string.Join("\n", paths);
        }
        catch
        {
            return null;
        }
    }

    private record StackAnalysis(
        List<string> Frameworks,
        bool HasDockerfile,
        bool HasDatabase,
        bool HasRedis,
        bool HasTests,
        bool IsMonorepo,
        bool HasFrontend,
        bool HasApi);
}
