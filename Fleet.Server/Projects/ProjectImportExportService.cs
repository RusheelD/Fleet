using Fleet.Server.Auth;
using Fleet.Server.Data;
using Fleet.Server.Data.Entities;
using Fleet.Server.Models;
using Microsoft.EntityFrameworkCore;

namespace Fleet.Server.Projects;

public class ProjectImportExportService(
    FleetDbContext db,
    IAuthService authService,
    ILogger<ProjectImportExportService> logger) : IProjectImportExportService
{
    private const string ProjectsFormat = "fleet.projects+workitems";
    private const string WorkItemsFormat = "fleet.workitems";
    private const int SchemaVersion = 1;
    private const string DefaultBranchPattern = "fleet/{workItemNumber}-{slug}";

    public async Task<ProjectsExportFileDto> ExportProjectsAsync(CancellationToken cancellationToken = default)
    {
        var ownerId = await GetOwnerIdAsync();
        var projects = await db.Projects
            .AsNoTracking()
            .Where(p => p.OwnerId == ownerId)
            .OrderBy(p => p.Title)
            .ToListAsync(cancellationToken);

        if (projects.Count == 0)
        {
            return new ProjectsExportFileDto(
                ProjectsFormat,
                SchemaVersion,
                DateTime.UtcNow.ToString("o"),
                []);
        }

        var projectIds = projects.Select(p => p.Id).ToArray();
        var levels = await db.WorkItemLevels
            .AsNoTracking()
            .Where(level => projectIds.Contains(level.ProjectId))
            .OrderBy(level => level.Ordinal)
            .ThenBy(level => level.Id)
            .ToListAsync(cancellationToken);
        var workItems = await db.WorkItems
            .AsNoTracking()
            .Where(item => projectIds.Contains(item.ProjectId))
            .OrderBy(item => item.WorkItemNumber)
            .ThenBy(item => item.Id)
            .ToListAsync(cancellationToken);

        var levelsByProjectId = levels
            .GroupBy(level => level.ProjectId)
            .ToDictionary(group => group.Key, group => group.ToList());
        var workItemsByProjectId = workItems
            .GroupBy(item => item.ProjectId)
            .ToDictionary(group => group.Key, group => group.ToList());

        var exportedProjects = new List<ProjectExportDto>(projects.Count);
        foreach (var project in projects)
        {
            var projectLevels = levelsByProjectId.GetValueOrDefault(project.Id, []);
            var levelNameById = projectLevels.ToDictionary(level => level.Id, level => level.Name);
            var projectWorkItems = workItemsByProjectId.GetValueOrDefault(project.Id, []);
            var workItemNumberById = projectWorkItems.ToDictionary(item => item.Id, item => item.WorkItemNumber);

            exportedProjects.Add(new ProjectExportDto(
                Slug: project.Slug,
                Title: project.Title,
                Description: project.Description,
                Repo: project.Repo,
                BranchPattern: NormalizeBranchPattern(project.BranchPattern),
                CommitAuthorMode: NormalizeCommitAuthorMode(project.CommitAuthorMode),
                CommitAuthorName: NormalizeOptional(project.CommitAuthorName),
                CommitAuthorEmail: NormalizeOptional(project.CommitAuthorEmail),
                WorkItemLevels: [.. projectLevels.Select(MapLevelToExportDto)],
                WorkItems: [.. projectWorkItems.Select(item => MapWorkItemToExportDto(item, levelNameById, workItemNumberById))]));
        }

        return new ProjectsExportFileDto(
            ProjectsFormat,
            SchemaVersion,
            DateTime.UtcNow.ToString("o"),
            [.. exportedProjects]);
    }

    public async Task<ProjectsImportResultDto> ImportProjectsAsync(
        ProjectsExportFileDto payload,
        CancellationToken cancellationToken = default)
    {
        ValidateProjectsPayload(payload);

        var ownerId = await GetOwnerIdAsync();
        var importedProjectIds = new List<string>();
        var totalImportedLevels = 0;
        var totalImportedWorkItems = 0;

        foreach (var importedProject in payload.Projects)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var title = NormalizeProjectTitle(importedProject.Title);
            var slug = await ResolveUniqueSlugAsync(
                ownerId,
                importedProject.Slug ?? importedProject.Title,
                cancellationToken);

            var project = new Project
            {
                Id = Guid.NewGuid().ToString("N")[..8],
                OwnerId = ownerId,
                Title = title,
                Slug = slug,
                Description = importedProject.Description?.Trim() ?? string.Empty,
                Repo = importedProject.Repo?.Trim() ?? string.Empty,
                LastActivity = DateTime.UtcNow.ToString("o"),
                BranchPattern = NormalizeBranchPattern(importedProject.BranchPattern),
                CommitAuthorMode = NormalizeCommitAuthorMode(importedProject.CommitAuthorMode),
                CommitAuthorName = NormalizeOptional(importedProject.CommitAuthorName),
                CommitAuthorEmail = NormalizeOptional(importedProject.CommitAuthorEmail),
                WorkItemSummary = new WorkItemSummary(),
                AgentSummary = new AgentSummary(),
            };

            db.Projects.Add(project);
            await db.SaveChangesAsync(cancellationToken);

            var (levelIdByName, importedLevels) = await EnsureLevelsAsync(
                project.Id,
                importedProject.WorkItemLevels,
                cancellationToken);
            totalImportedLevels += importedLevels;

            totalImportedWorkItems += await ImportWorkItemsIntoProjectAsync(
                project.Id,
                importedProject.WorkItems,
                levelIdByName,
                cancellationToken);

            await RefreshProjectSummaryAsync(project.Id, cancellationToken);
            importedProjectIds.Add(project.Id);
        }

        logger.LogInformation(
            "Imported {ProjectCount} project(s) with {WorkItemCount} work item(s) and {LevelCount} level(s)",
            importedProjectIds.Count,
            totalImportedWorkItems,
            totalImportedLevels);

        return new ProjectsImportResultDto(
            importedProjectIds.Count,
            totalImportedWorkItems,
            totalImportedLevels,
            [.. importedProjectIds]);
    }

    public async Task<ProjectWorkItemsExportFileDto?> ExportWorkItemsAsync(
        string projectId,
        CancellationToken cancellationToken = default)
    {
        var ownerId = await GetOwnerIdAsync();
        var project = await db.Projects
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == projectId && p.OwnerId == ownerId, cancellationToken);
        if (project is null)
            return null;

        var levels = await db.WorkItemLevels
            .AsNoTracking()
            .Where(level => level.ProjectId == projectId)
            .OrderBy(level => level.Ordinal)
            .ThenBy(level => level.Id)
            .ToListAsync(cancellationToken);
        var workItems = await db.WorkItems
            .AsNoTracking()
            .Where(item => item.ProjectId == projectId)
            .OrderBy(item => item.WorkItemNumber)
            .ThenBy(item => item.Id)
            .ToListAsync(cancellationToken);

        var levelNameById = levels.ToDictionary(level => level.Id, level => level.Name);
        var workItemNumberById = workItems.ToDictionary(item => item.Id, item => item.WorkItemNumber);

        return new ProjectWorkItemsExportFileDto(
            WorkItemsFormat,
            SchemaVersion,
            DateTime.UtcNow.ToString("o"),
            project.Title,
            project.Repo,
            [.. levels.Select(MapLevelToExportDto)],
            [.. workItems.Select(item => MapWorkItemToExportDto(item, levelNameById, workItemNumberById))]);
    }

    public async Task<WorkItemsImportResultDto> ImportWorkItemsAsync(
        string projectId,
        ProjectWorkItemsExportFileDto payload,
        CancellationToken cancellationToken = default)
    {
        ValidateWorkItemsPayload(payload);

        var ownerId = await GetOwnerIdAsync();
        var project = await db.Projects
            .FirstOrDefaultAsync(p => p.Id == projectId && p.OwnerId == ownerId, cancellationToken);
        if (project is null)
            throw new InvalidOperationException($"Project '{projectId}' was not found.");

        var (levelIdByName, importedLevels) = await EnsureLevelsAsync(
            projectId,
            payload.WorkItemLevels,
            cancellationToken);
        var importedWorkItems = await ImportWorkItemsIntoProjectAsync(
            projectId,
            payload.WorkItems,
            levelIdByName,
            cancellationToken);

        await RefreshProjectSummaryAsync(projectId, cancellationToken);

        logger.LogInformation(
            "Imported {WorkItemCount} work item(s) into project {ProjectId} with {LevelCount} new level(s)",
            importedWorkItems,
            projectId,
            importedLevels);

        return new WorkItemsImportResultDto(
            importedWorkItems,
            importedLevels);
    }

    private async Task<string> GetOwnerIdAsync()
    {
        var userId = await authService.GetCurrentUserIdAsync();
        return userId.ToString();
    }

    private static WorkItemLevelExportDto MapLevelToExportDto(WorkItemLevel level)
        => new(
            level.Name,
            level.IconName,
            level.Color,
            level.Ordinal,
            level.IsDefault);

    private static WorkItemExportDto MapWorkItemToExportDto(
        WorkItem item,
        IReadOnlyDictionary<int, string> levelNameById,
        IReadOnlyDictionary<int, int> workItemNumberById)
    {
        int? parentWorkItemNumber = null;
        if (item.ParentId is int parentId &&
            workItemNumberById.TryGetValue(parentId, out var mappedParentNumber))
        {
            parentWorkItemNumber = mappedParentNumber;
        }

        string? levelName = null;
        if (item.LevelId is int levelId &&
            levelNameById.TryGetValue(levelId, out var mappedLevelName))
        {
            levelName = mappedLevelName;
        }

        return new WorkItemExportDto(
            item.WorkItemNumber,
            item.Title,
            item.State,
            item.Priority,
            item.Difficulty,
            item.AssignedTo,
            [.. item.Tags],
            item.IsAI,
            item.Description,
            parentWorkItemNumber,
            levelName,
            NormalizeAssignmentMode(item.AssignmentMode, item.IsAI),
            NormalizeAssignedAgentCount(item.AssignedAgentCount),
            item.AcceptanceCriteria,
            item.LinkedPullRequestUrl,
            item.LastObservedPullRequestState,
            item.LastObservedPullRequestUrl);
    }

    private static void ValidateProjectsPayload(ProjectsExportFileDto payload)
    {
        if (payload is null)
            throw new InvalidOperationException("Import payload is required.");
        if (!string.Equals(payload.Format, ProjectsFormat, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Unsupported projects import format.");
        if (payload.SchemaVersion != SchemaVersion)
            throw new InvalidOperationException($"Unsupported projects import schema version: {payload.SchemaVersion}.");
        if (payload.Projects is null)
            throw new InvalidOperationException("Projects payload is missing.");
    }

    private static void ValidateWorkItemsPayload(ProjectWorkItemsExportFileDto payload)
    {
        if (payload is null)
            throw new InvalidOperationException("Import payload is required.");
        if (!string.Equals(payload.Format, WorkItemsFormat, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Unsupported work-items import format.");
        if (payload.SchemaVersion != SchemaVersion)
            throw new InvalidOperationException($"Unsupported work-items import schema version: {payload.SchemaVersion}.");
        if (payload.WorkItems is null)
            throw new InvalidOperationException("Work-items payload is missing.");
    }

    private static string NormalizeProjectTitle(string? value)
        => string.IsNullOrWhiteSpace(value) ? "Imported Project" : value.Trim();

    private static string NormalizeBranchPattern(string? value)
        => string.IsNullOrWhiteSpace(value) ? DefaultBranchPattern : value.Trim();

    private static string NormalizeCommitAuthorMode(string? value)
        => string.Equals(value, "custom", StringComparison.OrdinalIgnoreCase) ? "custom" : "fleet";

    private static string NormalizeAssignmentMode(string? value, bool isAi)
        => value?.ToLowerInvariant() switch
        {
            "auto" => "auto",
            "manual" => "manual",
            _ => isAi ? "auto" : "manual",
        };

    private static int? NormalizeAssignedAgentCount(int? value)
    {
        if (value is null || value <= 0)
            return null;
        return Math.Min(value.Value, 10);
    }

    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private async Task<string> ResolveUniqueSlugAsync(string ownerId, string? source, CancellationToken cancellationToken)
    {
        var baseSlug = SlugHelper.GenerateSlug(source ?? string.Empty);
        if (string.IsNullOrWhiteSpace(baseSlug))
            baseSlug = "imported-project";

        var candidate = baseSlug;
        var suffix = 2;

        while (await db.Projects
            .AsNoTracking()
            .AnyAsync(project => project.OwnerId == ownerId && project.Slug == candidate, cancellationToken))
        {
            candidate = $"{baseSlug}-{suffix}";
            suffix++;
        }

        return candidate;
    }

    private async Task<(Dictionary<string, int> LevelIdByName, int ImportedLevelCount)> EnsureLevelsAsync(
        string projectId,
        IEnumerable<WorkItemLevelExportDto>? importedLevels,
        CancellationToken cancellationToken)
    {
        var levels = await db.WorkItemLevels
            .Where(level => level.ProjectId == projectId)
            .OrderBy(level => level.Ordinal)
            .ThenBy(level => level.Id)
            .ToListAsync(cancellationToken);

        var levelIdByName = levels.ToDictionary(level => level.Name, level => level.Id, StringComparer.OrdinalIgnoreCase);
        var importedCount = 0;

        if (importedLevels is not null)
        {
            foreach (var level in importedLevels
                         .Where(level => !string.IsNullOrWhiteSpace(level.Name))
                         .OrderBy(level => level.Ordinal))
            {
                var normalizedName = level.Name.Trim();
                if (levelIdByName.ContainsKey(normalizedName))
                    continue;

                var entity = new WorkItemLevel
                {
                    ProjectId = projectId,
                    Name = normalizedName,
                    IconName = string.IsNullOrWhiteSpace(level.IconName) ? "task-list" : level.IconName.Trim(),
                    Color = string.IsNullOrWhiteSpace(level.Color) ? "#8A8886" : level.Color.Trim(),
                    Ordinal = level.Ordinal,
                    IsDefault = level.IsDefault,
                };

                db.WorkItemLevels.Add(entity);
                levels.Add(entity);
                importedCount++;
            }

            if (importedCount > 0)
                await db.SaveChangesAsync(cancellationToken);

            levelIdByName = levels.ToDictionary(level => level.Name, level => level.Id, StringComparer.OrdinalIgnoreCase);
        }

        return (levelIdByName, importedCount);
    }

    private async Task<int> ImportWorkItemsIntoProjectAsync(
        string projectId,
        IEnumerable<WorkItemExportDto>? importedWorkItems,
        IReadOnlyDictionary<string, int> levelIdByName,
        CancellationToken cancellationToken)
    {
        if (importedWorkItems is null)
            return 0;

        var sourceItems = importedWorkItems.ToList();
        if (sourceItems.Count == 0)
            return 0;

        var existingNumbers = await db.WorkItems
            .AsNoTracking()
            .Where(item => item.ProjectId == projectId)
            .Select(item => item.WorkItemNumber)
            .ToListAsync(cancellationToken);

        var usedNumbers = new HashSet<int>(existingNumbers);
        var nextWorkItemNumber = usedNumbers.Count == 0 ? 1 : usedNumbers.Max() + 1;

        var sourceToTargetNumber = new Dictionary<int, int>();
        var created = new List<(WorkItemExportDto Source, WorkItem Entity)>(sourceItems.Count);

        foreach (var source in sourceItems.OrderBy(item => item.WorkItemNumber).ThenBy(item => item.Title))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var normalizedSourceNumber = source.WorkItemNumber > 0 ? source.WorkItemNumber : nextWorkItemNumber;
            var targetNumber = normalizedSourceNumber;
            if (usedNumbers.Contains(targetNumber))
            {
                targetNumber = nextWorkItemNumber;
                while (usedNumbers.Contains(targetNumber))
                    targetNumber++;
            }

            usedNumbers.Add(targetNumber);
            nextWorkItemNumber = Math.Max(nextWorkItemNumber, targetNumber + 1);
            sourceToTargetNumber.TryAdd(source.WorkItemNumber, targetNumber);

            var levelId = ResolveLevelId(source.LevelName, levelIdByName);
            var entity = new WorkItem
            {
                ProjectId = projectId,
                WorkItemNumber = targetNumber,
                Title = string.IsNullOrWhiteSpace(source.Title) ? $"Imported item {targetNumber}" : source.Title.Trim(),
                State = string.IsNullOrWhiteSpace(source.State) ? "New" : source.State.Trim(),
                Priority = Math.Clamp(source.Priority, 1, 4),
                Difficulty = Math.Clamp(source.Difficulty, 1, 5),
                AssignedTo = source.AssignedTo?.Trim() ?? string.Empty,
                Tags = [.. (source.Tags ?? []).Where(tag => !string.IsNullOrWhiteSpace(tag)).Select(tag => tag.Trim()).Distinct(StringComparer.OrdinalIgnoreCase)],
                IsAI = source.IsAI,
                Description = source.Description?.Trim() ?? string.Empty,
                LevelId = levelId,
                AssignmentMode = NormalizeAssignmentMode(source.AssignmentMode, source.IsAI),
                AssignedAgentCount = NormalizeAssignedAgentCount(source.AssignedAgentCount),
                AcceptanceCriteria = source.AcceptanceCriteria?.Trim() ?? string.Empty,
                LinkedPullRequestUrl = NormalizeOptional(source.LinkedPullRequestUrl),
                LastObservedPullRequestState = NormalizeOptional(source.LastObservedPullRequestState)?.ToLowerInvariant(),
                LastObservedPullRequestUrl = NormalizeOptional(source.LastObservedPullRequestUrl),
            };

            db.WorkItems.Add(entity);
            created.Add((source, entity));
        }

        await db.SaveChangesAsync(cancellationToken);

        var entitiesByNumber = created.ToDictionary(item => item.Entity.WorkItemNumber, item => item.Entity);
        foreach (var (source, entity) in created)
        {
            if (source.ParentWorkItemNumber is not int sourceParentNumber)
                continue;

            if (!sourceToTargetNumber.TryGetValue(sourceParentNumber, out var mappedParentNumber))
                continue;

            if (!entitiesByNumber.TryGetValue(mappedParentNumber, out var parentEntity))
                continue;

            if (parentEntity.Id == entity.Id)
                continue;

            entity.ParentId = parentEntity.Id;
        }

        await db.SaveChangesAsync(cancellationToken);
        return created.Count;
    }

    private static int? ResolveLevelId(string? levelName, IReadOnlyDictionary<string, int> levelIdByName)
    {
        if (string.IsNullOrWhiteSpace(levelName))
            return null;

        return levelIdByName.TryGetValue(levelName.Trim(), out var levelId)
            ? levelId
            : null;
    }

    private async Task RefreshProjectSummaryAsync(string projectId, CancellationToken cancellationToken)
    {
        var project = await db.Projects.FirstOrDefaultAsync(p => p.Id == projectId, cancellationToken);
        if (project is null)
            return;

        var states = await db.WorkItems
            .AsNoTracking()
            .Where(item => item.ProjectId == projectId)
            .Select(item => item.State)
            .ToListAsync(cancellationToken);

        project.WorkItemSummary.Total = states.Count;
        project.WorkItemSummary.Active = states.Count(IsActiveState);
        project.WorkItemSummary.Resolved = states.Count(state =>
            string.Equals(state, "Resolved", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(state, "Resolved (AI)", StringComparison.OrdinalIgnoreCase));
        project.LastActivity = DateTime.UtcNow.ToString("o");

        await db.SaveChangesAsync(cancellationToken);
    }

    private static bool IsActiveState(string state)
        => string.Equals(state, "New", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(state, "Active", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(state, "Planning (AI)", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(state, "In Progress", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(state, "In Progress (AI)", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(state, "In-PR", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(state, "In-PR (AI)", StringComparison.OrdinalIgnoreCase);
}
