namespace Fleet.Server.Mcp;

/// <summary>
/// Configuration section for system-level MCP servers that are automatically
/// available to all users and agents without per-user setup.
/// Configured via appsettings.json under the "McpServers" section.
/// </summary>
public class SystemMcpServersOptions
{
    public const string SectionName = "McpServers";

    /// <summary>
    /// System-level MCP server definitions. Each server is available to every user.
    /// </summary>
    public SystemMcpServerConfig[] Servers { get; set; } = [];
}

public class SystemMcpServerConfig
{
    /// <summary>Unique name for this system MCP server.</summary>
    public string Name { get; set; } = "";

    /// <summary>Human-readable description.</summary>
    public string Description { get; set; } = "";

    /// <summary>Transport type: "stdio" or "http".</summary>
    public string TransportType { get; set; } = "stdio";

    /// <summary>Command to execute for stdio servers.</summary>
    public string? Command { get; set; }

    /// <summary>Command-line arguments.</summary>
    public string[] Arguments { get; set; } = [];

    /// <summary>Working directory for stdio servers.</summary>
    public string? WorkingDirectory { get; set; }

    /// <summary>HTTP endpoint for HTTP servers.</summary>
    public string? Endpoint { get; set; }

    /// <summary>Environment variables to pass to the process.</summary>
    public Dictionary<string, string> EnvironmentVariables { get; set; } = new();

    /// <summary>HTTP headers for HTTP servers.</summary>
    public Dictionary<string, string> Headers { get; set; } = new();

    /// <summary>Whether this system server is enabled. Default: true.</summary>
    public bool Enabled { get; set; } = true;
}
