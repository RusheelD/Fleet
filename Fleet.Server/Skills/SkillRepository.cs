using Fleet.Server.Data;
using Fleet.Server.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Fleet.Server.Skills;

public class SkillRepository(FleetDbContext context) : ISkillRepository
{
    public async Task<IReadOnlyList<PromptSkill>> GetUserSkillsAsync(int userId, CancellationToken cancellationToken = default)
        => await context.PromptSkills
            .AsNoTracking()
            .Where(skill => skill.UserProfileId == userId && skill.ProjectId == null)
            .OrderByDescending(skill => skill.Enabled)
            .ThenByDescending(skill => skill.UpdatedAtUtc)
            .ThenBy(skill => skill.Name)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<PromptSkill>> GetProjectSkillsAsync(int userId, string projectId, CancellationToken cancellationToken = default)
        => await context.PromptSkills
            .AsNoTracking()
            .Where(skill => skill.UserProfileId == userId && skill.ProjectId == projectId)
            .OrderByDescending(skill => skill.Enabled)
            .ThenByDescending(skill => skill.UpdatedAtUtc)
            .ThenBy(skill => skill.Name)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<PromptSkill>> GetEnabledPromptSkillsAsync(int userId, string? projectId, CancellationToken cancellationToken = default)
        => await context.PromptSkills
            .AsNoTracking()
            .Where(skill =>
                skill.UserProfileId == userId &&
                skill.Enabled &&
                (skill.ProjectId == null || skill.ProjectId == projectId))
            .OrderByDescending(skill => skill.ProjectId == projectId)
            .ThenByDescending(skill => skill.UpdatedAtUtc)
            .ThenBy(skill => skill.Name)
            .ToListAsync(cancellationToken);

    public Task<PromptSkill?> GetUserSkillAsync(int userId, int skillId, CancellationToken cancellationToken = default)
        => context.PromptSkills
            .FirstOrDefaultAsync(skill => skill.UserProfileId == userId && skill.ProjectId == null && skill.Id == skillId, cancellationToken);

    public Task<PromptSkill?> GetProjectSkillAsync(int userId, string projectId, int skillId, CancellationToken cancellationToken = default)
        => context.PromptSkills
            .FirstOrDefaultAsync(skill => skill.UserProfileId == userId && skill.ProjectId == projectId && skill.Id == skillId, cancellationToken);

    public async Task<PromptSkill> AddAsync(PromptSkill skill, CancellationToken cancellationToken = default)
    {
        await context.PromptSkills.AddAsync(skill, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
        return skill;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => context.SaveChangesAsync(cancellationToken);

    public async Task DeleteAsync(PromptSkill skill, CancellationToken cancellationToken = default)
    {
        context.PromptSkills.Remove(skill);
        await context.SaveChangesAsync(cancellationToken);
    }
}
