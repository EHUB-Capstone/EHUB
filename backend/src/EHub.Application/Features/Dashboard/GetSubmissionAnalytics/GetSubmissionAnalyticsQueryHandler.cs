using EHub.Application.Common.Interfaces.Persistence;
using EHub.Application.Common.Interfaces.Services;
using EHub.Contracts.Dashboard;
using EHub.Domain.Enums;
using EHub.Shared.Constants;
using EHub.Shared.Errors;
using EHub.Shared.Results;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace EHub.Application.Features.Dashboard.GetSubmissionAnalytics;

public sealed class GetSubmissionAnalyticsQueryHandler(
    IApplicationDbContext context,
    IDateTimeProvider dateTimeProvider,
    IValidator<GetSubmissionAnalyticsRequest> validator) : IGetSubmissionAnalyticsQueryHandler
{
    public async Task<Result<SubmissionAnalyticsResponse>> HandleAsync(
        Guid userId, IReadOnlyCollection<string> roles, GetSubmissionAnalyticsRequest request,
        CancellationToken cancellationToken = default)
    {
        var isAdmin = roles.Contains(SystemRoles.Admin, StringComparer.OrdinalIgnoreCase);
        if (userId == Guid.Empty || (!isAdmin && !roles.Contains(SystemRoles.Lecturer, StringComparer.OrdinalIgnoreCase)))
            return Denied();

        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
            return Result.Failure<SubmissionAnalyticsResponse>(ErrorCodes.ClassValidationError, validation.Errors[0].ErrorMessage);

        var classesQuery = context.Classes.AsNoTracking().Where(item =>
            item.Status != ClassStatus.Archived && item.Semester.Status != SemesterStatus.Archived &&
            (isAdmin || item.PrimaryLecturerId == userId || item.ClassLecturers.Any(assignment => assignment.LecturerId == userId)));

        if (request.ClassId.HasValue && !await classesQuery.AnyAsync(item => item.Id == request.ClassId, cancellationToken))
            return Denied();
        if (request.TeamId.HasValue && !await context.Teams.AsNoTracking().AnyAsync(team =>
                team.Id == request.TeamId && classesQuery.Any(item => item.Id == team.ClassId), cancellationToken))
            return Denied();

        if (request.Semester is not null)
        {
            var term = request.Semester.Trim().ToUpperInvariant() switch
            {
                "SP" => SemesterTerm.Spring,
                "SU" => SemesterTerm.Summer,
                _ => SemesterTerm.Fall
            };
            classesQuery = classesQuery.Where(item => item.Semester.Term == term);
        }
        if (request.Year.HasValue) classesQuery = classesQuery.Where(item => item.Semester.Year == request.Year);
        if (request.ClassId.HasValue) classesQuery = classesQuery.Where(item => item.Id == request.ClassId);

        var classes = await classesQuery.Select(item => new
        {
            item.Id, item.ClassCode, item.CourseId, CourseCode = item.Course.Code, SemesterCode = item.Semester.Code
        }).ToArrayAsync(cancellationToken);
        var classIds = classes.Select(item => item.Id).ToArray();
        var courseIds = classes.Select(item => item.CourseId).Distinct().ToArray();
        var teams = await context.Teams.AsNoTracking().Where(item => classIds.Contains(item.ClassId) &&
                item.Status == TeamStatus.Active && (!request.TeamId.HasValue || item.Id == request.TeamId))
            .OrderBy(item => item.TeamName)
            .Select(item => new { item.Id, item.ClassId, item.TeamName, HasWorkspace = item.Project != null && item.Project.Status != ProjectStatus.Archived })
            .ToArrayAsync(cancellationToken);
        var checkpoints = await context.Checkpoints.AsNoTracking().Where(item => item.CourseId.HasValue &&
                courseIds.Contains(item.CourseId.Value) && item.ClassId == null && item.Status != CheckpointStatus.Archived &&
                (!request.CheckpointNumber.HasValue || item.CheckpointNumber == request.CheckpointNumber))
            .OrderBy(item => item.CheckpointNumber)
            .Select(item => new { item.Id, item.CourseId, item.CheckpointNumber, item.Name, item.CourseWeight })
            .ToArrayAsync(cancellationToken);
        var checkpointIds = checkpoints.Select(item => item.Id).ToArray();
        var schedules = await context.ClassCheckpointSchedules.AsNoTracking()
            .Where(item => classIds.Contains(item.ClassId) && checkpointIds.Contains(item.CheckpointId))
            .Select(item => new { item.ClassId, item.CheckpointId, item.EndDateUtc }).ToArrayAsync(cancellationToken);
        var teamIds = teams.Select(item => item.Id).ToArray();
        // Requirement text and submission/evaluation statuses do not establish an artifact submission.
        var artifacts = await context.Submissions.AsNoTracking().Where(item => teamIds.Contains(item.TeamId) &&
                checkpointIds.Contains(item.CheckpointId) && (item.Files.Any() || item.Links.Any()))
            .Select(item => new
            {
                item.TeamId, item.CheckpointId,
                FirstFileAt = item.Files.Select(file => (DateTime?)file.UploadedAt).Min(),
                FirstLinkAt = item.Links.Select(link => (DateTime?)link.SubmittedAt).Min()
            }).ToArrayAsync(cancellationToken);
        var submittedByKey = artifacts.GroupBy(item => (item.TeamId, item.CheckpointId)).ToDictionary(
            group => group.Key,
            group => group.SelectMany(item => new[] { item.FirstFileAt, item.FirstLinkAt })
                .Where(time => time.HasValue).Min(time => time!.Value));
        var scheduleByKey = schedules.ToDictionary(item => (item.ClassId, item.CheckpointId), item => item.EndDateUtc);
        var classById = classes.ToDictionary(item => item.Id);
        var now = dateTimeProvider.UtcNow;
        var items = teams.SelectMany(team => checkpoints.Where(checkpoint => checkpoint.CourseId == classById[team.ClassId].CourseId)
            .Select(checkpoint =>
            {
                var hasSubmission = submittedByKey.TryGetValue((team.Id, checkpoint.Id), out var submittedAt);
                DateTime? deadline = scheduleByKey.TryGetValue((team.ClassId, checkpoint.Id), out var end) ? end : null;
                var @class = classById[team.ClassId];
                return new SubmissionAnalyticsItemResponse
                {
                    TeamId = team.Id, TeamName = team.TeamName, ClassId = team.ClassId, ClassCode = @class.ClassCode,
                    CourseCode = @class.CourseCode, SemesterCode = @class.SemesterCode, HasWorkspace = team.HasWorkspace,
                    CheckpointId = checkpoint.Id, CheckpointNumber = checkpoint.CheckpointNumber, CheckpointTitle = checkpoint.Name,
                    CourseWeight = checkpoint.CourseWeight, DeadlineUtc = deadline, SubmittedAtUtc = hasSubmission ? submittedAt : null,
                    Status = SubmissionAnalyticsRules.Status(hasSubmission, deadline, now)
                };
            })).ToArray();
        var submittedCount = items.Count(item => item.Status == "Submitted");
        return Result.Success(new SubmissionAnalyticsResponse
        {
            ServerTimeUtc = now, ExpectedCount = items.Length, SubmittedCount = submittedCount,
            NotSubmittedCount = items.Length - submittedCount, MissingCount = items.Count(item => item.Status == "Missing"), Items = items
        });
    }

    private static Result<SubmissionAnalyticsResponse> Denied() =>
        Result.Failure<SubmissionAnalyticsResponse>(ErrorCodes.ClassAccessDenied, "You do not have access to this submission analytics scope.");
}
