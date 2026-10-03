using EHub.Application.Common.Interfaces.Persistence;
using EHub.Application.Features.ProjectData.Common;
using EHub.Contracts.ProjectData;
using EHub.Shared.Errors;
using EHub.Shared.Results;
using Microsoft.EntityFrameworkCore;

namespace EHub.Application.Features.ProjectData.GetProjectDataSummary;

public sealed class GetProjectDataSummaryQueryHandler(IApplicationDbContext context) : IGetProjectDataSummaryQueryHandler
{
    public async Task<Result<ProjectDataSummaryResponse>> HandleAsync(
        GetProjectDataRequest request,
        Guid currentUserId,
        string currentUserRole,
        CancellationToken cancellationToken = default)
    {
        var isAdmin = ProjectDataQuery.IsAdmin(currentUserRole);
        if (!isAdmin && !ProjectDataQuery.IsLecturer(currentUserRole))
        {
            return Result.Failure<ProjectDataSummaryResponse>(
                ErrorCodes.ProjectDataAccessDenied,
                "Only administrators and lecturers can view project data.");
        }

        if (ProjectDataQuery.Normalize(request.Search)?.Length > ProjectDataQuery.MaxSearchLength)
        {
            return Result.Failure<ProjectDataSummaryResponse>(
                ErrorCodes.ProjectDataValidationError,
                "The search value is invalid.");
        }

        // The same scope and filters as the list, so the cards always describe the rows the table can show.
        // Paging and sorting do not apply. One group is one project, so projects are counted once each.
        var counts = await ProjectDataQuery.ApplyFilters(
                ProjectDataQuery.Scoped(context.Projects.AsNoTracking(), currentUserId, isAdmin),
                request)
            .GroupBy(_ => 1)
            .Select(group => new
            {
                Total = group.Count(),
                Potential = group.Count(project => project.IsHighPotential),
                Funded = group.Count(project => project.IsFunded),
                Awarded = group.Count(project => project.IsAwarded),
            })
            .FirstOrDefaultAsync(cancellationToken);

        return Result.Success(new ProjectDataSummaryResponse
        {
            TotalGroups = counts?.Total ?? 0,
            PotentialGroups = counts?.Potential ?? 0,
            FundedGroups = counts?.Funded ?? 0,
            AwardedGroups = counts?.Awarded ?? 0,
        });
    }
}
