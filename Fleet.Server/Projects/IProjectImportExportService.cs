using Fleet.Server.Models;

namespace Fleet.Server.Projects;

public interface IProjectImportExportService
{
    Task<ProjectsExportFileDto> ExportProjectsAsync(CancellationToken cancellationToken = default);
    Task<ProjectsImportResultDto> ImportProjectsAsync(
        ProjectsExportFileDto payload,
        CancellationToken cancellationToken = default);
    Task<ProjectWorkItemsExportFileDto?> ExportWorkItemsAsync(
        string projectId,
        CancellationToken cancellationToken = default);
    Task<WorkItemsImportResultDto> ImportWorkItemsAsync(
        string projectId,
        ProjectWorkItemsExportFileDto payload,
        CancellationToken cancellationToken = default);
}
