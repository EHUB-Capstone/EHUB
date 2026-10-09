using EHub.Application.Common.Interfaces.Persistence;
using EHub.Contracts.Teams;
using EHub.Domain.Entities;
using EHub.Domain.Enums;
using EHub.Shared.Constants;
using EHub.Shared.Errors;
using EHub.Shared.Results;
using Microsoft.EntityFrameworkCore;

namespace EHub.Application.Features.Teams.Lineage;

/// <summary>
/// Read-only history of a team across semesters. Access is decided per term, never from the
/// lineage or the requested team id alone:
/// admin sees everything; a lecturer sees terms up to the latest one taught in their classes;
/// a student sees terms up to the latest one they belonged to, and scores only for terms they were in;
/// a mentor sees only the terms they are assigned to (no scores).
/// </summary>
public sealed class TeamLineageHandler : ITeamLineageHandler
{
    private readonly IApplicationDbContext _context;

    public TeamLineageHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    private sealed record Term(Team Team, DateOnly SortDate, bool CanViewSubmissions, bool CanViewScores);

    public async Task<Result<TeamLineageDto>> GetLineageAsync(
        Guid teamId, Guid userId, string role, CancellationToken cancellationToken = default)
    {
        var access = await LoadAccessibleTermsAsync(teamId, userId, role, cancellationToken);
        if (access.IsFailure) return Result.Failure<TeamLineageDto>(access.Error);

        var (lineageId, terms) = access.Value;
        return Result.Success(new TeamLineageDto
        {
            TeamLineageId = lineageId,
            Terms = terms.Select(term => new TeamLineageTermDto
            {
                TeamId = term.Team.Id,
                ClassId = term.Team.ClassId,
                ClassCode = term.Team.Class.ClassCode,
                SemesterId = term.Team.Class.SemesterId,
                SemesterCode = term.Team.Class.Semester.Code,
                TeamName = term.Team.TeamName,
                ProjectName = term.Team.Project?.Name,
                ProjectStatus = term.Team.Project?.Status.ToString(),
                IsCurrent = term.Team.Id == teamId,
                CanViewSubmissions = term.CanViewSubmissions,
                CanViewScores = term.CanViewScores,
                Members = term.Team.TeamMembers
                    .OrderBy(member => member.JoinedAt)
                    .Select(member => new TeamLineageMemberDto
                    {
                        StudentId = member.StudentId,
                        FullName = member.ClassStudent.Student.FullName,
                        RollNumber = member.ClassStudent.Student.RollNumber,
                        IsLeader = member.RoleInTeam == TeamMemberRole.Leader
                    })
                    .ToArray()
            }).ToArray()
        });
    }

    public async Task<Result<IReadOnlyCollection<TeamLineageSubmissionDto>>> GetTermSubmissionsAsync(
        Guid teamId, Guid termTeamId, Guid userId, string role, CancellationToken cancellationToken = default)
    {
        var access = await LoadAccessibleTermsAsync(teamId, userId, role, cancellationToken);
        if (access.IsFailure) return Result.Failure<IReadOnlyCollection<TeamLineageSubmissionDto>>(access.Error);

        // The requested term must be one of the terms this caller may see in this lineage.
        var term = access.Value.Terms.FirstOrDefault(item => item.Team.Id == termTeamId);
        if (term == null)
        {
            return Result.Failure<IReadOnlyCollection<TeamLineageSubmissionDto>>(
                new Error(ErrorCodes.TeamNotFound, "The requested team term was not found."));
        }

        if (!term.CanViewSubmissions)
        {
            return Result.Failure<IReadOnlyCollection<TeamLineageSubmissionDto>>(
                new Error(ErrorCodes.ClassAccessDenied, "You cannot view submissions of this team term."));
        }

        var submissions = await _context.Submissions.AsNoTracking()
            .Where(submission => submission.TeamId == termTeamId && submission.Status != SubmissionStatus.Draft)
            .Select(submission => new
            {
                submission.Id,
                submission.CheckpointId,
                CheckpointName = submission.Checkpoint.Name,
                submission.Title,
                submission.Status,
                submission.VersionNumber,
                submission.SubmittedAt
            })
            .OrderBy(submission => submission.CheckpointName)
            .ThenBy(submission => submission.VersionNumber)
            .ToListAsync(cancellationToken);

        var evaluations = new Dictionary<Guid, List<TeamLineageEvaluationDto>>();
        if (term.CanViewScores && submissions.Count > 0)
        {
            var submissionIds = submissions.Select(submission => submission.Id).ToArray();
            var includeUnpublished = IsRole(role, SystemRoles.Admin) || IsRole(role, SystemRoles.Lecturer);
            var rows = await _context.Evaluations.AsNoTracking()
                .Where(evaluation => evaluation.SubmissionId.HasValue &&
                                     submissionIds.Contains(evaluation.SubmissionId.Value) &&
                                     (includeUnpublished ||
                                      evaluation.Status == EvaluationStatus.Published ||
                                      evaluation.Status == EvaluationStatus.Locked))
                .Select(evaluation => new
                {
                    SubmissionId = evaluation.SubmissionId!.Value,
                    evaluation.Id,
                    evaluation.EvaluatorRole,
                    evaluation.TotalScore,
                    evaluation.MaxTotalScore,
                    evaluation.OverallFeedback
                })
                .ToListAsync(cancellationToken);
            foreach (var row in rows)
            {
                if (!evaluations.TryGetValue(row.SubmissionId, out var list))
                    evaluations[row.SubmissionId] = list = [];
                list.Add(new TeamLineageEvaluationDto
                {
                    Id = row.Id,
                    EvaluatorRole = row.EvaluatorRole.ToString(),
                    TotalScore = row.TotalScore,
                    MaxTotalScore = row.MaxTotalScore,
                    OverallFeedback = row.OverallFeedback
                });
            }
        }

        return Result.Success<IReadOnlyCollection<TeamLineageSubmissionDto>>(submissions
            .Select(submission => new TeamLineageSubmissionDto
            {
                Id = submission.Id,
                CheckpointId = submission.CheckpointId,
                CheckpointName = submission.CheckpointName,
                Title = submission.Title,
                Status = submission.Status.ToString(),
                VersionNumber = submission.VersionNumber,
                SubmittedAt = submission.SubmittedAt,
                Evaluations = term.CanViewScores
                    ? (evaluations.TryGetValue(submission.Id, out var list) ? list : [])
                    : null
            })
            .ToArray());
    }

    public async Task<Result<TeamContinuityReportDto>> GetReportAsync(
        Guid semesterId, string role, CancellationToken cancellationToken = default)
    {
        if (!IsRole(role, SystemRoles.Admin))
        {
            return Result.Failure<TeamContinuityReportDto>(
                new Error(ErrorCodes.ClassAccessDenied, "Only an administrator can view the continuity report."));
        }

        var semester = await _context.Semesters.AsNoTracking()
            .Where(item => item.Id == semesterId)
            .Select(item => new { item.Id, item.Code })
            .FirstOrDefaultAsync(cancellationToken);
        if (semester == null)
        {
            return Result.Failure<TeamContinuityReportDto>(
                new Error(ErrorCodes.TeamNotFound, "The requested semester was not found."));
        }

        var continuedTeams = await _context.Teams.AsNoTracking()
            .Where(team => team.Class.SemesterId == semesterId && team.PreviousTeamId != null)
            .Select(team => new { team.ClassId, HasContinuedProject = team.Project != null && team.Project.PreviousProjectId != null })
            .ToListAsync(cancellationToken);
        var dissolved = await _context.TeamContinuations.AsNoTracking()
            .Where(item => item.TargetSemesterId == semesterId && item.Status == TeamContinuationStatus.Dissolved)
            .Select(item => item.TargetClassId)
            .ToListAsync(cancellationToken);

        var classIds = continuedTeams.Select(item => item.ClassId).Concat(dissolved).Distinct().ToArray();
        var classCodes = await _context.Classes.AsNoTracking()
            .Where(item => classIds.Contains(item.Id))
            .ToDictionaryAsync(item => item.Id, item => item.ClassCode, cancellationToken);

        return Result.Success(new TeamContinuityReportDto
        {
            SemesterId = semester.Id,
            SemesterCode = semester.Code,
            ContinuedTeamCount = continuedTeams.Count,
            ContinuedProjectCount = continuedTeams.Count(item => item.HasContinuedProject),
            DissolvedCount = dissolved.Count,
            Classes = classIds
                .Select(classId => new TeamContinuityReportItemDto
                {
                    ClassId = classId,
                    ClassCode = classCodes.GetValueOrDefault(classId, string.Empty),
                    ContinuedTeamCount = continuedTeams.Count(item => item.ClassId == classId),
                    DissolvedCount = dissolved.Count(id => id == classId)
                })
                .OrderBy(item => item.ClassCode)
                .ToArray()
        });
    }

    private async Task<Result<(Guid LineageId, IReadOnlyList<Term> Terms)>> LoadAccessibleTermsAsync(
        Guid teamId, Guid userId, string role, CancellationToken ct)
    {
        var lineageId = await _context.Teams.AsNoTracking()
            .Where(team => team.Id == teamId)
            .Select(team => (Guid?)team.TeamLineageId)
            .FirstOrDefaultAsync(ct);
        if (!lineageId.HasValue)
        {
            return Result.Failure<(Guid, IReadOnlyList<Term>)>(
                new Error(ErrorCodes.TeamNotFound, "The requested team was not found."));
        }

        var teams = await _context.Teams.AsNoTracking()
            .Include(team => team.Class).ThenInclude(item => item.Semester)
            .Include(team => team.Class).ThenInclude(item => item.ClassLecturers)
            .Include(team => team.TeamMembers).ThenInclude(member => member.ClassStudent).ThenInclude(item => item.Student)
            .Include(team => team.MentorAssignments).ThenInclude(assignment => assignment.MentorProfile)
            .Include(team => team.Project)
            .Where(team => team.TeamLineageId == lineageId.Value)
            .ToListAsync(ct);

        var ordered = teams
            .Select(team => (Team: team, SortDate: SortDateOf(team.Class.Semester)))
            .OrderBy(item => item.SortDate)
            .ToList();

        var terms = new List<Term>();
        if (IsRole(role, SystemRoles.Admin))
        {
            terms.AddRange(ordered.Select(item => new Term(item.Team, item.SortDate, true, true)));
        }
        else if (IsRole(role, SystemRoles.Lecturer))
        {
            var taught = ordered
                .Where(item => item.Team.Class.PrimaryLecturerId == userId ||
                               item.Team.Class.ClassLecturers.Any(lecturer => lecturer.LecturerId == userId))
                .Select(item => item.SortDate)
                .ToList();
            if (taught.Count > 0)
            {
                var latest = taught.Max();
                terms.AddRange(ordered
                    .Where(item => item.SortDate <= latest)
                    .Select(item => new Term(item.Team, item.SortDate, true, true)));
            }
        }
        else if (IsRole(role, SystemRoles.Student))
        {
            var studentId = await _context.Students.AsNoTracking()
                .Where(student => student.UserId == userId)
                .Select(student => (Guid?)student.Id)
                .FirstOrDefaultAsync(ct);
            if (studentId.HasValue)
            {
                var memberOf = ordered
                    .Where(item => item.Team.TeamMembers.Any(member => member.StudentId == studentId.Value))
                    .ToList();
                if (memberOf.Count > 0)
                {
                    var latest = memberOf.Max(item => item.SortDate);
                    var memberTeamIds = memberOf.Select(item => item.Team.Id).ToHashSet();
                    terms.AddRange(ordered
                        .Where(item => item.SortDate <= latest)
                        .Select(item => new Term(item.Team, item.SortDate, true, memberTeamIds.Contains(item.Team.Id))));
                }
            }
        }
        else if (IsRole(role, SystemRoles.Mentor))
        {
            terms.AddRange(ordered
                .Where(item => item.Team.MentorAssignments.Any(assignment =>
                    assignment.MentorProfile.UserId == userId &&
                    assignment.Status == MentorAssignmentStatus.Active &&
                    assignment.EndedAt == null))
                .Select(item => new Term(item.Team, item.SortDate, true, false)));
        }

        if (terms.Count == 0)
        {
            return Result.Failure<(Guid, IReadOnlyList<Term>)>(
                new Error(ErrorCodes.ClassAccessDenied, "You do not have access to this team's history."));
        }

        return Result.Success((lineageId.Value, (IReadOnlyList<Term>)terms));
    }

    private static DateOnly SortDateOf(Semester semester) =>
        semester.StartDate ?? new DateOnly(
            Math.Clamp(semester.Year, 1, 9998),
            semester.Term switch { SemesterTerm.Spring => 1, SemesterTerm.Summer => 5, _ => 9 },
            1);

    private static bool IsRole(string role, string expected) =>
        string.Equals(role, expected, StringComparison.OrdinalIgnoreCase);
}
