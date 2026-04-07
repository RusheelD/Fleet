using System.Text;
using System.Text.RegularExpressions;
using Fleet.Server.Data.Entities;
using Fleet.Server.Models;

namespace Fleet.Server.Skills;

public partial class SkillService(ISkillRepository repository, ILogger<SkillService> logger) : ISkillService
{
    private const int CatalogLimit = 24;
    private const int SelectedSkillLimit = 3;
    private const int SelectedSkillContentLimit = 1_800;

    private static readonly IReadOnlyList<PromptSkillTemplateDto> BuiltInTemplates =
    [
        new(
            "prd-to-backlog",
            "PRD to Backlog",
            "Turn product ideas, PRDs, or stakeholder asks into a clean implementation backlog.",
            "Use when the user has a feature brief, opportunity statement, or PRD and wants it broken into milestones, work items, risks, and sequencing.",
            """
            Convert the incoming product brief into a delivery-ready backlog.
            Focus on:
            - Outcomes first, not implementation trivia.
            - Clear milestone structure and dependency order.
            - Acceptance criteria that can be verified.
            - Explicit risks, open questions, and assumptions.
            - Work items that are appropriately sized for parallel execution when possible.
            """),
        new(
            "bug-triage",
            "Bug Triage",
            "Structure a production issue into severity, reproduction, impact, and next actions.",
            "Use when the user is dealing with a bug report, incident, flaky behavior, or support escalation.",
            """
            Triage the issue with a calm operations mindset.
            Produce:
            - Severity and customer impact.
            - Reproduction path and missing facts.
            - Most likely causes and validation steps.
            - Immediate mitigation options.
            - Follow-up work items for fix, prevention, and communication.
            """),
        new(
            "release-readiness",
            "Release Readiness",
            "Assess whether a feature or project is genuinely ready to launch.",
            "Use when the user asks about launch planning, go-live readiness, rollout risk, or operational confidence.",
            """
            Evaluate launch readiness across product, engineering, and operations.
            Check:
            - Scope completeness and unresolved blockers.
            - Testing, migration, and rollback confidence.
            - Monitoring, alerts, and ownership coverage.
            - Documentation, stakeholder communication, and support readiness.
            - Remaining launch risks with explicit recommendations.
            """),
        new(
            "technical-spike",
            "Technical Spike",
            "Shape an investigation into clear research questions, experiments, and decision checkpoints.",
            "Use when the user is exploring architecture options, unknowns, feasibility, or implementation strategy.",
            """
            Treat the request as a bounded research spike.
            Deliver:
            - The specific decision that needs to be made.
            - The key unknowns and what would de-risk them.
            - Experiments or evidence to gather.
            - Comparison criteria for options.
            - Exit criteria and the recommendation format.
            """),
        new(
            "qa-test-plan",
            "QA Test Plan",
            "Translate requirements into test coverage, scenarios, and acceptance checks.",
            "Use when the user wants a test plan, regression checklist, or acceptance test suite for a feature.",
            """
            Build a test plan that is practical and coverage-driven.
            Include:
            - Core user journeys and happy paths.
            - Edge cases, failure states, and regression hotspots.
            - Data/setup requirements and environment assumptions.
            - Automation candidates versus manual checks.
            - A concise sign-off checklist.
            """),
        new(
            "stakeholder-update",
            "Stakeholder Update",
            "Summarize project status for leadership, partners, or cross-functional stakeholders.",
            "Use when the user needs a status summary, decision memo, launch update, or executive-friendly project readout.",
            """
            Write for clarity, not drama.
            Emphasize:
            - Current status and recent progress.
            - What changed, what is blocked, and why it matters.
            - Decisions needed, owners, and dates.
            - Risks with concrete mitigation plans.
            - The next meaningful checkpoint.
            """),
    ];

    public Task<IReadOnlyList<PromptSkillTemplateDto>> GetTemplatesAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<PromptSkillTemplateDto>>(BuiltInTemplates);

    public async Task<IReadOnlyList<PromptSkillDto>> GetUserSkillsAsync(int userId, CancellationToken cancellationToken = default)
        => (await repository.GetUserSkillsAsync(userId, cancellationToken))
            .Select(ToDto)
            .ToList();

    public async Task<IReadOnlyList<PromptSkillDto>> GetProjectSkillsAsync(int userId, string projectId, CancellationToken cancellationToken = default)
        => (await repository.GetProjectSkillsAsync(userId, projectId, cancellationToken))
            .Select(ToDto)
            .ToList();

    public Task<PromptSkillDto> CreateUserSkillAsync(int userId, UpsertPromptSkillRequest request, CancellationToken cancellationToken = default)
        => CreateAsync(userId, null, request, cancellationToken);

    public async Task<PromptSkillDto> UpdateUserSkillAsync(int userId, int skillId, UpsertPromptSkillRequest request, CancellationToken cancellationToken = default)
    {
        var skill = await repository.GetUserSkillAsync(userId, skillId, cancellationToken)
            ?? throw new KeyNotFoundException("Skill not found.");
        return await UpdateAsync(skill, request, cancellationToken);
    }

    public async Task DeleteUserSkillAsync(int userId, int skillId, CancellationToken cancellationToken = default)
    {
        var skill = await repository.GetUserSkillAsync(userId, skillId, cancellationToken)
            ?? throw new KeyNotFoundException("Skill not found.");
        await repository.DeleteAsync(skill, cancellationToken);
    }

    public Task<PromptSkillDto> CreateProjectSkillAsync(int userId, string projectId, UpsertPromptSkillRequest request, CancellationToken cancellationToken = default)
        => CreateAsync(userId, projectId, request, cancellationToken);

    public async Task<PromptSkillDto> UpdateProjectSkillAsync(int userId, string projectId, int skillId, UpsertPromptSkillRequest request, CancellationToken cancellationToken = default)
    {
        var skill = await repository.GetProjectSkillAsync(userId, projectId, skillId, cancellationToken)
            ?? throw new KeyNotFoundException("Skill not found.");
        return await UpdateAsync(skill, request, cancellationToken);
    }

    public async Task DeleteProjectSkillAsync(int userId, string projectId, int skillId, CancellationToken cancellationToken = default)
    {
        var skill = await repository.GetProjectSkillAsync(userId, projectId, skillId, cancellationToken)
            ?? throw new KeyNotFoundException("Skill not found.");
        await repository.DeleteAsync(skill, cancellationToken);
    }

    public async Task<string> BuildPromptBlockAsync(int userId, string? projectId, string? query, CancellationToken cancellationToken = default)
    {
        var customSkills = await repository.GetEnabledPromptSkillsAsync(userId, projectId, cancellationToken);
        var candidates = BuiltInTemplates.Select(template => CreateCandidate(template))
            .Concat(customSkills.Select(skill => CreateCandidate(skill)))
            .ToList();

        if (candidates.Count == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        builder.AppendLine("## Playbooks");
        builder.AppendLine("These playbooks are reusable workflows. If one is relevant, follow it closely and adapt its output to the user's context.");
        builder.AppendLine();
        builder.AppendLine("### Available Playbooks");

        foreach (var candidate in candidates.Take(CatalogLimit))
        {
            builder.AppendLine($"- {candidate.Name} [{candidate.Scope}] - {candidate.Description}");
        }

        if (candidates.Count > CatalogLimit)
        {
            builder.AppendLine($"- ... and {candidates.Count - CatalogLimit} more playbooks.");
        }

        var selected = SelectRelevantSkills(candidates, query, projectId)
            .Take(SelectedSkillLimit)
            .ToList();
        if (selected.Count == 0)
        {
            return builder.ToString().TrimEnd();
        }

        builder.AppendLine();
        builder.AppendLine("### Relevant Playbooks");
        foreach (var skill in selected)
        {
            builder.AppendLine();
            builder.AppendLine($"#### {skill.Name} [{skill.Scope}]");
            builder.AppendLine(skill.Description);
            builder.AppendLine($"When to use: {skill.WhenToUse}");
            builder.AppendLine(TrimForPrompt(skill.Content, SelectedSkillContentLimit));
        }

        return builder.ToString().TrimEnd();
    }

    private async Task<PromptSkillDto> CreateAsync(int userId, string? projectId, UpsertPromptSkillRequest request, CancellationToken cancellationToken)
    {
        var normalized = NormalizeRequest(request);
        var skill = new PromptSkill
        {
            UserProfileId = userId,
            ProjectId = string.IsNullOrWhiteSpace(projectId) ? null : projectId,
            Name = normalized.Name,
            Description = normalized.Description,
            WhenToUse = normalized.WhenToUse,
            Content = normalized.Content,
            Enabled = normalized.Enabled,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
        };

        await repository.AddAsync(skill, cancellationToken);
        logger.LogInformation("Created {Scope} playbook '{SkillName}' for user {UserId}", GetScope(skill.ProjectId), skill.Name, userId);
        return ToDto(skill);
    }

    private async Task<PromptSkillDto> UpdateAsync(PromptSkill skill, UpsertPromptSkillRequest request, CancellationToken cancellationToken)
    {
        var normalized = NormalizeRequest(request);
        skill.Name = normalized.Name;
        skill.Description = normalized.Description;
        skill.WhenToUse = normalized.WhenToUse;
        skill.Content = normalized.Content;
        skill.Enabled = normalized.Enabled;
        skill.UpdatedAtUtc = DateTime.UtcNow;

        await repository.SaveChangesAsync(cancellationToken);
        return ToDto(skill);
    }

    private static UpsertPromptSkillRequest NormalizeRequest(UpsertPromptSkillRequest request)
    {
        var name = request.Name?.Trim() ?? string.Empty;
        var description = request.Description?.Trim() ?? string.Empty;
        var whenToUse = request.WhenToUse?.Trim() ?? string.Empty;
        var content = request.Content?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new InvalidOperationException("Playbook name is required.");
        }

        if (string.IsNullOrWhiteSpace(description))
        {
            throw new InvalidOperationException("Playbook description is required.");
        }

        if (string.IsNullOrWhiteSpace(whenToUse))
        {
            throw new InvalidOperationException("Playbook usage guidance is required.");
        }

        if (string.IsNullOrWhiteSpace(content))
        {
            throw new InvalidOperationException("Playbook content is required.");
        }

        return request with
        {
            Name = name,
            Description = description,
            WhenToUse = whenToUse,
            Content = content,
        };
    }

    private static IEnumerable<SkillCandidate> SelectRelevantSkills(
        IReadOnlyList<SkillCandidate> candidates,
        string? query,
        string? projectId)
    {
        var tokens = Tokenize(query);
        return candidates
            .Select(candidate => new
            {
                Candidate = candidate,
                Score = Score(candidate, tokens, projectId),
            })
            .Where(item => item.Score > 0)
            .OrderByDescending(item => item.Score)
            .ThenByDescending(item => item.Candidate.Scope == "project")
            .ThenBy(item => item.Candidate.Name)
            .Select(item => item.Candidate);
    }

    private static int Score(SkillCandidate candidate, HashSet<string> queryTokens, string? projectId)
    {
        var score = candidate.Scope == "project" && !string.IsNullOrWhiteSpace(projectId) ? 20 : 0;
        if (queryTokens.Count == 0)
        {
            return score;
        }

        var searchable = $"{candidate.Name} {candidate.Description} {candidate.WhenToUse} {candidate.Content}";
        var matchedTokens = queryTokens.Count(token => searchable.Contains(token, StringComparison.OrdinalIgnoreCase));
        score += matchedTokens * 25;

        if (matchedTokens > 0 && searchable.Contains(queryTokens.First(), StringComparison.OrdinalIgnoreCase))
        {
            score += 10;
        }

        return score;
    }

    private static HashSet<string> Tokenize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        return [.. WordRegex().Matches(text)
            .Select(match => match.Value.ToLowerInvariant())
            .Where(token => token.Length >= 3)];
    }

    private static string GetScope(string? projectId)
        => string.IsNullOrWhiteSpace(projectId) ? "personal" : "project";

    private static PromptSkillDto ToDto(PromptSkill skill)
        => new(
            skill.Id,
            skill.Name,
            skill.Description,
            skill.WhenToUse,
            skill.Content,
            skill.Enabled,
            GetScope(skill.ProjectId),
            skill.ProjectId,
            skill.CreatedAtUtc,
            skill.UpdatedAtUtc);

    private static string TrimForPrompt(string content, int maxLength)
    {
        if (content.Length <= maxLength)
        {
            return content;
        }

        return $"{content[..maxLength].TrimEnd()}\n\n[Playbook content truncated]";
    }

    private static SkillCandidate CreateCandidate(PromptSkillTemplateDto template)
        => new(template.Name, template.Description, template.WhenToUse, template.Content, "built-in");

    private static SkillCandidate CreateCandidate(PromptSkill skill)
        => new(skill.Name, skill.Description, skill.WhenToUse, skill.Content, GetScope(skill.ProjectId));

    private sealed record SkillCandidate(
        string Name,
        string Description,
        string WhenToUse,
        string Content,
        string Scope);

    [GeneratedRegex("[A-Za-z0-9_\\-/]+", RegexOptions.Compiled)]
    private static partial Regex WordRegex();
}
