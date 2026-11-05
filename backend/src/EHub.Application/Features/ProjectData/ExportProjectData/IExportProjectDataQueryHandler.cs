using EHub.Contracts.ProjectData;
using EHub.Shared.Results;

namespace EHub.Application.Features.ProjectData.ExportProjectData;

public sealed record ProjectDataExportFile(byte[] FileBytes, string ContentType, string FileName);

public interface IExportProjectDataQueryHandler
{
    /// <summary>
    /// Exports the project data report (.xlsx) for the projects the caller may see and the search and filters select:
    /// an overview with the filters applied, one row per project, summaries by semester, subject, class, industry,
    /// major, lecturer and mentor, and the competition entries.
    /// </summary>
    Task<Result<ProjectDataExportFile>> HandleAsync(
        GetProjectDataRequest request,
        Guid currentUserId,
        string currentUserRole,
        CancellationToken cancellationToken = default);
}
