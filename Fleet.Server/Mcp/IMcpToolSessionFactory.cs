using Fleet.Server.Agents;
using Fleet.Server.LLM;

namespace Fleet.Server.Mcp;

public interface IMcpToolSessionFactory
{
    Task<IMcpToolSession> CreateForChatAsync(int userId, bool includeWriteTools, CancellationToken cancellationToken = default);
    Task<IMcpToolSession> CreateForAgentAsync(string userId, AgentRole role, CancellationToken cancellationToken = default);
}

public interface IMcpToolSession : IAsyncDisposable
{
    IReadOnlyList<LLMToolDefinition> Definitions { get; }
    bool HasTool(string toolName);
    bool IsReadOnly(string toolName);
    Task<string> ExecuteAsync(string toolName, string argumentsJson, CancellationToken cancellationToken = default);
}
