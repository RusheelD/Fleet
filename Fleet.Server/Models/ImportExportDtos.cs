namespace Fleet.Server.Models;

public record ProjectsExportFileDto(
    string Format,
    int SchemaVersion,
    string ExportedAtUtc,
    ProjectExportDto[] Projects
);

public record ProjectWorkItemsExportFileDto(
    string Format,
    int SchemaVersion,
    string ExportedAtUtc,
    string ProjectTitle,
    string Repo,
    WorkItemLevelExportDto[] WorkItemLevels,
    WorkItemExportDto[] WorkItems
);

public record ProjectExportDto(
    string? Slug,
    string Title,
    string Description,
    string Repo,
    string BranchPattern,
    string CommitAuthorMode,
    string? CommitAuthorName,
    string? CommitAuthorEmail,
    WorkItemLevelExportDto[] WorkItemLevels,
    WorkItemExportDto[] WorkItems
);

public record WorkItemLevelExportDto(
    string Name,
    string IconName,
    string Color,
    int Ordinal,
    bool IsDefault
);

public record WorkItemExportDto(
    int WorkItemNumber,
    string Title,
    string State,
    int Priority,
    int Difficulty,
    string AssignedTo,
    string[] Tags,
    bool IsAI,
    string Description,
    int? ParentWorkItemNumber,
    string? LevelName,
    string AssignmentMode,
    int? AssignedAgentCount,
    string AcceptanceCriteria,
    string? LinkedPullRequestUrl,
    string? LastObservedPullRequestState,
    string? LastObservedPullRequestUrl
);

public record ProjectsImportResultDto(
    int ProjectsImported,
    int WorkItemsImported,
    int WorkItemLevelsImported,
    string[] ImportedProjectIds
);

public record WorkItemsImportResultDto(
    int WorkItemsImported,
    int WorkItemLevelsImported
);
