using EHub.Contracts.Teams;
using EHub.Shared.Results;

namespace EHub.Application.Features.Teams.Lineage;

public interface ITeamLineageHandler
{
    Task<Result<TeamLineageDto>> GetLineageAsync(
        Guid teamId, Guid userId, string role, CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyCollection<TeamLineageSubmissionDto>>> GetTermSubmissionsAsync(
        Guid teamId, Guid termTeamId, Guid userId, string role, CancellationToken cancellationToken = default);

    Task<Result<TeamContinuityReportDto>> GetReportAsync(
        Guid semesterId, string role, CancellationToken cancellationToken = default);
}
