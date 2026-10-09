using EHub.Application.Common.Interfaces.Persistence;
using EHub.Contracts.Workspaces;
using EHub.Domain.Entities;
using EHub.Domain.Enums;
using EHub.Shared.Constants;
using EHub.Shared.Errors;
using EHub.Shared.Results;
using Microsoft.EntityFrameworkCore;

namespace EHub.Application.Features.Workspaces.EvaluationReportExport;

public sealed class EvaluationReportExportHandler(IApplicationDbContext context) : IEvaluationReportExportHandler
{
    private const int MaximumTeamCount = 500;
    private const int MaximumCheckpointSelections = 5_000;

    public async Task<Result<(byte[] FileBytes, string ContentType, string FileName)>> HandleAsync(
        EvaluationReportExportRequest request,
        Guid currentUserId,
        string currentUserRole,
        CancellationToken cancellationToken = default)
    {
        var isAdmin = IsRole(currentUserRole, SystemRoles.Admin);
        var isLecturer = IsRole(currentUserRole, SystemRoles.Lecturer);
        if (!isAdmin && !isLecturer)
        {
            return Denied("You do not have permission to export evaluation reports.");
        }

        var requestedScopes = request?.Teams ?? Array.Empty<EvaluationReportTeamScopeRequest>();
        if (requestedScopes.Count == 0 || requestedScopes.Count > MaximumTeamCount ||
            requestedScopes.Any(scope => scope.TeamId == Guid.Empty || scope.CheckpointNumbers is null ||
                                          scope.CheckpointNumbers.Count == 0 ||
                                          scope.CheckpointNumbers.Any(number => number <= 0)) ||
            requestedScopes.Sum(scope => scope.CheckpointNumbers?.Count ?? 0) > MaximumCheckpointSelections)
        {
            return Invalid($"Select between 1 and {MaximumTeamCount} valid teams and no more than {MaximumCheckpointSelections} checkpoint selections.");
        }

        var scopesByTeam = requestedScopes
            .GroupBy(scope => scope.TeamId)
            .ToDictionary(
                group => group.Key,
                group => group.SelectMany(scope => scope.CheckpointNumbers).Distinct().ToHashSet());
        var teamIds = scopesByTeam.Keys.ToArray();

        var teamQuery = context.Teams.AsNoTracking()
            .Where(team => teamIds.Contains(team.Id) && team.Status == TeamStatus.Active);
        if (isLecturer)
        {
            teamQuery = teamQuery.Where(team =>
                team.Class.PrimaryLecturerId == currentUserId ||
                team.Class.ClassLecturers.Any(assignment => assignment.LecturerId == currentUserId));
        }

        var teams = await teamQuery
            .Include(team => team.Class).ThenInclude(targetClass => targetClass.Course)
            .Include(team => team.Class).ThenInclude(targetClass => targetClass.Semester)
            .Include(team => team.Class).ThenInclude(targetClass => targetClass.ClassLecturers)
            .Include(team => team.TeamMembers).ThenInclude(member => member.ClassStudent).ThenInclude(enrollment => enrollment.Student)
            .Include(team => team.Project)
            .OrderBy(team => team.Class.ClassCode)
            .ThenBy(team => team.TeamName)
            .ToArrayAsync(cancellationToken);
        if (teams.Length != teamIds.Length)
        {
            return Denied("One or more requested teams are unavailable or outside your access scope.");
        }

        var courseIds = teams.Select(team => team.Class.CourseId).Distinct().ToArray();
        var assessments = await context.Rubrics.AsNoTracking()
            .Where(rubric => rubric.CourseId.HasValue && courseIds.Contains(rubric.CourseId.Value) &&
                             rubric.ClassId == null && rubric.CheckpointId == null &&
                             rubric.Status == RubricStatus.Active)
            .OrderBy(rubric => rubric.Name)
            .ThenBy(rubric => rubric.Id)
            .ToArrayAsync(cancellationToken);
        var checkpointRubrics = await context.Rubrics.AsNoTracking()
            .Include(rubric => rubric.Checkpoint)
            .Where(rubric => rubric.CourseId.HasValue && courseIds.Contains(rubric.CourseId.Value) &&
                             rubric.ClassId == null && rubric.CheckpointId != null &&
                             rubric.Status == RubricStatus.Active && rubric.Criteria.Any() &&
                             rubric.Checkpoint != null && rubric.Checkpoint.ClassId == null &&
                             rubric.Checkpoint.Status != CheckpointStatus.Archived)
            .OrderBy(rubric => rubric.Checkpoint!.CheckpointNumber)
            .ThenBy(rubric => rubric.Id)
            .ToArrayAsync(cancellationToken);

        var columns = new List<EvaluationReportColumn>();
        foreach (var courseId in courseIds.OrderBy(id => id))
        {
            columns.AddRange(assessments
                .Where(assessment => assessment.CourseId == courseId)
                .Select(assessment => new EvaluationReportColumn(
                    courseId, assessment.Id, assessment.Name, 0)));

            var checkpointNumbers = teams
                .Where(team => team.Class.CourseId == courseId)
                .SelectMany(team => scopesByTeam[team.Id])
                .ToHashSet();
            columns.AddRange(checkpointRubrics
                .Where(rubric => rubric.CourseId == courseId &&
                                 checkpointNumbers.Contains(rubric.Checkpoint!.CheckpointNumber))
                .GroupBy(rubric => rubric.CheckpointId)
                .Select(group => group.First())
                .Select(rubric => new EvaluationReportColumn(
                    courseId,
                    rubric.Id,
                    $"Checkpoint {rubric.Checkpoint!.CheckpointNumber}",
                    10_000 + rubric.Checkpoint.CheckpointNumber)));
        }

        var distinctColumns = columns
            .GroupBy(column => (column.CourseId, column.RubricId))
            .Select(group => group.First())
            .ToArray();
        var rubricIds = distinctColumns.Select(column => column.RubricId).ToArray();
        var projectIds = teams.Where(team => team.Project is not null).Select(team => team.Project!.Id).ToArray();
        var evaluations = projectIds.Length == 0 || rubricIds.Length == 0
            ? Array.Empty<Evaluation>()
            : await context.Evaluations.AsNoTracking()
                .Where(evaluation => projectIds.Contains(evaluation.ProjectId) &&
                                     rubricIds.Contains(evaluation.RubricId) &&
                                     (evaluation.Status == EvaluationStatus.Submitted ||
                                      evaluation.Status == EvaluationStatus.Published))
                .Include(evaluation => evaluation.MemberScores)
                .OrderByDescending(evaluation => evaluation.UpdatedAt ?? evaluation.CreatedAt)
                .ThenByDescending(evaluation => evaluation.Id)
                .ToArrayAsync(cancellationToken);
        var latestEvaluations = evaluations
            .GroupBy(evaluation => (evaluation.ProjectId, evaluation.RubricId))
            .Select(group => group.First())
            .ToArray();

        var students = teams.SelectMany(team =>
            {
                var includedRubricIds = distinctColumns
                    .Where(column => column.CourseId == team.Class.CourseId &&
                        (column.SortOrder < 10_000 || checkpointRubrics.Any(rubric =>
                            rubric.Id == column.RubricId &&
                            scopesByTeam[team.Id].Contains(rubric.Checkpoint!.CheckpointNumber))))
                    .Select(column => column.RubricId)
                    .ToHashSet();
                return team.TeamMembers
                    .Where(member => member.CountsTowardActiveTeam &&
                                     member.ClassStudent.EnrollmentStatus == EnrollmentStatus.Active)
                    .Select(member => new EvaluationReportStudentRow(
                        member.ClassStudent.Student.RollNumber ?? string.Empty,
                        team.Class.CourseId,
                        team.Project?.Id,
                        member.StudentId,
                        includedRubricIds));
            })
            .OrderBy(student => student.RollNumber, StringComparer.OrdinalIgnoreCase)
            .ThenBy(student => student.StudentId)
            .ToArray();
        var scores = latestEvaluations.SelectMany(evaluation => teams
                .Where(team => team.Project?.Id == evaluation.ProjectId)
                .SelectMany(team => team.TeamMembers
                    .Where(member => member.CountsTowardActiveTeam &&
                                     member.ClassStudent.EnrollmentStatus == EnrollmentStatus.Active)
                    .Select(member => new
                    {
                        evaluation.ProjectId,
                        evaluation.RubricId,
                        member.StudentId,
                        Score = evaluation.MemberScores
                            .FirstOrDefault(score => score.StudentId == member.StudentId)?.Score ?? evaluation.TotalScore
                    })))
            .ToDictionary(item => (item.ProjectId, item.RubricId, item.StudentId), item => item.Score);

        var fileBytes = EvaluationReportWorkbookBuilder.Build(distinctColumns, students, scores);
        return Result.Success((fileBytes, EvaluationReportWorkbookBuilder.ContentType, BuildFileName(teams)));
    }

    private static string BuildFileName(IReadOnlyCollection<Team> teams)
    {
        var classCodes = teams.Select(team => team.Class.ClassCode).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var semesterCodes = teams.Select(team => team.Class.Semester.Code).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var prefix = classCodes.Length == 1 ? classCodes[0] : semesterCodes.Length == 1 ? semesterCodes[0] : "evaluation";
        var safePrefix = string.Concat(prefix.Select(character => Path.GetInvalidFileNameChars().Contains(character) ? '_' : character));
        return $"{safePrefix}_evaluation_report.xlsx";
    }

    private static Result<(byte[], string, string)> Denied(string message) =>
        Result.Failure<(byte[], string, string)>(new Error(ErrorCodes.WorkspaceAccessDenied, message));

    private static Result<(byte[], string, string)> Invalid(string message) =>
        Result.Failure<(byte[], string, string)>(new Error(ErrorCodes.WorkspaceValidationError, message));

    private static bool IsRole(string role, string expected) =>
        string.Equals(role, expected, StringComparison.OrdinalIgnoreCase);
}
