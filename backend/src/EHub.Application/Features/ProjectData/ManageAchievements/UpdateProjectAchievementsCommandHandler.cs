using System.Text.Json;
using EHub.Application.Common.Interfaces.Persistence;
using EHub.Application.Common.Interfaces.Services;
using EHub.Application.Features.ProjectData.Common;
using EHub.Contracts.ProjectData;
using EHub.Domain.Entities;
using EHub.Shared.Errors;
using EHub.Shared.Results;
using Microsoft.EntityFrameworkCore;

namespace EHub.Application.Features.ProjectData.ManageAchievements;

public sealed class UpdateProjectAchievementsCommandHandler(
    IApplicationDbContext context,
    IDateTimeProvider dateTimeProvider) : IUpdateProjectAchievementsCommandHandler
{
    private const string ActivityAction = "PROJECT_ACHIEVEMENTS_CHANGED";

    public async Task<Result<ProjectAchievementsResponse>> HandleAsync(
        Guid projectId,
        UpdateProjectAchievementsRequest request,
        Guid currentUserId,
        string currentUserRole,
        CancellationToken cancellationToken = default)
    {
        var isAdmin = ProjectDataQuery.IsAdmin(currentUserRole);
        if (!isAdmin && !ProjectDataQuery.IsLecturer(currentUserRole))
        {
            return Failure(ErrorCodes.ProjectDataAccessDenied, "Only administrators and lecturers can update project achievements.");
        }

        if (!uint.TryParse(request.RowVersion, out var expectedVersion))
            return Failure(ErrorCodes.ProjectDataValidationError, "A valid rowVersion is required.");

        var requested = new HashSet<string>(StringComparer.Ordinal);
        foreach (var value in request.Achievements ?? [])
        {
            if (ProjectAchievementMapping.Canonicalize(value) is not { } canonical)
                return Failure(ErrorCodes.ProjectDataValidationError, "Achievement must be Potential, Funded or Awarded.");
            requested.Add(canonical);
        }

        // The project must exist within the feature scope (administrator view) before role-specific access is judged.
        var project = await ProjectDataQuery.Scoped(context.Projects, currentUserId, isAdmin: true)
            .FirstOrDefaultAsync(item => item.Id == projectId, cancellationToken);
        if (project is null)
            return Failure(ErrorCodes.ProjectDataNotFound, "The project was not found.");

        if (!isAdmin && !await ProjectDataQuery.Scoped(context.Projects.AsNoTracking(), currentUserId, isAdmin: false)
                .AnyAsync(item => item.Id == projectId, cancellationToken))
        {
            return Failure(ErrorCodes.ProjectDataAccessDenied, "You can only update projects of classes assigned to you.");
        }

        if (project.Version != expectedVersion)
            return Failure(ErrorCodes.ProjectDataConcurrencyConflict, "The project was changed by another user. Refresh and try again.");

        var before = ProjectAchievementMapping.ToNames(project);
        var nextPotential = requested.Contains(ProjectAchievementNames.Potential);
        var nextFunded = requested.Contains(ProjectAchievementNames.Funded);
        var nextAwarded = requested.Contains(ProjectAchievementNames.Awarded);

        // An unchanged label set is a no-op so retries after a lost response stay harmless.
        if (project.IsHighPotential == nextPotential &&
            project.IsFunded == nextFunded &&
            project.IsAwarded == nextAwarded)
        {
            return Result.Success(ToResponse(project));
        }

        var now = dateTimeProvider.UtcNow;
        project.IsHighPotential = nextPotential;
        project.IsFunded = nextFunded;
        project.IsAwarded = nextAwarded;
        project.UpdatedAt = now;
        project.UpdatedBy = currentUserId;

        var after = ProjectAchievementMapping.ToNames(project);
        var changed = before.Except(after).Concat(after.Except(before)).ToArray();
        context.ProjectActivityLogs.Add(new ProjectActivityLog
        {
            ProjectId = project.Id,
            ActorUserId = currentUserId,
            Action = ActivityAction,
            Summary = $"Achievements changed from {Describe(before)} to {Describe(after)}.",
            ChangedFieldsJson = JsonSerializer.Serialize(changed),
            OccurredAtUtc = now,
        });

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Failure(ErrorCodes.ProjectDataConcurrencyConflict, "The project was changed by another user. Refresh and try again.");
        }

        return Result.Success(ToResponse(project));
    }

    private static ProjectAchievementsResponse ToResponse(Project project) => new()
    {
        ProjectId = project.Id,
        Achievements = ProjectAchievementMapping.ToNames(project),
        RowVersion = project.Version.ToString(),
    };

    private static string Describe(IReadOnlyCollection<string> names) =>
        names.Count == 0 ? "none" : string.Join(", ", names);

    private static Result<ProjectAchievementsResponse> Failure(string code, string message) =>
        Result.Failure<ProjectAchievementsResponse>(code, message);
}
