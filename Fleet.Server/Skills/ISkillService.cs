using Fleet.Server.Models;

namespace Fleet.Server.Skills;

public interface ISkillService
{
    Task<IReadOnlyList<PromptSkillTemplateDto>> GetTemplatesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PromptSkillDto>> GetUserSkillsAsync(int userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PromptSkillDto>> GetProjectSkillsAsync(int userId, string projectId, CancellationToken cancellationToken = default);
    Task<PromptSkillDto> CreateUserSkillAsync(int userId, UpsertPromptSkillRequest request, CancellationToken cancellationToken = default);
    Task<PromptSkillDto> UpdateUserSkillAsync(int userId, int skillId, UpsertPromptSkillRequest request, CancellationToken cancellationToken = default);
    Task DeleteUserSkillAsync(int userId, int skillId, CancellationToken cancellationToken = default);
    Task<PromptSkillDto> CreateProjectSkillAsync(int userId, string projectId, UpsertPromptSkillRequest request, CancellationToken cancellationToken = default);
    Task<PromptSkillDto> UpdateProjectSkillAsync(int userId, string projectId, int skillId, UpsertPromptSkillRequest request, CancellationToken cancellationToken = default);
    Task DeleteProjectSkillAsync(int userId, string projectId, int skillId, CancellationToken cancellationToken = default);
    Task<string> BuildPromptBlockAsync(int userId, string? projectId, string? query, CancellationToken cancellationToken = default);
}
