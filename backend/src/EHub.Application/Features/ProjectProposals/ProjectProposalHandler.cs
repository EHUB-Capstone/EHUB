using System.Text.Json;
using EHub.Application.Common.Exceptions;
using EHub.Application.Common.Interfaces.AI;
using EHub.Application.Common.Interfaces.Persistence;
using EHub.Application.Common.Interfaces.Services;
using EHub.Application.Features.Classes.Common;
using EHub.Contracts.ProjectProposals;
using EHub.Domain.Entities;
using EHub.Domain.Enums;
using EHub.Shared.Constants;
using EHub.Shared.Errors;
using EHub.Shared.Results;
using Microsoft.EntityFrameworkCore;

namespace EHub.Application.Features.ProjectProposals;

public sealed class ProjectProposalHandler : IProjectProposalHandler
{
    private const string SnapshotSchemaVersion = "project-proposal-snapshot-v1";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IApplicationDbContext _context;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IAiFeatureGate _aiFeatureGate;

    public ProjectProposalHandler(
        IApplicationDbContext context,
        IUnitOfWork unitOfWork,
        IDateTimeProvider dateTimeProvider,
        IAiFeatureGate aiFeatureGate)
    {
        _context = context;
        _unitOfWork = unitOfWork;
        _dateTimeProvider = dateTimeProvider;
        _aiFeatureGate = aiFeatureGate;
    }

    public async Task<Result<ProjectProposalDto>> GetByTeamAsync(
        Guid teamId,
        Guid userId,
        string role,
        CancellationToken cancellationToken = default)
    {
        var team = await TeamQuery().FirstOrDefaultAsync(item => item.Id == teamId, cancellationToken);
        if (team == null) return Failure(ErrorCodes.TeamNotFound, "The requested team was not found.");
        if (!CanView(team, userId, role)) return Failure(ErrorCodes.ProjectProposalAccessDenied, "You cannot view this project proposal.");

        var proposal = await ProposalQuery().FirstOrDefaultAsync(item => item.TeamId == teamId, cancellationToken);
        return proposal == null
            ? Failure(ErrorCodes.ProjectProposalNotFound, "The team has not created a project proposal.")
            : Result.Success(ToDto(proposal));
    }

    public async Task<Result<ProjectProposalDto>> CreateAsync(
        Guid teamId,
        CreateProjectProposalRequest request,
        Guid userId,
        string role,
        CancellationToken cancellationToken = default)
    {
        var contentError = ValidateDraftContent(request);
        if (contentError != null) return Failure(contentError);

        try
        {
            return await _unitOfWork.ExecuteInSerializableTransactionAsync(async transactionCancellationToken =>
            {
                var team = await TeamQuery(tracking: true).FirstOrDefaultAsync(item => item.Id == teamId, transactionCancellationToken);
                if (team == null) return Failure(ErrorCodes.TeamNotFound, "The requested team was not found.");
                var accessError = GetDraftMutationError(team, userId, role);
                if (accessError != null) return Failure(accessError);
                if (team.Project == null) return Failure(ErrorCodes.WorkspaceNotFound, "Create the project workspace before creating a project proposal.");
                if (await _context.ProjectProposals.AnyAsync(item => item.TeamId == teamId || item.ProjectId == team.Project.Id, transactionCancellationToken))
                    return Failure(ErrorCodes.ProjectProposalStateInvalid, "This team already has a project proposal.");

                var now = _dateTimeProvider.UtcNow;
                var proposal = new ProjectProposal
                {
                    ProjectId = team.Project.Id,
                    Project = team.Project,
                    TeamId = team.Id,
                    Team = team,
                    ClassId = team.ClassId,
                    Class = team.Class,
                    Status = ProjectProposalStatus.Draft,
                    CreatedById = userId,
                    CreatedBy = userId,
                    CreatedAt = now
                };
                ApplyContent(proposal, request);
                proposal.Versions.Add(CreateVersion(proposal, 1, ProjectProposalVersionPurpose.DraftSave,
                    request.ChangeNote, userId, now));
                _context.ProjectProposals.Add(proposal);
                AddActivity(team.Project, userId, "PROJECT_PROPOSAL_CREATED", "Created the detailed project proposal.", now);
                await _context.SaveChangesAsync(transactionCancellationToken);
                return Result.Success(ToDto(proposal));
            }, cancellationToken);
        }
        catch (SerializableTransactionConflictException)
        {
            return Failure(ErrorCodes.ProjectProposalConcurrencyConflict, "The project proposal changed concurrently. Refresh and try again.");
        }
        catch (DbUpdateException)
        {
            return Failure(ErrorCodes.ProjectProposalConcurrencyConflict, "A project proposal was created or changed concurrently. Refresh and try again.");
        }
    }

    public async Task<Result<ProjectProposalDto>> UpdateAsync(
        Guid proposalId,
        UpdateProjectProposalRequest request,
        Guid userId,
        string role,
        CancellationToken cancellationToken = default)
    {
        var contentError = ValidateDraftContent(request);
        if (contentError != null) return Failure(contentError);
        if (!uint.TryParse(request.RowVersion, out var rowVersion))
            return Failure(ErrorCodes.ProjectProposalValidationError, "A valid rowVersion is required.");

        try
        {
            return await _unitOfWork.ExecuteInSerializableTransactionAsync(async transactionCancellationToken =>
            {
                var proposal = await ProposalQuery(tracking: true).FirstOrDefaultAsync(item => item.Id == proposalId, transactionCancellationToken);
                if (proposal == null) return Failure(ErrorCodes.ProjectProposalNotFound, "The project proposal was not found.");
                var accessError = GetDraftMutationError(proposal.Team, userId, role);
                if (accessError != null) return Failure(accessError);
                if (proposal.Version != rowVersion)
                    return Failure(ErrorCodes.ProjectProposalConcurrencyConflict, "The supplied rowVersion is stale. Refresh the project proposal and try again.");
                if (proposal.Status is not (ProjectProposalStatus.Draft or ProjectProposalStatus.NeedsRevision))
                    return Failure(ErrorCodes.ProjectProposalStateInvalid, "Only Draft or NeedsRevision proposals can be edited.");
                if (HasSameContent(proposal, request))
                    return Failure(ErrorCodes.ProjectProposalValidationError, "Change at least one proposal field before saving a new version.");

                ApplyContent(proposal, request);
                if (proposal.Status == ProjectProposalStatus.NeedsRevision)
                    proposal.Status = ProjectProposalStatus.Draft;
                proposal.UpdatedById = userId;
                proposal.UpdatedBy = userId;
                var now = _dateTimeProvider.UtcNow;
                proposal.UpdatedAt = now;
                var version = CreateVersion(proposal, NextVersionNumber(proposal), ProjectProposalVersionPurpose.DraftSave,
                    request.ChangeNote, userId, now);
                proposal.Versions.Add(version);
                _context.ProjectProposalVersions.Add(version);
                AddActivity(proposal.Project, userId, "PROJECT_PROPOSAL_DRAFT_SAVED", "Saved a new project proposal draft version.", now);
                await _context.SaveChangesAsync(transactionCancellationToken);
                return Result.Success(ToDto(proposal));
            }, cancellationToken);
        }
        catch (SerializableTransactionConflictException)
        {
            return Failure(ErrorCodes.ProjectProposalConcurrencyConflict, "The project proposal changed concurrently. Refresh and try again.");
        }
        catch (DbUpdateException)
        {
            return Failure(ErrorCodes.ProjectProposalConcurrencyConflict, "The project proposal version conflicted with another save. Refresh and try again.");
        }
    }

    public async Task<Result<ProjectProposalDto>> SubmitAsync(
        Guid proposalId,
        SubmitProjectProposalRequest request,
        Guid userId,
        string role,
        CancellationToken cancellationToken = default)
    {
        if (!uint.TryParse(request.RowVersion, out var rowVersion))
            return Failure(ErrorCodes.ProjectProposalValidationError, "A valid rowVersion is required.");
        if (Trimmed(request.ChangeNote).Length > 1_000)
            return Failure(ErrorCodes.ProjectProposalValidationError, "Change note must not exceed 1000 characters.");

        try
        {
            return await _unitOfWork.ExecuteInSerializableTransactionAsync(async transactionCancellationToken =>
            {
                var proposal = await ProposalQuery(tracking: true).FirstOrDefaultAsync(item => item.Id == proposalId, transactionCancellationToken);
                if (proposal == null) return Failure(ErrorCodes.ProjectProposalNotFound, "The project proposal was not found.");
                var teamError = GetActiveTeamError(proposal.Team);
                if (teamError != null) return Failure(teamError);
                if (!IsLeader(proposal.Team, userId, role))
                    return Failure(ErrorCodes.ProjectProposalAccessDenied, "Only the active team leader can submit the project proposal.");
                if (proposal.Version != rowVersion)
                    return Failure(ErrorCodes.ProjectProposalConcurrencyConflict, "The project proposal changed concurrently. Refresh and try again.");
                if (proposal.Status != ProjectProposalStatus.Draft)
                    return Failure(ErrorCodes.ProjectProposalStateInvalid, "Only a saved Draft proposal can be submitted.");
                if (proposal.Team.ProjectDirection?.Status != ProjectDirectionStatus.Approved)
                    return Failure(ErrorCodes.ProjectProposalDirectionNotApproved, "The project direction must be approved before the detailed proposal can be submitted.");

                var submissionError = ValidateSubmission(proposal);
                if (submissionError != null) return Failure(submissionError);

                var now = _dateTimeProvider.UtcNow;
                var version = CreateVersion(proposal, NextVersionNumber(proposal), ProjectProposalVersionPurpose.Submission,
                    request.ChangeNote, userId, now);
                proposal.Versions.Add(version);
                _context.ProjectProposalVersions.Add(version);
                if (_aiFeatureGate.IsEnabled)
                {
                    var analysisJob = new ProjectProposalAnalysisJob
                    {
                        ProposalVersionId = version.Id,
                        ProposalVersion = version,
                        Status = ProjectProposalAnalysisJobStatus.Pending,
                        CandidateScope = ProjectProposalAnalysisCandidateScope.AllSystem,
                        IncludeCrossSemester = true,
                        LanguageMode = ProjectProposalAnalysisLanguageMode.VietnameseAndEnglish,
                        ConfigurationVersion = "proposal-analysis-config-v1",
                        RequestedByUserId = userId,
                        AvailableAtUtc = now,
                        CreatedAtUtc = now
                    };
                    version.AnalysisJob = analysisJob;
                    _context.ProjectProposalAnalysisJobs.Add(analysisJob);
                }
                proposal.Status = ProjectProposalStatus.Submitted;
                proposal.SubmittedAt = now;
                proposal.ApprovedAt = null;
                proposal.RejectedAt = null;
                proposal.UpdatedById = userId;
                proposal.UpdatedBy = userId;
                proposal.UpdatedAt = now;
                var lecturerUserIds = proposal.Team.Class.ClassLecturers
                    .Select(assignment => assignment.LecturerId)
                    .Append(proposal.Team.Class.PrimaryLecturerId ?? Guid.Empty)
                    .Where(lecturerId => lecturerId != Guid.Empty)
                    .Distinct()
                    .ToArray();
                AddActivity(proposal.Project, userId, "PROJECT_PROPOSAL_SUBMITTED", "Submitted the detailed project proposal for lecturer review.", now);
                ClassOutbox.Enqueue(_context, "ProjectProposal.Submitted.v1", proposal.ClassId, new
                {
                    TeamId = proposal.TeamId,
                    ProjectProposalId = proposal.Id,
                    ProposalVersionId = version.Id,
                    LecturerUserIds = lecturerUserIds
                }, now);
                await _context.SaveChangesAsync(transactionCancellationToken);
                return Result.Success(ToDto(proposal));
            }, cancellationToken);
        }
        catch (SerializableTransactionConflictException)
        {
            return Failure(ErrorCodes.ProjectProposalConcurrencyConflict, "The project proposal changed concurrently. Refresh and try again.");
        }
        catch (DbUpdateException)
        {
            return Failure(ErrorCodes.ProjectProposalConcurrencyConflict, "The submitted version conflicted with another request. Refresh and try again.");
        }
    }

    public async Task<Result<IReadOnlyCollection<ProjectProposalVersionSummaryDto>>> GetVersionsAsync(
        Guid proposalId,
        Guid userId,
        string role,
        CancellationToken cancellationToken = default)
    {
        var proposal = await ProposalQuery().FirstOrDefaultAsync(item => item.Id == proposalId, cancellationToken);
        if (proposal == null) return FailureVersions(ErrorCodes.ProjectProposalNotFound, "The project proposal was not found.");
        if (!CanView(proposal.Team, userId, role))
            return FailureVersions(ErrorCodes.ProjectProposalAccessDenied, "You cannot view this project proposal.");
        return Result.Success<IReadOnlyCollection<ProjectProposalVersionSummaryDto>>(proposal.Versions
            .OrderByDescending(version => version.VersionNumber)
            .Select(ToVersionSummaryDto)
            .ToArray());
    }

    public async Task<Result<ProjectProposalVersionDto>> GetVersionAsync(
        Guid proposalId,
        Guid versionId,
        Guid userId,
        string role,
        CancellationToken cancellationToken = default)
    {
        var proposal = await ProposalQuery().FirstOrDefaultAsync(item => item.Id == proposalId, cancellationToken);
        if (proposal == null) return FailureVersion(ErrorCodes.ProjectProposalNotFound, "The project proposal was not found.");
        if (!CanView(proposal.Team, userId, role))
            return FailureVersion(ErrorCodes.ProjectProposalAccessDenied, "You cannot view this project proposal.");
        var version = proposal.Versions.SingleOrDefault(item => item.Id == versionId);
        if (version == null) return FailureVersion(ErrorCodes.ProjectProposalVersionNotFound, "The project proposal version was not found.");
        return TryMapVersion(version);
    }

    public async Task<Result<ProjectProposalDto>> RestoreVersionAsync(
        Guid proposalId,
        Guid versionId,
        RestoreProjectProposalVersionRequest request,
        Guid userId,
        string role,
        CancellationToken cancellationToken = default)
    {
        if (!uint.TryParse(request.RowVersion, out var rowVersion))
            return Failure(ErrorCodes.ProjectProposalValidationError, "A valid rowVersion is required.");
        if (Trimmed(request.ChangeNote).Length > 1_000)
            return Failure(ErrorCodes.ProjectProposalValidationError, "Change note must not exceed 1000 characters.");

        try
        {
            return await _unitOfWork.ExecuteInSerializableTransactionAsync(async transactionCancellationToken =>
            {
                var proposal = await ProposalQuery(tracking: true).FirstOrDefaultAsync(item => item.Id == proposalId, transactionCancellationToken);
                if (proposal == null) return Failure(ErrorCodes.ProjectProposalNotFound, "The project proposal was not found.");
                var accessError = GetDraftMutationError(proposal.Team, userId, role);
                if (accessError != null) return Failure(accessError);
                if (proposal.Version != rowVersion)
                    return Failure(ErrorCodes.ProjectProposalConcurrencyConflict, "The project proposal changed concurrently. Refresh and try again.");
                if (proposal.Status is not (ProjectProposalStatus.Draft or ProjectProposalStatus.NeedsRevision))
                    return Failure(ErrorCodes.ProjectProposalStateInvalid, "Only Draft or NeedsRevision proposals can restore a version.");
                var sourceVersion = proposal.Versions.SingleOrDefault(item => item.Id == versionId);
                if (sourceVersion == null)
                    return Failure(ErrorCodes.ProjectProposalVersionNotFound, "The project proposal version was not found.");
                var snapshot = DeserializeSnapshot(sourceVersion);
                if (snapshot == null)
                    return Failure(ErrorCodes.ProjectProposalStateInvalid, "The selected proposal version uses an unsupported or invalid snapshot.");
                if (HasSameContent(proposal, snapshot))
                    return Failure(ErrorCodes.ProjectProposalValidationError, "The selected version already matches the current draft.");

                ApplySnapshot(proposal, snapshot);
                proposal.Status = ProjectProposalStatus.Draft;
                proposal.UpdatedById = userId;
                proposal.UpdatedBy = userId;
                var now = _dateTimeProvider.UtcNow;
                proposal.UpdatedAt = now;
                var note = string.IsNullOrWhiteSpace(request.ChangeNote)
                    ? $"Restored version {sourceVersion.VersionNumber}."
                    : request.ChangeNote;
                var restoredVersion = CreateVersion(proposal, NextVersionNumber(proposal), ProjectProposalVersionPurpose.DraftSave,
                    note, userId, now);
                proposal.Versions.Add(restoredVersion);
                _context.ProjectProposalVersions.Add(restoredVersion);
                AddActivity(proposal.Project, userId, "PROJECT_PROPOSAL_VERSION_RESTORED",
                    $"Restored project proposal version {sourceVersion.VersionNumber} as a new draft.", now);
                await _context.SaveChangesAsync(transactionCancellationToken);
                return Result.Success(ToDto(proposal));
            }, cancellationToken);
        }
        catch (SerializableTransactionConflictException)
        {
            return Failure(ErrorCodes.ProjectProposalConcurrencyConflict, "The project proposal changed concurrently. Refresh and try again.");
        }
        catch (DbUpdateException)
        {
            return Failure(ErrorCodes.ProjectProposalConcurrencyConflict, "The restored version conflicted with another save. Refresh and try again.");
        }
    }

    public async Task<Result<ProjectProposalDto>> ReviewAsync(
        Guid proposalId,
        ReviewProjectProposalRequest request,
        Guid userId,
        string role,
        CancellationToken cancellationToken = default)
    {
        if (!IsRole(role, SystemRoles.Lecturer))
            return Failure(ErrorCodes.ProjectProposalAccessDenied, "Only an assigned lecturer can review a project proposal.");
        if (!uint.TryParse(request.RowVersion, out var rowVersion))
            return Failure(ErrorCodes.ProjectProposalValidationError, "A valid rowVersion is required.");
        if (!Enum.TryParse<ProjectProposalStatus>(request.Decision, true, out var decision)
            || decision is not (ProjectProposalStatus.Approved or ProjectProposalStatus.NeedsRevision or ProjectProposalStatus.Rejected))
            return Failure(ErrorCodes.ProjectProposalValidationError, "Decision must be Approved, NeedsRevision, or Rejected.");
        var feedback = Trimmed(request.Feedback);
        if (feedback.Length is < 3 or > 1_000)
            return Failure(ErrorCodes.ProjectProposalValidationError, "Review feedback must be between 3 and 1000 characters.");

        try
        {
            return await _unitOfWork.ExecuteInSerializableTransactionAsync(async transactionCancellationToken =>
            {
                var proposal = await ProposalQuery(tracking: true).FirstOrDefaultAsync(item => item.Id == proposalId, transactionCancellationToken);
                if (proposal == null) return Failure(ErrorCodes.ProjectProposalNotFound, "The project proposal was not found.");
                var teamError = GetActiveTeamError(proposal.Team);
                if (teamError != null) return Failure(teamError);
                if (!IsAssignedLecturer(proposal.Team, userId))
                    return Failure(ErrorCodes.ProjectProposalAccessDenied, "A lecturer cannot review project proposals outside assigned classes.");
                if (proposal.Version != rowVersion)
                    return Failure(ErrorCodes.ProjectProposalConcurrencyConflict, "The project proposal changed concurrently. Refresh and try again.");
                if (proposal.Status != ProjectProposalStatus.Submitted)
                    return Failure(ErrorCodes.ProjectProposalStateInvalid, "Only a Submitted project proposal can be reviewed.");
                var submittedVersion = LatestSubmittedVersion(proposal);
                if (submittedVersion == null)
                    return Failure(ErrorCodes.ProjectProposalStateInvalid, "The submitted proposal version could not be found.");

                var previousStatus = proposal.Status;
                var now = _dateTimeProvider.UtcNow;
                proposal.Status = decision;
                proposal.ApprovedAt = decision == ProjectProposalStatus.Approved ? now : null;
                proposal.RejectedAt = decision == ProjectProposalStatus.Rejected ? now : null;
                proposal.UpdatedById = userId;
                proposal.UpdatedBy = userId;
                proposal.UpdatedAt = now;
                var review = new ProjectProposalReview
                {
                    ProjectProposalId = proposal.Id,
                    ProjectProposal = proposal,
                    ProposalVersionId = submittedVersion.Id,
                    ProposalVersion = submittedVersion,
                    FromStatus = previousStatus,
                    ToStatus = decision,
                    Feedback = feedback,
                    ReviewedByUserId = userId,
                    OccurredAtUtc = now
                };
                proposal.Reviews.Add(review);
                _context.ProjectProposalReviews.Add(review);
                var studentUserIds = proposal.Team.TeamMembers
                    .Where(member => member.CountsTowardActiveTeam
                        && member.ClassStudent.EnrollmentStatus == EnrollmentStatus.Active
                        && member.ClassStudent.Student.UserId.HasValue)
                    .Select(member => member.ClassStudent.Student.UserId!.Value)
                    .Distinct()
                    .ToArray();
                AddActivity(proposal.Project, userId, "PROJECT_PROPOSAL_REVIEWED", $"Reviewed the project proposal: {decision}.", now);
                ClassOutbox.Enqueue(_context, "ProjectProposal.Reviewed.v1", proposal.ClassId, new
                {
                    TeamId = proposal.TeamId,
                    ProjectProposalId = proposal.Id,
                    ProposalVersionId = submittedVersion.Id,
                    Decision = decision.ToString(),
                    StudentUserIds = studentUserIds
                }, now);
                await _context.SaveChangesAsync(transactionCancellationToken);
                return Result.Success(ToDto(proposal));
            }, cancellationToken);
        }
        catch (SerializableTransactionConflictException)
        {
            return Failure(ErrorCodes.ProjectProposalConcurrencyConflict, "The project proposal was reviewed concurrently. Refresh and try again.");
        }
        catch (DbUpdateException)
        {
            return Failure(ErrorCodes.ProjectProposalConcurrencyConflict, "The project proposal review conflicted with another request. Refresh and try again.");
        }
    }

    private IQueryable<Team> TeamQuery(bool tracking = false)
    {
        var query = tracking ? _context.Teams.AsQueryable() : _context.Teams.AsNoTracking();
        return query
            .Include(team => team.Class).ThenInclude(item => item.ClassLecturers)
            .Include(team => team.Project).ThenInclude(project => project!.ProjectTags)
            .Include(team => team.ProjectDirection)
            .Include(team => team.TeamMembers).ThenInclude(member => member.ClassStudent).ThenInclude(enrollment => enrollment.Student)
            .Include(team => team.MentorAssignments).ThenInclude(assignment => assignment.MentorProfile);
    }

    private IQueryable<ProjectProposal> ProposalQuery(bool tracking = false)
    {
        var query = tracking ? _context.ProjectProposals.AsQueryable() : _context.ProjectProposals.AsNoTracking();
        return query
            .Include(proposal => proposal.Versions).ThenInclude(version => version.AnalysisJob)
            .Include(proposal => proposal.Reviews)
            .Include(proposal => proposal.Project).ThenInclude(project => project.ProjectTags)
            .Include(proposal => proposal.Team).ThenInclude(team => team.Class).ThenInclude(item => item.ClassLecturers)
            .Include(proposal => proposal.Team).ThenInclude(team => team.ProjectDirection)
            .Include(proposal => proposal.Team).ThenInclude(team => team.TeamMembers).ThenInclude(member => member.ClassStudent).ThenInclude(enrollment => enrollment.Student)
            .Include(proposal => proposal.Team).ThenInclude(team => team.MentorAssignments).ThenInclude(assignment => assignment.MentorProfile);
    }

    private Error? GetDraftMutationError(Team team, Guid userId, string role)
    {
        var teamError = GetActiveTeamError(team);
        if (teamError != null) return teamError;
        return IsActiveMember(team, userId, role)
            ? null
            : new Error(ErrorCodes.ProjectProposalAccessDenied, "Only active team members can edit the project proposal draft.");
    }

    private static Error? GetActiveTeamError(Team team)
    {
        if (team.Status != TeamStatus.Active)
            return new Error(ErrorCodes.TeamInactive, "Project proposal cannot be changed for an inactive team.");
        return ClassStateRules.GetMutationError(team.Class.Status);
    }

    private static bool CanView(Team team, Guid userId, string role)
    {
        if (IsRole(role, SystemRoles.Admin)) return true;
        if (IsRole(role, SystemRoles.Lecturer)) return IsAssignedLecturer(team, userId);
        if (IsRole(role, SystemRoles.Mentor)) return team.MentorAssignments.Any(assignment =>
            assignment.MentorProfile.UserId == userId && assignment.Status == MentorAssignmentStatus.Active && assignment.EndedAt == null);
        return IsActiveMember(team, userId, role);
    }

    private static bool IsAssignedLecturer(Team team, Guid userId) =>
        team.Class.PrimaryLecturerId == userId || team.Class.ClassLecturers.Any(assignment => assignment.LecturerId == userId);

    private static bool IsActiveMember(Team team, Guid userId, string role) =>
        IsRole(role, SystemRoles.Student) && team.TeamMembers.Any(member =>
            member.CountsTowardActiveTeam
            && member.ClassStudent.EnrollmentStatus == EnrollmentStatus.Active
            && member.ClassStudent.Student.UserId == userId);

    private static bool IsLeader(Team team, Guid userId, string role) =>
        IsRole(role, SystemRoles.Student) && team.TeamMembers.Any(member =>
            member.CountsTowardActiveTeam
            && member.RoleInTeam == TeamMemberRole.Leader
            && member.ClassStudent.EnrollmentStatus == EnrollmentStatus.Active
            && member.ClassStudent.Student.UserId == userId);

    private static string? ValidateDraftContent(ProjectProposalContentRequest request)
    {
        var values = new (string? Value, int Maximum, string Name)[]
        {
            (request.Title, 200, "Title"), (request.StartupName, 150, "Startup name"), (request.Tagline, 200, "Tagline"),
            (request.Problem, 3_000, "Problem"), (request.Solution, 3_000, "Solution"),
            (request.TargetCustomers, 2_000, "Target customers"), (request.ValueProposition, 2_000, "Value proposition"),
            (request.MarketSize, 2_500, "Market size"), (request.Competitors, 3_000, "Competitors"),
            (request.BusinessModel, 3_000, "Business model"), (request.RevenueModel, 2_000, "Revenue model"),
            (request.MarketingStrategy, 3_000, "Marketing strategy"), (request.Technology, 3_000, "Technology"),
            (request.FinancialPlan, 3_000, "Financial plan"), (request.Roadmap, 3_000, "Roadmap"),
            (request.TeamIntroduction, 2_000, "Team introduction"), (request.ChangeNote, 1_000, "Change note")
        };
        var oversized = values.FirstOrDefault(item => Trimmed(item.Value).Length > item.Maximum);
        if (oversized.Value != null)
            return $"{oversized.Name} must not exceed {oversized.Maximum} characters.";
        if (ProjectProposalValidationRules.TotalContentLength(request) > ProjectProposalValidationRules.MaximumTotalContentLength)
            return $"Proposal content must not exceed {ProjectProposalValidationRules.MaximumTotalContentLength} characters in total.";
        return null;
    }

    private static string? ValidateSubmission(ProjectProposal proposal)
    {
        if (Trimmed(proposal.Title).Length is < 5 or > 200) return "Title must be between 5 and 200 characters before submission.";
        if (Trimmed(proposal.StartupName).Length is < 2 or > 150) return "Startup name must be between 2 and 150 characters before submission.";
        if (Trimmed(proposal.Problem).Length is < 100 or > 3_000) return "Problem must be between 100 and 3000 characters before submission.";
        if (Trimmed(proposal.Solution).Length is < 100 or > 3_000) return "Solution must be between 100 and 3000 characters before submission.";
        if (Trimmed(proposal.TargetCustomers).Length is < 50 or > 2_000) return "Target customers must be between 50 and 2000 characters before submission.";
        if (Trimmed(proposal.ValueProposition).Length is < 50 or > 2_000) return "Value proposition must be between 50 and 2000 characters before submission.";
        if (Trimmed(proposal.BusinessModel).Length is < 100 or > 3_000) return "Business model must be between 100 and 3000 characters before submission.";
        if (Trimmed(proposal.Roadmap).Length is < 100 or > 3_000) return "Roadmap must be between 100 and 3000 characters before submission.";
        var startupIndustryCount = proposal.Project.ProjectTags.Count(tag => tag.TagType == ProjectTagType.StartupField);
        return startupIndustryCount is < 1 or > 3
            ? "The project must have between 1 and 3 startup industries before proposal submission."
            : null;
    }

    private static void ApplyContent(ProjectProposal proposal, ProjectProposalContentRequest request)
    {
        proposal.Title = Trimmed(request.Title);
        proposal.StartupName = NullIfEmpty(request.StartupName);
        proposal.Tagline = NullIfEmpty(request.Tagline);
        proposal.Problem = NullIfEmpty(request.Problem);
        proposal.Solution = NullIfEmpty(request.Solution);
        proposal.TargetCustomers = NullIfEmpty(request.TargetCustomers);
        proposal.ValueProposition = NullIfEmpty(request.ValueProposition);
        proposal.MarketSize = NullIfEmpty(request.MarketSize);
        proposal.Competitors = NullIfEmpty(request.Competitors);
        proposal.BusinessModel = NullIfEmpty(request.BusinessModel);
        proposal.RevenueModel = NullIfEmpty(request.RevenueModel);
        proposal.MarketingStrategy = NullIfEmpty(request.MarketingStrategy);
        proposal.Technology = NullIfEmpty(request.Technology);
        proposal.FinancialPlan = NullIfEmpty(request.FinancialPlan);
        proposal.Roadmap = NullIfEmpty(request.Roadmap);
        proposal.TeamIntroduction = NullIfEmpty(request.TeamIntroduction);
    }

    private static void ApplySnapshot(ProjectProposal proposal, ProjectProposalSnapshotDto snapshot)
    {
        proposal.Title = Trimmed(snapshot.Title);
        proposal.StartupName = NullIfEmpty(snapshot.StartupName);
        proposal.Tagline = NullIfEmpty(snapshot.Tagline);
        proposal.Problem = NullIfEmpty(snapshot.Problem);
        proposal.Solution = NullIfEmpty(snapshot.Solution);
        proposal.TargetCustomers = NullIfEmpty(snapshot.TargetCustomers);
        proposal.ValueProposition = NullIfEmpty(snapshot.ValueProposition);
        proposal.MarketSize = NullIfEmpty(snapshot.MarketSize);
        proposal.Competitors = NullIfEmpty(snapshot.Competitors);
        proposal.BusinessModel = NullIfEmpty(snapshot.BusinessModel);
        proposal.RevenueModel = NullIfEmpty(snapshot.RevenueModel);
        proposal.MarketingStrategy = NullIfEmpty(snapshot.MarketingStrategy);
        proposal.Technology = NullIfEmpty(snapshot.Technology);
        proposal.FinancialPlan = NullIfEmpty(snapshot.FinancialPlan);
        proposal.Roadmap = NullIfEmpty(snapshot.Roadmap);
        proposal.TeamIntroduction = NullIfEmpty(snapshot.TeamIntroduction);
    }

    private static bool HasSameContent(ProjectProposal proposal, ProjectProposalContentRequest request) =>
        HasSameContent(proposal, ToSnapshot(request));

    private static bool HasSameContent(ProjectProposal proposal, ProjectProposalSnapshotDto snapshot) =>
        string.Equals(Trimmed(proposal.Title), Trimmed(snapshot.Title), StringComparison.Ordinal)
        && string.Equals(Trimmed(proposal.StartupName), Trimmed(snapshot.StartupName), StringComparison.Ordinal)
        && string.Equals(Trimmed(proposal.Tagline), Trimmed(snapshot.Tagline), StringComparison.Ordinal)
        && string.Equals(Trimmed(proposal.Problem), Trimmed(snapshot.Problem), StringComparison.Ordinal)
        && string.Equals(Trimmed(proposal.Solution), Trimmed(snapshot.Solution), StringComparison.Ordinal)
        && string.Equals(Trimmed(proposal.TargetCustomers), Trimmed(snapshot.TargetCustomers), StringComparison.Ordinal)
        && string.Equals(Trimmed(proposal.ValueProposition), Trimmed(snapshot.ValueProposition), StringComparison.Ordinal)
        && string.Equals(Trimmed(proposal.MarketSize), Trimmed(snapshot.MarketSize), StringComparison.Ordinal)
        && string.Equals(Trimmed(proposal.Competitors), Trimmed(snapshot.Competitors), StringComparison.Ordinal)
        && string.Equals(Trimmed(proposal.BusinessModel), Trimmed(snapshot.BusinessModel), StringComparison.Ordinal)
        && string.Equals(Trimmed(proposal.RevenueModel), Trimmed(snapshot.RevenueModel), StringComparison.Ordinal)
        && string.Equals(Trimmed(proposal.MarketingStrategy), Trimmed(snapshot.MarketingStrategy), StringComparison.Ordinal)
        && string.Equals(Trimmed(proposal.Technology), Trimmed(snapshot.Technology), StringComparison.Ordinal)
        && string.Equals(Trimmed(proposal.FinancialPlan), Trimmed(snapshot.FinancialPlan), StringComparison.Ordinal)
        && string.Equals(Trimmed(proposal.Roadmap), Trimmed(snapshot.Roadmap), StringComparison.Ordinal)
        && string.Equals(Trimmed(proposal.TeamIntroduction), Trimmed(snapshot.TeamIntroduction), StringComparison.Ordinal);

    private static ProjectProposalVersion CreateVersion(
        ProjectProposal proposal,
        int versionNumber,
        ProjectProposalVersionPurpose purpose,
        string? changeNote,
        Guid userId,
        DateTime now) => new()
    {
        ProjectProposalId = proposal.Id,
        ProjectProposal = proposal,
        VersionNumber = versionNumber,
        SnapshotJson = JsonSerializer.Serialize(ToSnapshot(proposal), JsonOptions),
        SnapshotSchemaVersion = SnapshotSchemaVersion,
        Purpose = purpose,
        ChangeNote = NullIfEmpty(changeNote),
        ChangedById = userId,
        CreatedAt = now
    };

    private static int NextVersionNumber(ProjectProposal proposal) =>
        proposal.Versions.Count == 0 ? 1 : proposal.Versions.Max(version => version.VersionNumber) + 1;

    private static ProjectProposalVersion? LatestSubmittedVersion(ProjectProposal proposal) =>
        proposal.Versions
            .Where(version => version.Purpose == ProjectProposalVersionPurpose.Submission)
            .OrderByDescending(version => version.VersionNumber)
            .FirstOrDefault();

    private void AddActivity(Project project, Guid userId, string action, string summary, DateTime now)
    {
        _context.ProjectActivityLogs.Add(new ProjectActivityLog
        {
            ProjectId = project.Id,
            Project = project,
            ActorUserId = userId,
            Action = action,
            Summary = summary,
            ChangedFieldsJson = "[]",
            OccurredAtUtc = now
        });
    }

    private static ProjectProposalSnapshotDto ToSnapshot(ProjectProposalContentRequest request) => new()
    {
        Title = Trimmed(request.Title), StartupName = Trimmed(request.StartupName), Tagline = Trimmed(request.Tagline),
        Problem = Trimmed(request.Problem), Solution = Trimmed(request.Solution), TargetCustomers = Trimmed(request.TargetCustomers),
        ValueProposition = Trimmed(request.ValueProposition), MarketSize = Trimmed(request.MarketSize), Competitors = Trimmed(request.Competitors),
        BusinessModel = Trimmed(request.BusinessModel), RevenueModel = Trimmed(request.RevenueModel), MarketingStrategy = Trimmed(request.MarketingStrategy),
        Technology = Trimmed(request.Technology), FinancialPlan = Trimmed(request.FinancialPlan), Roadmap = Trimmed(request.Roadmap),
        TeamIntroduction = Trimmed(request.TeamIntroduction)
    };

    private static ProjectProposalSnapshotDto ToSnapshot(ProjectProposal proposal) => new()
    {
        Title = Trimmed(proposal.Title), StartupName = Trimmed(proposal.StartupName), Tagline = Trimmed(proposal.Tagline),
        Problem = Trimmed(proposal.Problem), Solution = Trimmed(proposal.Solution), TargetCustomers = Trimmed(proposal.TargetCustomers),
        ValueProposition = Trimmed(proposal.ValueProposition), MarketSize = Trimmed(proposal.MarketSize), Competitors = Trimmed(proposal.Competitors),
        BusinessModel = Trimmed(proposal.BusinessModel), RevenueModel = Trimmed(proposal.RevenueModel), MarketingStrategy = Trimmed(proposal.MarketingStrategy),
        Technology = Trimmed(proposal.Technology), FinancialPlan = Trimmed(proposal.FinancialPlan), Roadmap = Trimmed(proposal.Roadmap),
        TeamIntroduction = Trimmed(proposal.TeamIntroduction)
    };

    private static ProjectProposalSnapshotDto? DeserializeSnapshot(ProjectProposalVersion version)
    {
        if (!string.Equals(version.SnapshotSchemaVersion, SnapshotSchemaVersion, StringComparison.Ordinal)) return null;
        try { return JsonSerializer.Deserialize<ProjectProposalSnapshotDto>(version.SnapshotJson, JsonOptions); }
        catch (JsonException) { return null; }
    }

    private static ProjectProposalDto ToDto(ProjectProposal proposal)
    {
        var submittedVersion = LatestSubmittedVersion(proposal);
        return new ProjectProposalDto
        {
            Id = proposal.Id, ProjectId = proposal.ProjectId, TeamId = proposal.TeamId, ClassId = proposal.ClassId,
            Title = Trimmed(proposal.Title), StartupName = Trimmed(proposal.StartupName), Tagline = Trimmed(proposal.Tagline),
            Problem = Trimmed(proposal.Problem), Solution = Trimmed(proposal.Solution), TargetCustomers = Trimmed(proposal.TargetCustomers),
            ValueProposition = Trimmed(proposal.ValueProposition), MarketSize = Trimmed(proposal.MarketSize), Competitors = Trimmed(proposal.Competitors),
            BusinessModel = Trimmed(proposal.BusinessModel), RevenueModel = Trimmed(proposal.RevenueModel), MarketingStrategy = Trimmed(proposal.MarketingStrategy),
            Technology = Trimmed(proposal.Technology), FinancialPlan = Trimmed(proposal.FinancialPlan), Roadmap = Trimmed(proposal.Roadmap),
            TeamIntroduction = Trimmed(proposal.TeamIntroduction), Status = proposal.Status.ToString(),
            CurrentSubmittedVersionId = submittedVersion?.Id,
            CurrentAnalysisJobId = submittedVersion?.AnalysisJob?.Id,
            CurrentAnalysisStatus = submittedVersion?.AnalysisJob?.Status.ToString(),
            SubmittedAtUtc = proposal.SubmittedAt,
            ApprovedAtUtc = proposal.ApprovedAt, RejectedAtUtc = proposal.RejectedAt, RowVersion = proposal.Version.ToString(),
            Reviews = proposal.Reviews.OrderByDescending(review => review.OccurredAtUtc).Select(review => new ProjectProposalReviewDto
            {
                Id = review.Id, ProposalVersionId = review.ProposalVersionId, FromStatus = review.FromStatus.ToString(),
                ToStatus = review.ToStatus.ToString(), Feedback = review.Feedback,
                ReviewedByUserId = review.ReviewedByUserId, OccurredAtUtc = review.OccurredAtUtc
            }).ToArray()
        };
    }

    private static ProjectProposalVersionSummaryDto ToVersionSummaryDto(ProjectProposalVersion version) => new()
    {
        Id = version.Id, VersionNumber = version.VersionNumber, Purpose = version.Purpose.ToString(),
        SnapshotSchemaVersion = version.SnapshotSchemaVersion, ChangeNote = version.ChangeNote ?? string.Empty,
        ChangedByUserId = version.ChangedById, CreatedAtUtc = version.CreatedAt
    };

    private static Result<ProjectProposalVersionDto> TryMapVersion(ProjectProposalVersion version)
    {
        var snapshot = DeserializeSnapshot(version);
        return snapshot == null
            ? FailureVersion(ErrorCodes.ProjectProposalStateInvalid, "The selected proposal version uses an unsupported or invalid snapshot.")
            : Result.Success(new ProjectProposalVersionDto
            {
                Id = version.Id, ProjectProposalId = version.ProjectProposalId, VersionNumber = version.VersionNumber,
                Purpose = version.Purpose.ToString(), SnapshotSchemaVersion = version.SnapshotSchemaVersion,
                ChangeNote = version.ChangeNote ?? string.Empty, ChangedByUserId = version.ChangedById,
                CreatedAtUtc = version.CreatedAt, Snapshot = snapshot
            });
    }

    private static string Trimmed(string? value) => value?.Trim() ?? string.Empty;
    private static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static bool IsRole(string role, string expected) => string.Equals(role, expected, StringComparison.OrdinalIgnoreCase);

    private static Result<ProjectProposalDto> Failure(string message) =>
        Failure(ErrorCodes.ProjectProposalValidationError, message);
    private static Result<ProjectProposalDto> Failure(Error error) => Result.Failure<ProjectProposalDto>(error);
    private static Result<ProjectProposalDto> Failure(string code, string message) => Result.Failure<ProjectProposalDto>(new Error(code, message));
    private static Result<IReadOnlyCollection<ProjectProposalVersionSummaryDto>> FailureVersions(string code, string message) =>
        Result.Failure<IReadOnlyCollection<ProjectProposalVersionSummaryDto>>(new Error(code, message));
    private static Result<ProjectProposalVersionDto> FailureVersion(string code, string message) =>
        Result.Failure<ProjectProposalVersionDto>(new Error(code, message));
}
