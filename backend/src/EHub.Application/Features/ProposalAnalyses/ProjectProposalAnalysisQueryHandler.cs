using System.Text.Json;
using EHub.Application.Common.Interfaces.AI;
using EHub.Application.Common.Interfaces.Persistence;
using EHub.Contracts.ProjectProposals;
using EHub.Domain.Entities;
using EHub.Domain.Enums;
using EHub.Shared.Constants;
using EHub.Shared.Errors;
using EHub.Shared.Results;
using Microsoft.EntityFrameworkCore;

namespace EHub.Application.Features.ProposalAnalyses;

public sealed class ProjectProposalAnalysisQueryHandler : IProjectProposalAnalysisQueryHandler
{
    private readonly IApplicationDbContext _context;
    private readonly IAiFeatureGate _featureGate;

    public ProjectProposalAnalysisQueryHandler(IApplicationDbContext context, IAiFeatureGate featureGate)
    {
        _context = context;
        _featureGate = featureGate;
    }

    public async Task<Result<ProjectProposalAnalysisDto>> GetAsync(
        Guid jobId,
        Guid userId,
        string role,
        CancellationToken cancellationToken = default)
    {
        if (!_featureGate.IsEnabled)
            return Failure(ErrorCodes.AiFeatureDisabled, "AI proposal analysis is currently disabled.");

        var job = await _context.ProjectProposalAnalysisJobs
            .AsNoTracking()
            .Include(candidate => candidate.Result)
                .ThenInclude(result => result!.Matches)
                .ThenInclude(match => match.CandidateProposalVersion)
                .ThenInclude(version => version.ProjectProposal)
                .ThenInclude(proposal => proposal.Class)
                .ThenInclude(targetClass => targetClass.Semester)
            .Include(candidate => candidate.ProposalVersion)
                .ThenInclude(version => version.ProjectProposal)
                .ThenInclude(proposal => proposal.Team)
                .ThenInclude(team => team.Class)
                .ThenInclude(targetClass => targetClass.ClassLecturers)
            .Include(candidate => candidate.ProposalVersion)
                .ThenInclude(version => version.ProjectProposal)
                .ThenInclude(proposal => proposal.Team)
                .ThenInclude(team => team.TeamMembers)
                .ThenInclude(member => member.ClassStudent)
                .ThenInclude(enrollment => enrollment.Student)
            .Include(candidate => candidate.ProposalVersion)
                .ThenInclude(version => version.ProjectProposal)
                .ThenInclude(proposal => proposal.Team)
                .ThenInclude(team => team.MentorAssignments)
                .ThenInclude(assignment => assignment.MentorProfile)
            .SingleOrDefaultAsync(candidate => candidate.Id == jobId, cancellationToken);

        if (job == null)
            return Failure(ErrorCodes.ProjectProposalAnalysisNotFound, "The proposal analysis job was not found.");
        if (!CanView(job.ProposalVersion.ProjectProposal.Team, userId, role))
            return Failure(ErrorCodes.ProjectProposalAnalysisAccessDenied, "You cannot view this proposal analysis.");

        var isStudent = IsRole(role, SystemRoles.Student);
        return Result.Success(new ProjectProposalAnalysisDto
        {
            JobId = job.Id,
            ProposalVersionId = job.ProposalVersionId,
            Status = job.Status.ToString(),
            AttemptCount = job.AttemptCount,
            CandidateScope = job.CandidateScope.ToString(),
            IncludeCrossSemester = job.IncludeCrossSemester,
            LanguageMode = job.LanguageMode.ToString(),
            RequestedAtUtc = job.CreatedAtUtc,
            ProcessingStartedAtUtc = job.ProcessingStartedAtUtc,
            CompletedAtUtc = job.CompletedAtUtc,
            FailedAtUtc = job.FailedAtUtc,
            FailureCode = job.Status == ProjectProposalAnalysisJobStatus.Failed ? job.LastErrorCode : null,
            CanViewDetailedReport = !isStudent,
            Report = job.Result == null ? null : new ProjectProposalAnalysisReportDto
            {
                Summary = job.Result.Summary,
                OverlapRisk = job.Result.OverlapRisk.ToString(),
                PotentialDifferentiators = DeserializeItems(job.Result.PotentialDifferentiatorsJson),
                Limitations = DeserializeItems(job.Result.LimitationsJson),
                Provider = job.Result.Provider,
                Model = job.Result.Model,
                PromptVersion = job.Result.PromptVersion,
                OutputSchemaVersion = job.Result.OutputSchemaVersion,
                EmbeddingProvider = job.Result.EmbeddingProvider,
                EmbeddingModel = job.Result.EmbeddingModel,
                EmbeddingDimension = job.Result.EmbeddingDimension,
                TextSchemaVersion = job.Result.TextSchemaVersion,
                RetrievalVersion = job.Result.RetrievalVersion,
                Matches = isStudent ? [] : job.Result.Matches
                    .OrderBy(match => match.Rank)
                    .Select(ToMatchDto)
                    .ToArray(),
                GeneratedAtUtc = job.Result.GeneratedAtUtc
            }
        });
    }

    private static bool CanView(Team team, Guid userId, string role)
    {
        if (IsRole(role, SystemRoles.Admin)) return true;
        if (IsRole(role, SystemRoles.Lecturer))
            return team.Class.PrimaryLecturerId == userId
                || team.Class.ClassLecturers.Any(assignment => assignment.LecturerId == userId);
        if (IsRole(role, SystemRoles.Mentor))
            return team.MentorAssignments.Any(assignment => assignment.MentorProfile.UserId == userId
                && assignment.Status == MentorAssignmentStatus.Active
                && assignment.EndedAt == null);
        return IsRole(role, SystemRoles.Student) && team.TeamMembers.Any(member =>
            member.CountsTowardActiveTeam
            && member.ClassStudent.EnrollmentStatus == EnrollmentStatus.Active
            && member.ClassStudent.Student.UserId == userId);
    }

    private static IReadOnlyCollection<string> DeserializeItems(string json)
    {
        try { return JsonSerializer.Deserialize<string[]>(json) ?? []; }
        catch (JsonException) { return []; }
    }

    private static ProjectProposalAnalysisMatchDto ToMatchDto(ProjectProposalAnalysisMatch match)
    {
        ProjectProposalSnapshotDto? snapshot = null;
        try
        {
            snapshot = JsonSerializer.Deserialize<ProjectProposalSnapshotDto>(
                match.CandidateProposalVersion.SnapshotJson,
                new JsonSerializerOptions(JsonSerializerDefaults.Web));
        }
        catch (JsonException)
        {
            // Historical evidence remains available even if its display snapshot can no longer be parsed.
        }

        var proposal = match.CandidateProposalVersion.ProjectProposal;
        return new ProjectProposalAnalysisMatchDto
        {
            Rank = match.Rank,
            ProposalVersionId = match.CandidateProposalVersionId,
            ProjectProposalId = proposal.Id,
            ProjectId = proposal.ProjectId,
            TeamId = proposal.TeamId,
            ClassId = proposal.ClassId,
            ClassCode = proposal.Class.ClassCode,
            SemesterCode = proposal.Class.Semester.Code,
            Title = snapshot?.Title ?? string.Empty,
            StartupName = snapshot?.StartupName ?? string.Empty,
            SemanticSimilarity = Math.Max(0, match.SemanticSimilarity),
            SubmittedAtUtc = match.CandidateProposalVersion.CreatedAt
        };
    }

    private static bool IsRole(string role, string expected) =>
        string.Equals(role, expected, StringComparison.OrdinalIgnoreCase);

    private static Result<ProjectProposalAnalysisDto> Failure(string code, string message) =>
        Result.Failure<ProjectProposalAnalysisDto>(new Error(code, message));
}
