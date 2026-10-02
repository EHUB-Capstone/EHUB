using EHub.Application.Common.Interfaces.Persistence;
using EHub.Contracts.Workspaces;
using EHub.Domain.Enums;
using EHub.Shared.Constants;
using EHub.Shared.Errors;
using EHub.Shared.Results;
using Microsoft.EntityFrameworkCore;

namespace EHub.Application.Features.Workspaces.StudentPreviousScores;

public sealed class StudentPreviousScoresHandler(IApplicationDbContext context)
{
    public async Task<Result<StudentPreviousScoresResponse>> GetAsync(
        Guid currentClassId, Guid studentId, Guid userId, string role,
        CancellationToken cancellationToken = default)
    {
        var isAdmin = string.Equals(role, SystemRoles.Admin, StringComparison.OrdinalIgnoreCase);
        var isLecturer = string.Equals(role, SystemRoles.Lecturer, StringComparison.OrdinalIgnoreCase);
        if (!isAdmin && !isLecturer) return Denied();

        // This relationship is rechecked on every read, so dropping the enrollment
        // or changing the teaching assignment immediately revokes historical access.
        var enrollment = await context.ClassStudents.AsNoTracking()
            .Where(item => item.ClassId == currentClassId && item.StudentId == studentId &&
                item.EnrollmentStatus == EnrollmentStatus.Active &&
                item.Class.Status == ClassStatus.Active && item.Class.Semester.Status == SemesterStatus.Active &&
                (isAdmin || item.Class.PrimaryLecturerId == userId ||
                 item.Class.ClassLecturers.Any(assignment => assignment.LecturerId == userId)))
            .Select(item => new { item.CourseId, item.Class.Semester.Year, item.Class.Semester.Term })
            .SingleOrDefaultAsync(cancellationToken);
        if (enrollment is null) return Denied();

        // Term is stored as text; order the small semester catalog by the enum's
        // chronological value, not by its database string representation.
        var semesters = await context.Semesters.AsNoTracking()
            .Where(item => item.Year <= enrollment.Year &&
                (item.Status == SemesterStatus.Completed || item.Status == SemesterStatus.Archived))
            .Select(item => new { item.Id, item.Code, item.Year, item.Term })
            .ToArrayAsync(cancellationToken);
        var previous = semesters
            .Where(item => item.Year < enrollment.Year || item.Term < enrollment.Term)
            .OrderByDescending(item => item.Year).ThenByDescending(item => item.Term)
            .FirstOrDefault();
        if (previous is null)
            return Result.Success(new StudentPreviousScoresResponse { StudentId = studentId });

        var projectIds = await context.TeamMembers.AsNoTracking()
            .Where(item => item.StudentId == studentId && item.CountsTowardActiveTeam &&
                item.ClassStudent.EnrollmentStatus == EnrollmentStatus.Completed &&
                item.ClassStudent.SemesterId == previous.Id && item.ClassStudent.CourseId == enrollment.CourseId &&
                item.Team.Project != null)
            .Select(item => item.Team.Project!.Id).Distinct().ToArrayAsync(cancellationToken);
        var evaluations = await context.Evaluations.AsNoTracking()
            .Where(item => projectIds.Contains(item.ProjectId) && item.Rubric.CourseId == enrollment.CourseId &&
                (item.Status == EvaluationStatus.Submitted || item.Status == EvaluationStatus.Published))
            .OrderByDescending(item => item.UpdatedAt ?? item.CreatedAt).ThenByDescending(item => item.Id)
            .Select(item => new
            {
                item.RubricId, item.Status, item.Rubric.Name, item.Rubric.CheckpointId,
                Number = item.Rubric.Checkpoint == null ? (int?)null : item.Rubric.Checkpoint.CheckpointNumber,
                Weight = item.Rubric.Checkpoint == null ? item.Rubric.CourseWeight : item.Rubric.Checkpoint.CourseWeight,
                Score = item.MemberScores.Where(score => score.StudentId == studentId)
                    .Select(score => (decimal?)score.Score).FirstOrDefault() ?? item.TotalScore
            }).ToArrayAsync(cancellationToken);
        var components = evaluations.GroupBy(item => item.RubricId).Select(group => group.First())
            .Where(item => item.Status == EvaluationStatus.Published)
            .OrderBy(item => item.Number).ThenBy(item => item.Name)
            .Select(item => new StudentPreviousScoreResponse
            {
                AssessmentId = item.RubricId, CheckpointNumber = item.Number, Name = item.Name,
                Weight = item.Weight, Score = item.Score
            }).ToArray();
        return Result.Success(new StudentPreviousScoresResponse
        {
            StudentId = studentId, SemesterCode = previous.Code, Components = components
        });
    }

    private static Result<StudentPreviousScoresResponse> Denied() =>
        Result.Failure<StudentPreviousScoresResponse>(ErrorCodes.WorkspaceAccessDenied,
            "You do not have access to this student's previous scores.");
}
