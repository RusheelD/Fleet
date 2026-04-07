using Fleet.Server.Models;

namespace Fleet.Server.Mcp;

public interface IMcpServerService
{
    Task<IReadOnlyList<McpServerDto>> GetServersAsync(int userId);
    Task<IReadOnlyList<McpServerTemplateDto>> GetBuiltInTemplatesAsync();
    Task<McpServerDto> CreateAsync(int userId, UpsertMcpServerRequest request);
    Task<McpServerDto> UpdateAsync(int userId, int id, UpsertMcpServerRequest request);
    Task DeleteAsync(int userId, int id);
    Task<McpServerValidationResultDto> ValidateAsync(int userId, int id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<McpServerRuntimeConfig>> GetEnabledRuntimeConfigsAsync(int userId);
}
