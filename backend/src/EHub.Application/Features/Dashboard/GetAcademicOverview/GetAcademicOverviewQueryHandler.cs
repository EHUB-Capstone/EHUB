using EHub.Application.Common.Interfaces.Persistence;
using EHub.Application.Common.Interfaces.Services;
using EHub.Application.Features.Rankings.TeamRankings;
using EHub.Contracts.Dashboard;
using EHub.Domain.Enums;
using EHub.Shared.Errors;
using EHub.Shared.Results;
using Microsoft.EntityFrameworkCore;

namespace EHub.Application.Features.Dashboard.GetAcademicOverview;

public sealed class GetAcademicOverviewQueryHandler(
    IApplicationDbContext context,
    IDateTimeProvider dateTimeProvider) : IGetAcademicOverviewQueryHandler
{
    private const int ActivityWeekCount = 8;

    public async Task<Result<AcademicOverviewResponse>> HandleAsync(
        Guid lecturerId,
        GetAcademicOverviewRequest request,
        CancellationToken cancellationToken = default)
    {
        var selectedSemester = await context.Semesters
            .AsNoTracking()
            .Where(semester =>
                semester.Status != SemesterStatus.Archived &&
                (request.SemesterId.HasValue
                    ? semester.Id == request.SemesterId.Value
                    : semester.Status == SemesterStatus.Active))
            .Select(semester => new
            {
                semester.Id,
                semester.Code,
                semester.Name,
                semester.Year,
                semester.Status
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (selectedSemester is null)
        {
            return Result.Failure<AcademicOverviewResponse>(new Error(
                ErrorCodes.SemesterNotFound,
                request.SemesterId.HasValue
                    ? "The selected semester does not exist or is archived."
                    : "No active semester is configured for the academic overview."));
        }

        var now = EnsureUtc(dateTimeProvider.UtcNow);
        var classData = await context.Classes
            .AsNoTracking()
            .Where(@class =>
                (@class.PrimaryLecturerId == lecturerId ||
                    @class.ClassLecturers.Any(assignment => assignment.LecturerId == lecturerId)) &&
                @class.Status != ClassStatus.Archived &&
                @class.Semester.Status != SemesterStatus.Archived)
            .OrderByDescending(@class => @class.Semester.Year)
            .ThenBy(@class => @class.Semester.Code)
            .ThenBy(@class => @class.Course.Code)
            .ThenBy(@class => @class.ClassCode)
            .Select(@class => new
            {
                @class.Id,
                @class.ClassCode,
                @class.CourseId,
                CourseCode = @class.Course.Code,
                CourseName = @class.Course.Name,
                @class.SemesterId,
                SemesterCode = @class.Semester.Code,
                SemesterName = @class.Semester.Name,
                SemesterYear = @class.Semester.Year,
                SemesterStatus = @class.Semester.Status
            })
            .ToArrayAsync(cancellationToken);
        var assignedClasses = classData
            .Select(item => new ClassRow(
                item.Id,
                item.ClassCode,
                item.CourseId,
                item.CourseCode,
                item.CourseName,
                item.SemesterId,
                item.SemesterCode,
                item.SemesterName,
                item.SemesterYear,
                item.SemesterStatus))
            .ToArray();

        if (request.ClassId.HasValue && assignedClasses.All(item => item.Id != request.ClassId.Value))
        {
            return Result.Failure<AcademicOverviewResponse>(new Error(
                ErrorCodes.ClassAccessDenied,
                "You do not have access to the selected class."));
        }

        var semesterClasses = assignedClasses
            .Where(item => item.SemesterId == selectedSemester.Id)
            .ToArray();
        if (request.CourseId.HasValue &&
            semesterClasses.All(item => item.CourseId != request.CourseId.Value))
        {
            return Result.Failure<AcademicOverviewResponse>(new Error(
                ErrorCodes.ClassValidationError,
                "The selected subject is not available in the selected semester."));
        }

        if (request.ClassId.HasValue)
        {
            var selectedClass = assignedClasses.Single(item => item.Id == request.ClassId.Value);
            if (selectedClass.SemesterId != selectedSemester.Id ||
                (request.CourseId.HasValue && selectedClass.CourseId != request.CourseId.Value))
            {
                return Result.Failure<AcademicOverviewResponse>(new Error(
                    ErrorCodes.ClassValidationError,
                    "The selected class does not match the selected semester and subject."));
            }
        }

        var subjectOptions = semesterClasses
            .GroupBy(item => new { item.CourseId, item.CourseCode, item.CourseName })
            .OrderBy(group => group.Key.CourseCode)
            .Select(group => new AcademicOverviewSubjectOptionResponse
            {
                Id = group.Key.CourseId,
                Code = group.Key.CourseCode,
                Name = group.Key.CourseName
            })
            .ToArray();
        var classOptions = semesterClasses
            .Where(item => !request.CourseId.HasValue || item.CourseId == request.CourseId.Value)
            .OrderBy(item => item.Code)
            .Select(item => new AcademicOverviewClassOptionResponse
            {
                Id = item.Id,
                Code = item.Code,
                CourseId = item.CourseId
            })
            .ToArray();
        var semesterOptions = assignedClasses
            .GroupBy(item => new
            {
                item.SemesterId,
                item.SemesterCode,
                item.SemesterName,
                item.SemesterYear,
                item.SemesterStatus
            })
            .Select(group => new AcademicOverviewSemesterOptionResponse
            {
                Id = group.Key.SemesterId,
                Code = group.Key.SemesterCode,
                Name = group.Key.SemesterName,
                Year = group.Key.SemesterYear,
                IsActive = group.Key.SemesterStatus == SemesterStatus.Active
            })
            .Append(new AcademicOverviewSemesterOptionResponse
            {
                Id = selectedSemester.Id,
                Code = selectedSemester.Code,
                Name = selectedSemester.Name,
                Year = selectedSemester.Year,
                IsActive = selectedSemester.Status == SemesterStatus.Active
            })
            .GroupBy(item => item.Id)
            .Select(group => group.First())
            .OrderByDescending(item => item.Year)
            .ThenBy(item => item.Code)
            .ToArray();
        var filterOptions = new AcademicOverviewFilterOptionsResponse
        {
            Semesters = semesterOptions,
            Subjects = subjectOptions,
            Classes = classOptions
        };
        var selectedSubject = request.CourseId.HasValue
            ? subjectOptions.FirstOrDefault(item => item.Id == request.CourseId.Value)
            : null;
        var selectedClassOption = request.ClassId.HasValue
            ? classOptions.FirstOrDefault(item => item.Id == request.ClassId.Value)
            : null;
        var scope = new AcademicOverviewScopeResponse
        {
            SemesterId = selectedSemester.Id,
            SemesterCode = selectedSemester.Code,
            SemesterName = selectedSemester.Name,
            CourseId = selectedSubject?.Id,
            SubjectCode = selectedSubject?.Code,
            SubjectName = selectedSubject?.Name,
            ClassId = selectedClassOption?.Id,
            ClassCode = selectedClassOption?.Code
        };
        var classes = semesterClasses
            .Where(item => !request.CourseId.HasValue || item.CourseId == request.CourseId.Value)
            .Where(item => !request.ClassId.HasValue || item.Id == request.ClassId.Value)
            .ToArray();

        if (classes.Length == 0)
        {
            return Result.Success(BuildEmptyResponse(
                scope,
                filterOptions,
                assignedClasses.Length > 0,
                now));
        }

        var classIds = classes.Select(item => item.Id).ToArray();
        var courseIds = classes.Select(item => item.CourseId).Distinct().ToArray();
        var teamData = await context.Teams
            .AsNoTracking()
            .Where(team => classIds.Contains(team.ClassId) && team.Status == TeamStatus.Active)
            .OrderBy(team => team.Class.ClassCode)
            .ThenBy(team => team.TeamName)
            .Select(team => new
            {
                team.Id,
                team.ClassId,
                team.TeamName,
                team.Class.CourseId,
                team.Class.ClassCode,
                ProjectId = team.Project != null && team.Project.Status != ProjectStatus.Archived
                    ? (Guid?)team.Project.Id
                    : null,
                ProjectName = team.Project != null && team.Project.Status != ProjectStatus.Archived
                    ? team.Project.Name
                    : string.Empty,
                IsHighPotential = team.Project != null &&
                    team.Project.Status != ProjectStatus.Archived &&
                    team.Project.IsHighPotential
            })
            .ToArrayAsync(cancellationToken);
        var teams = teamData.Select(item => new TeamRow(
            item.Id,
            item.ClassId,
            item.TeamName,
            item.CourseId,
            item.ClassCode,
            item.ProjectId,
            item.ProjectName,
            item.IsHighPotential)).ToArray();
        var teamIds = teams.Select(item => item.Id).ToArray();
        var projectIds = teams.Where(item => item.ProjectId.HasValue)
            .Select(item => item.ProjectId!.Value)
            .ToArray();

        var checkpointData = await context.Checkpoints
            .AsNoTracking()
            .Where(checkpoint =>
                checkpoint.CourseId.HasValue &&
                courseIds.Contains(checkpoint.CourseId.Value) &&
                checkpoint.ClassId == null &&
                checkpoint.Status != CheckpointStatus.Archived)
            .OrderBy(checkpoint => checkpoint.Course!.Code)
            .ThenBy(checkpoint => checkpoint.CheckpointNumber)
            .Select(checkpoint => new
            {
                checkpoint.Id,
                CourseId = checkpoint.CourseId!.Value,
                CourseCode = checkpoint.Course!.Code,
                checkpoint.CheckpointNumber,
                checkpoint.Name
            })
            .ToArrayAsync(cancellationToken);
        var checkpoints = checkpointData.Select(item => new CheckpointRow(
            item.Id,
            item.CourseId,
            item.CourseCode,
            item.CheckpointNumber,
            item.Name)).ToArray();
        var checkpointIds = checkpoints.Select(item => item.Id).ToArray();

        var scheduleData = checkpointIds.Length == 0
            ? []
            : await context.ClassCheckpointSchedules
                .AsNoTracking()
                .Where(schedule =>
                    classIds.Contains(schedule.ClassId) &&
                    checkpointIds.Contains(schedule.CheckpointId))
                .Select(schedule => new
                {
                    schedule.ClassId,
                    schedule.CheckpointId,
                    schedule.EndDateUtc
                })
                .ToArrayAsync(cancellationToken);
        var schedules = scheduleData.Select(item => new ScheduleRow(
            item.ClassId,
            item.CheckpointId,
            EnsureUtc(item.EndDateUtc))).ToArray();

        var submissionData = teamIds.Length == 0 || checkpointIds.Length == 0
            ? []
            : await context.Submissions
                .AsNoTracking()
                .Where(submission =>
                    teamIds.Contains(submission.TeamId) &&
                    checkpointIds.Contains(submission.CheckpointId) &&
                    submission.Project.Status != ProjectStatus.Archived &&
                    submission.Status != SubmissionStatus.Draft &&
                    submission.SubmittedAt.HasValue)
                .Select(submission => new
                {
                    submission.TeamId,
                    submission.Team.ClassId,
                    submission.ProjectId,
                    submission.CheckpointId,
                    SubmittedAt = submission.SubmittedAt!.Value
                })
                .ToArrayAsync(cancellationToken);
        var submissions = submissionData.Select(item => new SubmissionRow(
            item.TeamId,
            item.ClassId,
            item.ProjectId,
            item.CheckpointId,
            EnsureUtc(item.SubmittedAt))).ToArray();
        var uniqueSubmissions = submissions
            .GroupBy(item => (item.TeamId, item.CheckpointId))
            .Select(group => group.OrderBy(item => item.SubmittedAtUtc).First())
            .ToArray();

        var rubricData = await context.Rubrics
            .AsNoTracking()
            .Where(rubric =>
                rubric.CourseId.HasValue &&
                courseIds.Contains(rubric.CourseId.Value) &&
                rubric.ClassId == null &&
                (!rubric.CheckpointId.HasValue || checkpointIds.Contains(rubric.CheckpointId.Value)) &&
                rubric.Status == RubricStatus.Active)
            .OrderBy(rubric => rubric.Id)
            .Select(rubric => new
            {
                rubric.Id,
                CourseId = rubric.CourseId!.Value,
                rubric.CheckpointId,
                rubric.CourseWeight
            })
            .ToArrayAsync(cancellationToken);
        var rubrics = rubricData.Select(item => new RubricRow(
            item.Id,
            item.CourseId,
            item.CheckpointId,
            item.CourseWeight)).ToArray();

        var evaluationData = projectIds.Length == 0
            ? []
            : await context.Evaluations
                .AsNoTracking()
                .Where(evaluation =>
                    projectIds.Contains(evaluation.ProjectId) &&
                    evaluation.Status != EvaluationStatus.Draft)
                .Select(evaluation => new
                {
                    evaluation.Id,
                    evaluation.ProjectId,
                    evaluation.Project.TeamId,
                    evaluation.Project.Team.ClassId,
                    evaluation.RubricId,
                    CheckpointId = evaluation.SubmissionId.HasValue
                        ? (Guid?)evaluation.Submission!.CheckpointId
                        : evaluation.Rubric.CheckpointId,
                    evaluation.TotalScore,
                    evaluation.Status,
                    OccurredAtUtc = evaluation.PublishedAt ??
                        evaluation.SubmittedAt ??
                        evaluation.UpdatedAt ??
                        evaluation.CreatedAt
                })
                .ToArrayAsync(cancellationToken);
        var evaluations = evaluationData.Select(item => new EvaluationRow(
            item.Id,
            item.ProjectId,
            item.TeamId,
            item.ClassId,
            item.RubricId,
            item.CheckpointId,
            item.TotalScore,
            item.Status,
            EnsureUtc(item.OccurredAtUtc))).ToArray();

        var evaluationByProjectRubric = evaluations
            .GroupBy(item => (item.ProjectId, item.RubricId))
            .ToDictionary(
                group => group.Key,
                group => group.OrderByDescending(item => item.OccurredAtUtc)
                    .ThenByDescending(item => item.Id)
                    .First());
        var uniqueEvaluations = evaluations
            .Where(item => item.CheckpointId.HasValue && checkpointIds.Contains(item.CheckpointId.Value))
            .GroupBy(item => (item.ProjectId, CheckpointId: item.CheckpointId!.Value))
            .Select(group => group.OrderByDescending(item => item.OccurredAtUtc)
                .ThenByDescending(item => item.Id)
                .First())
            .ToArray();

        var submittedPairs = uniqueSubmissions
            .Select(item => (item.TeamId, item.CheckpointId))
            .ToHashSet();
        var evaluatedPairs = uniqueEvaluations
            .Select(item => (item.ProjectId, CheckpointId: item.CheckpointId!.Value))
            .ToHashSet();
        var scheduleByClassCheckpoint = schedules
            .ToDictionary(item => (item.ClassId, item.CheckpointId));
        var teamsById = teams.ToDictionary(item => item.Id);

        var allCheckpointProgress = checkpoints.Select(checkpoint =>
        {
            var applicableTeams = teams
                .Where(team => team.CourseId == checkpoint.CourseId)
                .ToArray();
            var submittedTeams = applicableTeams.Count(team =>
                submittedPairs.Contains((team.Id, checkpoint.Id)));
            var evaluatedProjects = applicableTeams.Count(team =>
                team.ProjectId.HasValue &&
                evaluatedPairs.Contains((team.ProjectId.Value, checkpoint.Id)));
            var missedDeadlineTeams = applicableTeams.Count(team =>
                scheduleByClassCheckpoint.TryGetValue((team.ClassId, checkpoint.Id), out var schedule) &&
                schedule.EndDateUtc < now &&
                !submittedPairs.Contains((team.Id, checkpoint.Id)));

            return new AcademicOverviewCheckpointResponse
            {
                CheckpointId = checkpoint.Id,
                CourseCode = checkpoint.CourseCode,
                CheckpointNumber = checkpoint.Number,
                Title = checkpoint.Title,
                ExpectedTeams = applicableTeams.Length,
                SubmittedTeams = submittedTeams,
                EvaluatedProjects = evaluatedProjects,
                MissedDeadlineTeams = missedDeadlineTeams
            };
        }).ToArray();
        var nearestSchedule = schedules
            .Where(item => item.EndDateUtc >= now)
            .OrderBy(item => item.EndDateUtc)
            .ThenBy(item => item.CheckpointId)
            .FirstOrDefault()
            ?? schedules
                .Where(item => item.EndDateUtc < now)
                .OrderByDescending(item => item.EndDateUtc)
                .ThenBy(item => item.CheckpointId)
                .FirstOrDefault();
        var checkpointProgress = nearestSchedule is null
            ? []
            : allCheckpointProgress
                .Where(item => item.CheckpointId == nearestSchedule.CheckpointId)
                .ToArray();

        var pendingEvaluations = uniqueSubmissions.Count(submission =>
            teamsById.TryGetValue(submission.TeamId, out var team) &&
            team.ProjectId.HasValue &&
            !evaluatedPairs.Contains((team.ProjectId.Value, submission.CheckpointId)));
        var classBreakdown = classes.Select(@class =>
        {
            var classTeams = teams.Where(team => team.ClassId == @class.Id).ToArray();
            var classTeamIds = classTeams.Select(team => team.Id).ToHashSet();
            var classProjectIds = classTeams.Where(team => team.ProjectId.HasValue)
                .Select(team => team.ProjectId!.Value)
                .ToHashSet();
            return new AcademicOverviewClassResponse
            {
                ClassId = @class.Id,
                ClassCode = @class.Code,
                Teams = classTeams.Length,
                Projects = classProjectIds.Count,
                Submissions = uniqueSubmissions.Count(item => classTeamIds.Contains(item.TeamId)),
                Evaluations = uniqueEvaluations.Count(item => classProjectIds.Contains(item.ProjectId)),
                PotentialProjects = classTeams.Count(team => team.IsHighPotential)
            };
        }).ToArray();

        var topTeams = BuildTopTeams(teams, rubrics, evaluationByProjectRubric);
        var activityTrend = BuildActivityTrend(uniqueSubmissions, uniqueEvaluations, now);

        return Result.Success(new AcademicOverviewResponse
        {
            Scope = scope,
            FilterOptions = filterOptions,
            Metrics = new AcademicOverviewMetricsResponse
            {
                TotalClasses = classes.Length,
                TotalTeams = teams.Length,
                TotalProjects = teams.Count(item => item.ProjectId.HasValue),
                TotalSubmissions = uniqueSubmissions.Length,
                TotalEvaluations = uniqueEvaluations.Length,
                TotalPotentialProjects = teams.Count(item => item.IsHighPotential)
            },
            Attention = new AcademicOverviewAttentionResponse
            {
                MissedDeadlines = allCheckpointProgress.Sum(item => item.MissedDeadlineTeams),
                PendingEvaluations = pendingEvaluations
            },
            ActivityTrend = activityTrend,
            CheckpointProgress = checkpointProgress,
            Classes = classBreakdown,
            TopTeams = topTeams,
            LastUpdatedAtUtc = now,
            HasAssignedClasses = true,
            HasMatchingClasses = true,
            HasClasses = true
        });
    }

    private static AcademicOverviewResponse BuildEmptyResponse(
        AcademicOverviewScopeResponse scope,
        AcademicOverviewFilterOptionsResponse filterOptions,
        bool hasAssignedClasses,
        DateTime now) => new()
        {
            Scope = scope,
            FilterOptions = filterOptions,
            ActivityTrend = BuildActivityTrend([], [], now),
            LastUpdatedAtUtc = now,
            HasAssignedClasses = hasAssignedClasses,
            HasMatchingClasses = false,
            HasClasses = false
        };

    private static AcademicOverviewActivityResponse[] BuildActivityTrend(
        IReadOnlyCollection<SubmissionRow> submissions,
        IReadOnlyCollection<EvaluationRow> evaluations,
        DateTime now)
    {
        var currentWeekStart = StartOfWeek(now);
        var firstWeekStart = currentWeekStart.AddDays(-7 * (ActivityWeekCount - 1));
        return Enumerable.Range(0, ActivityWeekCount)
            .Select(index =>
            {
                var weekStart = firstWeekStart.AddDays(index * 7);
                var weekEnd = weekStart.AddDays(7);
                return new AcademicOverviewActivityResponse
                {
                    WeekStartUtc = weekStart,
                    Submissions = submissions.Count(item =>
                        item.SubmittedAtUtc >= weekStart && item.SubmittedAtUtc < weekEnd),
                    Evaluations = evaluations.Count(item =>
                        item.OccurredAtUtc >= weekStart && item.OccurredAtUtc < weekEnd)
                };
            })
            .ToArray();
    }

    private static AcademicOverviewTopTeamResponse[] BuildTopTeams(
        IReadOnlyCollection<TeamRow> teams,
        IReadOnlyCollection<RubricRow> rubrics,
        IReadOnlyDictionary<(Guid ProjectId, Guid RubricId), EvaluationRow> evaluations)
    {
        var results = new List<AcademicOverviewTopTeamResponse>();
        foreach (var team in teams.Where(item => item.ProjectId.HasValue))
        {
            var courseRubrics = rubrics.Where(item => item.CourseId == team.CourseId).ToArray();
            var selectedRubrics = courseRubrics
                .Where(item => !item.CheckpointId.HasValue)
                .Concat(courseRubrics
                    .Where(item => item.CheckpointId.HasValue)
                    .GroupBy(item => item.CheckpointId!.Value)
                    .Select(group => group.First()))
                .ToArray();
            var components = selectedRubrics.Select(rubric =>
            {
                var evaluation = evaluations.GetValueOrDefault((team.ProjectId!.Value, rubric.Id));
                return new TeamRankingComponent(
                    rubric.Weight,
                    evaluation?.Score,
                    evaluation?.Status);
            }).ToArray();
            var courseTotal = TeamRankingRules.CalculateCourseTotal(components);
            if (!courseTotal.HasValue)
            {
                continue;
            }

            results.Add(new AcademicOverviewTopTeamResponse
            {
                TeamId = team.Id,
                TeamName = team.Name,
                ClassCode = team.ClassCode,
                ProjectName = team.ProjectName,
                CourseTotal = courseTotal.Value,
                CompletedComponents = components.Count(item => item.Score.HasValue),
                TotalComponents = components.Length
            });
        }

        return results
            .OrderByDescending(item => item.CourseTotal)
            .ThenBy(item => item.TeamName, StringComparer.OrdinalIgnoreCase)
            .Take(5)
            .ToArray();
    }

    private static DateTime StartOfWeek(DateTime value)
    {
        var date = EnsureUtc(value).Date;
        var daysSinceMonday = ((int)date.DayOfWeek + 6) % 7;
        return date.AddDays(-daysSinceMonday);
    }

    private static DateTime EnsureUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };

    private sealed record ClassRow(
        Guid Id,
        string Code,
        Guid CourseId,
        string CourseCode,
        string CourseName,
        Guid SemesterId,
        string SemesterCode,
        string SemesterName,
        int SemesterYear,
        SemesterStatus SemesterStatus);
    private sealed record TeamRow(
        Guid Id,
        Guid ClassId,
        string Name,
        Guid CourseId,
        string ClassCode,
        Guid? ProjectId,
        string ProjectName,
        bool IsHighPotential);
    private sealed record CheckpointRow(Guid Id, Guid CourseId, string CourseCode, int Number, string Title);
    private sealed record ScheduleRow(Guid ClassId, Guid CheckpointId, DateTime EndDateUtc);
    private sealed record SubmissionRow(
        Guid TeamId,
        Guid ClassId,
        Guid ProjectId,
        Guid CheckpointId,
        DateTime SubmittedAtUtc);
    private sealed record RubricRow(Guid Id, Guid CourseId, Guid? CheckpointId, decimal Weight);
    private sealed record EvaluationRow(
        Guid Id,
        Guid ProjectId,
        Guid TeamId,
        Guid ClassId,
        Guid RubricId,
        Guid? CheckpointId,
        decimal Score,
        EvaluationStatus Status,
        DateTime OccurredAtUtc);
}
