using EHub.Application.Common.Interfaces.Persistence;
using EHub.Contracts.Rankings;
using EHub.Domain.Enums;
using EHub.Shared.Constants;
using EHub.Shared.Errors;
using EHub.Shared.Results;
using Microsoft.EntityFrameworkCore;

namespace EHub.Application.Features.Rankings.TeamRankings;

public sealed class TeamRankingQueryHandler(IApplicationDbContext context) : ITeamRankingQueryHandler
{
    public async Task<Result<TeamRankingListResponse>> HandleAsync(
        GetTeamRankingsRequest request,
        Guid currentUserId,
        string currentUserRole,
        CancellationToken cancellationToken = default)
    {
        if (!IsRole(currentUserRole, SystemRoles.Admin) &&
            !IsRole(currentUserRole, SystemRoles.Lecturer))
        {
            return Result.Failure<TeamRankingListResponse>(
                ErrorCodes.WorkspaceAccessDenied,
                "Only administrators and lecturers can view team rankings.");
        }

        var teamQuery = context.Teams.AsNoTracking()
            .Where(team => team.Status == TeamStatus.Active);
        if (IsRole(currentUserRole, SystemRoles.Lecturer))
        {
            teamQuery = teamQuery.Where(team =>
                team.Class.PrimaryLecturerId == currentUserId ||
                team.Class.ClassLecturers.Any(assignment => assignment.LecturerId == currentUserId));
        }

        var availableSemesterRows = await teamQuery
            .Select(team => new
            {
                Id = team.Class.SemesterId,
                team.Class.Semester.Code,
                team.Class.Semester.Term,
                team.Class.Semester.Year,
                team.Class.Semester.Status,
            })
            .Distinct()
            .ToArrayAsync(cancellationToken);
        var availableSemesters = availableSemesterRows
            .Select(item => new SemesterRow(item.Id, item.Code, item.Term, item.Year, item.Status))
            .OrderByDescending(item => item.Status == SemesterStatus.Active)
            .ThenByDescending(item => item.Year)
            .ThenBy(item => item.Term)
            .ToArray();

        var activeSemester = availableSemesters.FirstOrDefault(item => item.Status == SemesterStatus.Active);
        var selectedSemester = SelectSemester(availableSemesters, request, activeSemester);
        if (selectedSemester is null)
        {
            return Result.Success(new TeamRankingListResponse
            {
                ActiveSemester = ToNullableSemesterResponse(activeSemester),
                AvailableSemesters = availableSemesters.Select(ToSemesterResponse).ToArray(),
            });
        }

        var teamData = await teamQuery
            .Where(team => team.Class.SemesterId == selectedSemester.Id)
            .OrderBy(team => team.Class.ClassCode)
            .ThenBy(team => team.TeamName)
            .Select(team => new
            {
                team.Id,
                team.TeamName,
                team.TeamCode,
                team.ClassId,
                team.Class.ClassCode,
                CourseId = team.Class.CourseId,
                CourseCode = team.Class.Course.Code,
                SemesterCode = team.Class.Semester.Code,
                team.Class.Semester.Year,
                ProjectId = team.Project == null ? (Guid?)null : team.Project.Id,
                ProjectName = team.Project == null ? string.Empty : team.Project.Name,
                ProjectDescription = team.Project == null ? string.Empty : team.Project.Description ?? string.Empty,
            })
            .ToArrayAsync(cancellationToken);
        var teamIds = teamData.Select(team => team.Id).ToArray();
        var semesterGroupData = await context.TeamMembers.AsNoTracking()
            .Where(member => teamIds.Contains(member.TeamId) &&
                             member.CountsTowardActiveTeam &&
                             member.ClassStudent.SemesterGroupName != null &&
                             member.ClassStudent.SemesterGroupName != string.Empty)
            .Select(member => new
            {
                member.TeamId,
                SemesterGroupName = member.ClassStudent.SemesterGroupName!,
            })
            .Distinct()
            .ToArrayAsync(cancellationToken);
        var semesterGroupsByTeamId = semesterGroupData
            .GroupBy(item => item.TeamId)
            .ToDictionary(
                group => group.Key,
                group => string.Join(", ", group
                    .Select(item => item.SemesterGroupName.Trim())
                    .Where(item => item.Length > 0)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)));
        var teamRows = teamData.Select(team => new TeamRow(
            team.Id,
            team.TeamName,
            team.TeamCode,
            semesterGroupsByTeamId.GetValueOrDefault(team.Id, string.Empty),
            team.ClassId,
            team.ClassCode,
            team.CourseId,
            team.CourseCode,
            team.SemesterCode,
            team.Year,
            team.ProjectId,
            team.ProjectName,
            team.ProjectDescription)).ToArray();

        if (teamRows.Length == 0)
        {
            return Result.Success(CreateResponse(activeSemester, selectedSemester, availableSemesters, []));
        }

        var courseIds = teamRows.Select(item => item.CourseId).Distinct().ToArray();
        var checkpointData = await context.Checkpoints.AsNoTracking()
            .Where(item => item.CourseId.HasValue && courseIds.Contains(item.CourseId.Value) &&
                           item.ClassId == null && item.Status != CheckpointStatus.Archived)
            .OrderBy(item => item.CheckpointNumber)
            .Select(item => new
            {
                item.Id,
                CourseId = item.CourseId!.Value,
                item.CheckpointNumber,
                item.Name,
                item.CourseWeight,
            })
            .ToArrayAsync(cancellationToken);
        var checkpoints = checkpointData.Select(item => new CheckpointRow(
            item.Id, item.CourseId, item.CheckpointNumber, item.Name, item.CourseWeight)).ToArray();

        var checkpointIds = checkpoints.Select(item => item.Id).ToArray();
        var checkpointRubricData = checkpointIds.Length == 0
            ? []
            : await context.Rubrics.AsNoTracking()
            .Where(item => item.CourseId.HasValue && item.CheckpointId.HasValue && checkpointIds.Contains(item.CheckpointId.Value) &&
                               item.ClassId == null && item.Status == RubricStatus.Active)
                .OrderBy(item => item.Id)
                .Select(item => new
                {
                    item.Id,
                    CourseId = item.CourseId!.Value,
                    item.CheckpointId,
                    item.Name,
                    item.CourseWeight,
                })
                .ToArrayAsync(cancellationToken);
        var checkpointRubrics = checkpointRubricData.Select(item => new RubricRow(
            item.Id, item.CourseId, item.CheckpointId, item.Name, item.CourseWeight)).ToArray();
        var rubricByCheckpoint = checkpointRubrics
            .GroupBy(item => item.CheckpointId!.Value)
            .ToDictionary(group => group.Key, group => group.First());

        var assessmentData = await context.Rubrics.AsNoTracking()
            .Where(item => item.CourseId.HasValue && courseIds.Contains(item.CourseId.Value) &&
                           item.ClassId == null && item.CheckpointId == null &&
                           item.Status == RubricStatus.Active)
            .OrderBy(item => item.Name)
            .Select(item => new
            {
                item.Id,
                CourseId = item.CourseId!.Value,
                item.Name,
                item.CourseWeight,
            })
            .ToArrayAsync(cancellationToken);
        var assessments = assessmentData.Select(item => new RubricRow(
            item.Id, item.CourseId, null, item.Name, item.CourseWeight)).ToArray();

        var projectIds = teamRows.Where(item => item.ProjectId.HasValue)
            .Select(item => item.ProjectId!.Value).ToArray();
        var rubricIds = checkpointRubrics.Select(item => item.Id)
            .Concat(assessments.Select(item => item.Id)).Distinct().ToArray();
        var evaluationData = projectIds.Length == 0 || rubricIds.Length == 0
            ? []
            : await context.Evaluations.AsNoTracking()
                .Where(item => projectIds.Contains(item.ProjectId) && rubricIds.Contains(item.RubricId) &&
                               (item.Status == EvaluationStatus.Submitted || item.Status == EvaluationStatus.Published))
                .Select(item => new
                {
                    item.Id,
                    item.ProjectId,
                    item.RubricId,
                    item.TotalScore,
                    item.Status,
                    UpdatedAt = item.UpdatedAt ?? item.CreatedAt,
                })
                .ToArrayAsync(cancellationToken);
        var evaluationRows = evaluationData.Select(item => new EvaluationRow(
            item.Id, item.ProjectId, item.RubricId, item.TotalScore, item.Status, item.UpdatedAt)).ToArray();
        var latestEvaluations = evaluationRows
            .GroupBy(item => (item.ProjectId, item.RubricId))
            .ToDictionary(
                group => group.Key,
                group => group.OrderByDescending(item => item.UpdatedAt).ThenByDescending(item => item.Id).First());

        var items = teamRows.Select(team => BuildItem(
            team,
            checkpoints.Where(item => item.CourseId == team.CourseId).ToArray(),
            assessments.Where(item => item.CourseId == team.CourseId).ToArray(),
            rubricByCheckpoint,
            latestEvaluations)).ToArray();

        return Result.Success(CreateResponse(activeSemester, selectedSemester, availableSemesters, items));
    }

    private static TeamRankingItemResponse BuildItem(
        TeamRow team,
        IReadOnlyCollection<CheckpointRow> checkpoints,
        IReadOnlyCollection<RubricRow> assessments,
        IReadOnlyDictionary<Guid, RubricRow> rubricByCheckpoint,
        IReadOnlyDictionary<(Guid ProjectId, Guid RubricId), EvaluationRow> latestEvaluations)
    {
        EvaluationRow? FindEvaluation(Guid rubricId) => team.ProjectId.HasValue &&
            latestEvaluations.TryGetValue((team.ProjectId.Value, rubricId), out var evaluation)
                ? evaluation
                : null;

        var checkpointResponses = checkpoints.Select(checkpoint =>
        {
            var rubric = rubricByCheckpoint.GetValueOrDefault(checkpoint.Id);
            var evaluation = rubric is null ? null : FindEvaluation(rubric.Id);
            return new TeamRankingCheckpointResponse
            {
                CheckpointId = checkpoint.Id,
                Number = checkpoint.Number,
                Title = checkpoint.Title,
                Weight = checkpoint.Weight,
                Score = evaluation?.Score,
                Status = StatusName(evaluation?.Status),
            };
        }).ToArray();
        var assessmentResponses = assessments.Select(assessment =>
        {
            var evaluation = FindEvaluation(assessment.Id);
            return new TeamRankingAssessmentResponse
            {
                AssessmentId = assessment.Id,
                Name = assessment.Name,
                Weight = assessment.Weight,
                Score = evaluation?.Score,
                Status = StatusName(evaluation?.Status),
            };
        }).ToArray();
        var checkpointComponents = checkpointResponses.Select(item => new TeamRankingComponent(
                item.Weight, item.Score, ParseStatus(item.Status)))
            .ToArray();
        var checkpointRubricIds = checkpoints
            .Select(checkpoint => rubricByCheckpoint.GetValueOrDefault(checkpoint.Id)?.Id)
            .Where(rubricId => rubricId.HasValue)
            .Select(rubricId => rubricId!.Value)
            .ToHashSet();
        var relevantEvaluations = checkpointComponents.Length == 0 || !team.ProjectId.HasValue
            ? Array.Empty<EvaluationRow>()
            : latestEvaluations.Where(item => item.Key.ProjectId == team.ProjectId.Value &&
                                               checkpointRubricIds.Contains(item.Key.RubricId))
                .Select(item => item.Value).ToArray();

        return new TeamRankingItemResponse
        {
            TeamId = team.Id,
            TeamName = team.Name,
            TeamCode = team.Code,
            ProjectName = team.ProjectName,
            ProjectDescription = team.ProjectDescription,
            SemesterGroupName = team.SemesterGroupName,
            ClassId = team.ClassId,
            ClassCode = team.ClassCode,
            CourseCode = team.CourseCode,
            Semester = team.SemesterCode,
            Year = team.Year,
            Checkpoints = checkpointResponses,
            Assessments = assessmentResponses,
            CourseTotal = TeamRankingRules.CalculateCourseTotal(checkpointComponents),
            Status = TeamRankingRules.ResolveStatus(checkpointComponents),
            CompletedComponentCount = checkpointComponents.Count(item => item.Score.HasValue),
            PublishedComponentCount = checkpointComponents.Count(item => item.Score.HasValue && item.Status == EvaluationStatus.Published),
            TotalComponentCount = checkpointComponents.Length,
            LastUpdatedAt = relevantEvaluations.Length == 0 ? null : relevantEvaluations.Max(item => item.UpdatedAt),
        };
    }

    private static TeamRankingListResponse CreateResponse(
        SemesterRow? activeSemester,
        SemesterRow selectedSemester,
        IReadOnlyCollection<SemesterRow> availableSemesters,
        IReadOnlyCollection<TeamRankingItemResponse> items) => new()
    {
        ActiveSemester = ToNullableSemesterResponse(activeSemester),
        SelectedSemester = ToSemesterResponse(selectedSemester),
        AvailableSemesters = availableSemesters.Select(ToSemesterResponse).ToArray(),
        Items = items,
    };

    private static SemesterRow? SelectSemester(
        IReadOnlyCollection<SemesterRow> available,
        GetTeamRankingsRequest request,
        SemesterRow? activeSemester)
    {
        if (string.IsNullOrWhiteSpace(request.Semester) && !request.Year.HasValue)
        {
            return activeSemester ?? available.FirstOrDefault();
        }

        var semester = request.Semester?.Trim().ToUpperInvariant();
        return available.FirstOrDefault(item =>
            (!request.Year.HasValue || item.Year == request.Year.Value) &&
            (string.IsNullOrWhiteSpace(semester) || semester == "ALL" ||
             ToSemesterCode(item.Term) == semester || item.Code.Equals(semester, StringComparison.OrdinalIgnoreCase)));
    }

    private static TeamRankingSemesterResponse? ToNullableSemesterResponse(SemesterRow? row) =>
        row is null ? null : ToSemesterResponse(row);

    private static TeamRankingSemesterResponse ToSemesterResponse(SemesterRow row) => new()
    {
        Id = row.Id,
        Semester = ToSemesterCode(row.Term),
        Year = row.Year,
        Code = row.Code,
        IsActive = row.Status == SemesterStatus.Active,
    };

    private static string ToSemesterCode(SemesterTerm term) => term switch
    {
        SemesterTerm.Spring => "SP",
        SemesterTerm.Summer => "SU",
        SemesterTerm.Fall => "FA",
        _ => term.ToString().ToUpperInvariant(),
    };

    private static string StatusName(EvaluationStatus? status) =>
        status?.ToString().ToUpperInvariant() ?? "NOT_GRADED";

    private static EvaluationStatus? ParseStatus(string status) =>
        Enum.TryParse<EvaluationStatus>(status, true, out var value) ? value : null;

    private static bool IsRole(string role, string expected) =>
        string.Equals(role, expected, StringComparison.OrdinalIgnoreCase);

    private sealed record SemesterRow(Guid Id, string Code, SemesterTerm Term, int Year, SemesterStatus Status);
    private sealed record TeamRow(
        Guid Id, string Name, string Code, string SemesterGroupName, Guid ClassId, string ClassCode, Guid CourseId,
        string CourseCode, string SemesterCode, int Year, Guid? ProjectId, string ProjectName,
        string ProjectDescription);
    private sealed record CheckpointRow(Guid Id, Guid CourseId, int Number, string Title, decimal Weight);
    private sealed record RubricRow(Guid Id, Guid CourseId, Guid? CheckpointId, string Name, decimal Weight);
    private sealed record EvaluationRow(
        Guid Id, Guid ProjectId, Guid RubricId, decimal Score, EvaluationStatus Status, DateTime UpdatedAt);
}
