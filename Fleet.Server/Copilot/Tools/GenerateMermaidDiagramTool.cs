using System.Net.Http.Headers;
using System.Text.Json;
using Fleet.Server.Connections;
using Fleet.Server.Projects;

namespace Fleet.Server.Copilot.Tools;

/// <summary>
/// Generates Mermaid diagram markup for the project's architecture, tech stack,
/// or dependency graph. The LLM analyses the repo and returns renderable Mermaid syntax.
/// </summary>
public class GenerateMermaidDiagramTool(
    IProjectService projectService,
    IConnectionService connectionService,
    IHttpClientFactory httpClientFactory) : IChatTool
{
    public string Name => "generate_mermaid_diagram";

    public bool IsWriteTool => true;

    public string Description =>
        "Generate a Mermaid diagram for the project. Analyses the repository to produce " +
        "architecture diagrams, tech stack visualizations, dependency graphs, or CI/CD flow diagrams. " +
        "Returns renderable Mermaid markdown that can be displayed inline or committed to the repo as documentation.";

    public string ParametersJsonSchema => """
        {
            "type": "object",
            "properties": {
                "diagram_type": {
                    "type": "string",
                    "description": "The type of diagram to generate.",
                    "enum": ["architecture", "stack", "dependencies", "ci-cd", "entity-relationship", "sequence", "class"]
                },
                "scope": {
                    "type": "string",
                    "description": "What part of the project to diagram. Use 'full' for the whole project, or specify a subdirectory path (e.g. 'src/components', 'backend/api').",
                    "default": "full"
                },
                "detail_level": {
                    "type": "string",
                    "description": "How detailed the diagram should be.",
                    "enum": ["high-level", "detailed"],
                    "default": "high-level"
                },
                "include_external": {
                    "type": "boolean",
                    "description": "Whether to include external services and third-party dependencies in the diagram.",
                    "default": true
                }
            },
            "required": ["diagram_type"]
        }
        """;

    public async Task<string> ExecuteAsync(string argumentsJson, ChatToolContext context, CancellationToken cancellationToken = default)
    {
        if (!context.TryGetProjectId(out var projectId))
            return ChatToolContext.ProjectScopeRequiredMessage;

        var args = JsonSerializer.Deserialize<JsonElement>(argumentsJson);
        var diagramType = args.TryGetProperty("diagram_type", out var typeProp) ? typeProp.GetString() : null;
        if (string.IsNullOrWhiteSpace(diagramType))
            return "Error: 'diagram_type' parameter is required.";

        var scope = args.TryGetProperty("scope", out var scopeProp) ? scopeProp.GetString() ?? "full" : "full";
        var detailLevel = args.TryGetProperty("detail_level", out var detailProp) ? detailProp.GetString() ?? "high-level" : "high-level";
        var includeExternal = !args.TryGetProperty("include_external", out var extProp) || extProp.GetBoolean();

        // Get project info
        var projects = await projectService.GetAllProjectsAsync();
        var project = projects.FirstOrDefault(p => p.Id == projectId);
        if (project is null)
            return "Error: project not found.";

        // Fetch repo tree for analysis
        string? repoTree = null;
        string? packageJson = null;
        string? csprojContent = null;

        if (!string.IsNullOrWhiteSpace(project.Repo))
        {
            repoTree = await FetchRepoTreeAsync(project.Repo, context.UserId, cancellationToken);

            // Fetch key manifest files for dependency analysis
            if (diagramType is "dependencies" or "stack")
            {
                packageJson = await FetchFileAsync(project.Repo, "package.json", context.UserId, cancellationToken);
                // Try to find a .csproj file
                if (repoTree?.Contains(".csproj") == true)
                {
                    var csprojPath = repoTree
                        .Split('\n')
                        .FirstOrDefault(l => l.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase));
                    if (csprojPath is not null)
                        csprojContent = await FetchFileAsync(project.Repo, csprojPath, context.UserId, cancellationToken);
                }
            }
        }

        var analysis = AnalyzeForDiagram(repoTree, packageJson, csprojContent);

        return JsonSerializer.Serialize(new
        {
            diagramType,
            scope,
            detailLevel,
            includeExternal,
            projectTitle = project.Title,
            repo = project.Repo,
            analysis,
            instruction = BuildDiagramInstruction(diagramType, scope, detailLevel, includeExternal, analysis, project.Title),
        }, new JsonSerializerOptions { WriteIndented = true });
    }

    private static DiagramAnalysis AnalyzeForDiagram(string? repoTree, string? packageJson, string? csprojContent)
    {
        var frameworks = new List<string>();
        var services = new List<string>();
        var databases = new List<string>();
        var frontendLibs = new List<string>();
        var buildTools = new List<string>();

        if (repoTree is not null)
        {
            var tree = repoTree.ToLowerInvariant();

            // Frameworks
            if (tree.Contains(".csproj")) frameworks.Add(".NET");
            if (tree.Contains("next.config")) frameworks.Add("Next.js");
            else if (tree.Contains("vite.config")) frameworks.Add("Vite");
            if (tree.Contains("angular.json")) frameworks.Add("Angular");
            if (tree.Contains("requirements.txt") || tree.Contains("pyproject.toml")) frameworks.Add("Python");
            if (tree.Contains("go.mod")) frameworks.Add("Go");
            if (tree.Contains("cargo.toml")) frameworks.Add("Rust");
            if (tree.Contains("pom.xml")) frameworks.Add("Maven/Java");
            if (tree.Contains("build.gradle")) frameworks.Add("Gradle/Java");

            // Services
            if (tree.Contains("dockerfile") || tree.Contains("docker-compose")) services.Add("Docker");
            if (tree.Contains("kubernetes") || tree.Contains("k8s")) services.Add("Kubernetes");
            if (tree.Contains("redis")) services.Add("Redis");
            if (tree.Contains("nginx")) services.Add("Nginx");

            // Databases
            if (tree.Contains("migrations") || tree.Contains("dbcontext")) databases.Add("PostgreSQL/SQL");
            if (tree.Contains("prisma")) databases.Add("Prisma ORM");
            if (tree.Contains("mongodb") || tree.Contains("mongoose")) databases.Add("MongoDB");

            // Build
            if (tree.Contains(".github/workflows")) buildTools.Add("GitHub Actions");
            if (tree.Contains("azure-pipelines")) buildTools.Add("Azure Pipelines");
            if (tree.Contains("jenkinsfile")) buildTools.Add("Jenkins");
        }

        // Parse package.json for frontend libs
        if (packageJson is not null)
        {
            try
            {
                var pkg = JsonDocument.Parse(packageJson);
                var deps = new List<string>();
                if (pkg.RootElement.TryGetProperty("dependencies", out var d))
                    deps.AddRange(d.EnumerateObject().Select(p => p.Name));
                if (pkg.RootElement.TryGetProperty("devDependencies", out var dd))
                    deps.AddRange(dd.EnumerateObject().Select(p => p.Name));

                if (deps.Any(d => d == "react")) frontendLibs.Add("React");
                if (deps.Any(d => d == "vue")) frontendLibs.Add("Vue");
                if (deps.Any(d => d == "svelte")) frontendLibs.Add("Svelte");
                if (deps.Any(d => d.Contains("fluentui") || d.Contains("fluent-ui"))) frontendLibs.Add("Fluent UI");
                if (deps.Any(d => d.Contains("material"))) frontendLibs.Add("Material UI");
                if (deps.Any(d => d == "tailwindcss")) frontendLibs.Add("Tailwind CSS");
                if (deps.Any(d => d == "typescript")) buildTools.Add("TypeScript");
                if (deps.Any(d => d == "eslint")) buildTools.Add("ESLint");
            }
            catch
            {
                // Ignore parse errors
            }
        }

        // Parse .csproj for NuGet packages
        if (csprojContent is not null)
        {
            var nugetPackages = new List<string>();
            // Simple XML scanning for PackageReference
            foreach (var line in csprojContent.Split('\n'))
            {
                if (line.Contains("PackageReference", StringComparison.OrdinalIgnoreCase))
                {
                    var includeStart = line.IndexOf("Include=\"", StringComparison.OrdinalIgnoreCase);
                    if (includeStart >= 0)
                    {
                        includeStart += 9;
                        var includeEnd = line.IndexOf('"', includeStart);
                        if (includeEnd > includeStart)
                            nugetPackages.Add(line[includeStart..includeEnd]);
                    }
                }
            }

            if (nugetPackages.Any(p => p.Contains("EntityFramework"))) frameworks.Add("Entity Framework");
            if (nugetPackages.Any(p => p.Contains("Aspire"))) frameworks.Add(".NET Aspire");
            if (nugetPackages.Any(p => p.Contains("SignalR"))) services.Add("SignalR");
        }

        return new DiagramAnalysis(
            frameworks.Distinct().ToList(),
            services.Distinct().ToList(),
            databases.Distinct().ToList(),
            frontendLibs.Distinct().ToList(),
            buildTools.Distinct().ToList());
    }

    private static string BuildDiagramInstruction(
        string diagramType, string scope, string detailLevel,
        bool includeExternal, DiagramAnalysis analysis, string projectTitle)
    {
        var parts = new List<string>
        {
            $"Generate a Mermaid {GetDiagramTypeName(diagramType)} diagram for '{projectTitle}'.",
            $"Scope: {scope}. Detail level: {detailLevel}."
        };

        if (analysis.Frameworks.Count > 0)
            parts.Add($"Detected frameworks: {string.Join(", ", analysis.Frameworks)}.");
        if (analysis.Services.Count > 0)
            parts.Add($"Detected services: {string.Join(", ", analysis.Services)}.");
        if (analysis.Databases.Count > 0)
            parts.Add($"Detected databases: {string.Join(", ", analysis.Databases)}.");
        if (analysis.FrontendLibs.Count > 0)
            parts.Add($"Frontend libraries: {string.Join(", ", analysis.FrontendLibs)}.");
        if (analysis.BuildTools.Count > 0)
            parts.Add($"Build/CI tools: {string.Join(", ", analysis.BuildTools)}.");

        if (includeExternal)
            parts.Add("Include external services, APIs, and third-party integrations in the diagram.");
        else
            parts.Add("Focus only on internal components; exclude external services.");

        parts.Add($"Use Mermaid {GetMermaidDirective(diagramType)} syntax.");
        parts.Add("Return ONLY valid Mermaid markup wrapped in ```mermaid code fences, ready to render.");
        parts.Add("Use clear, readable labels. Group related components using subgraph where appropriate.");

        return string.Join(" ", parts);
    }

    private static string GetDiagramTypeName(string diagramType) => diagramType switch
    {
        "architecture" => "architecture / system design",
        "stack" => "technology stack",
        "dependencies" => "dependency graph",
        "ci-cd" => "CI/CD pipeline flow",
        "entity-relationship" => "entity-relationship (ER)",
        "sequence" => "sequence",
        "class" => "class",
        _ => diagramType
    };

    private static string GetMermaidDirective(string diagramType) => diagramType switch
    {
        "architecture" => "graph TD (top-down flowchart)",
        "stack" => "graph TD (top-down flowchart with layers)",
        "dependencies" => "graph LR (left-right dependency flow)",
        "ci-cd" => "graph LR (left-right pipeline flow)",
        "entity-relationship" => "erDiagram",
        "sequence" => "sequenceDiagram",
        "class" => "classDiagram",
        _ => "graph TD"
    };

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

            return string.Join("\n", tree.EnumerateArray()
                .Where(e => e.TryGetProperty("path", out _))
                .Select(e => e.GetProperty("path").GetString())
                .Where(p => p is not null)
                .Take(500));
        }
        catch
        {
            return null;
        }
    }

    private async Task<string?> FetchFileAsync(string repo, string path, string userId, CancellationToken cancellationToken)
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
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github.raw+json"));

            var response = await client.GetAsync(
                $"https://api.github.com/repos/{repo}/contents/{path}",
                cancellationToken);

            if (!response.IsSuccessStatusCode)
                return null;

            return await response.Content.ReadAsStringAsync(cancellationToken);
        }
        catch
        {
            return null;
        }
    }

    private record DiagramAnalysis(
        List<string> Frameworks,
        List<string> Services,
        List<string> Databases,
        List<string> FrontendLibs,
        List<string> BuildTools);
}
