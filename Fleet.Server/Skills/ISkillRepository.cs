using Fleet.Server.Data.Entities;

namespace Fleet.Server.Skills;

public interface ISkillRepository
{
    Task<IReadOnlyList<PromptSkill>> GetUserSkillsAsync(int userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PromptSkill>> GetProjectSkillsAsync(int userId, string projectId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PromptSkill>> GetEnabledPromptSkillsAsync(int userId, string? projectId, CancellationToken cancellationToken = default);
    Task<PromptSkill?> GetUserSkillAsync(int userId, int skillId, CancellationToken cancellationToken = default);
    Task<PromptSkill?> GetProjectSkillAsync(int userId, string projectId, int skillId, CancellationToken cancellationToken = default);
    Task<PromptSkill> AddAsync(PromptSkill skill, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
    Task DeleteAsync(PromptSkill skill, CancellationToken cancellationToken = default);
}
