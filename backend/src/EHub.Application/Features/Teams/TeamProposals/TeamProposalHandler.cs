using System.Text.Json;
using EHub.Application.Common.Interfaces.Persistence;
using EHub.Application.Common.Exceptions;
using EHub.Application.Features.Classes.Common;
using EHub.Contracts.Teams;
using EHub.Domain.Entities;
using EHub.Domain.Enums;
using EHub.Shared.Constants;
using EHub.Shared.Errors;
using EHub.Shared.Results;
using Microsoft.EntityFrameworkCore;

namespace EHub.Application.Features.Teams.TeamProposals;

public sealed class TeamProposalHandler : ITeamProposalHandler
{
    private static readonly HashSet<string> GroupOneMajorCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        "BBA_HM",
        "BBA_IB",
        "BBA_MC",
        "BBA_MKT",
        "BEN",
        "BBA_TM"
    };

    private static readonly HashSet<string> GroupTwoMajorCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        "BIT_AI",
        "BIT_GD",
        "BIT_IA",
        "BIT_SE"
    };

    private readonly IApplicationDbContext _context;
    private readonly IUnitOfWork _unitOfWork;

    public TeamProposalHandler(IApplicationDbContext context, IUnitOfWork unitOfWork)
    {
        _context = context;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<IReadOnlyCollection<TeamProposalDto>>> GetForClassAsync(
        Guid classId, Guid userId, string role, CancellationToken cancellationToken = default)
    {
        var targetClass = await _context.Classes.AsNoTracking().FirstOrDefaultAsync(item => item.Id == classId, cancellationToken);
        if (targetClass == null) return FailureList(ErrorCodes.ClassNotFound, "The requested class was not found.");
        var query = ProposalQuery().Where(item => item.ClassId == classId);
        if (IsRole(role, SystemRoles.Lecturer))
        {
            if (targetClass.PrimaryLecturerId != userId) return FailureList(ErrorCodes.ClassAccessDenied, "You can only review proposals in classes assigned to you.");
        }
        else if (IsRole(role, SystemRoles.Student))
        {
            var studentId = await GetStudentIdAsync(userId, cancellationToken);
            var expectedStatus = ClassStateRules.IsReadOnly(targetClass.Status)
                ? EnrollmentStatus.Completed
                : EnrollmentStatus.Active;
            if (!studentId.HasValue || !await _context.ClassStudents.AsNoTracking().AnyAsync(item =>
                    item.ClassId == classId && item.StudentId == studentId && item.EnrollmentStatus == expectedStatus, cancellationToken))
                return FailureList(ErrorCodes.ClassAccessDenied, "You do not have access to this class proposal history.");
            query = query.Where(item => item.Members.Any(member => member.StudentId == studentId && member.IsIncluded));
        }
        else if (!IsRole(role, SystemRoles.Admin)) return FailureList(ErrorCodes.ClassAccessDenied, "You cannot view these proposals.");
        var proposals = await query.OrderByDescending(item => item.CreatedAt).ToListAsync(cancellationToken);
        return Result.Success<IReadOnlyCollection<TeamProposalDto>>(proposals.Select(ToDto).ToArray());
    }

    public async Task<Result<TeamProposalDto>> CreateAsync(
        Guid classId, CreateTeamProposalRequest request, Guid userId, string role, CancellationToken cancellationToken = default)
    {
        if (!IsRole(role, SystemRoles.Student)) return Failure(ErrorCodes.ClassAccessDenied, "Only a student can create a team proposal.");
        var studentId = await GetStudentIdAsync(userId, cancellationToken);
        if (!studentId.HasValue) return Failure(ErrorCodes.ClassAccessDenied, "The current account is not linked to a student profile.");
        if (!request.MemberIds.Contains(studentId.Value)) return Failure(ErrorCodes.ClassAccessDenied, "The proposing student must be included in the proposal.");

        var composition = await LoadAndValidateCompositionAsync(
            classId,
            request.MemberIds,
            request.LeaderStudentId,
            null,
            cancellationToken);
        if (composition.IsFailure) return Failure(composition.Error.Code, composition.Error.Message);
        var nameError = ValidateStudentSubmissionText(
            request.TeamName,
            request.ProjectName ?? string.Empty,
            request.Description ?? string.Empty);
        if (nameError != null) return Failure(nameError.Value.Code, nameError.Value.Message);
        var now = DateTime.UtcNow;
        var proposal = new TeamProposal
        {
            ClassId = classId,
            ProposedByStudentId = studentId.Value,
            TeamName = request.TeamName.Trim(),
            Description = request.Description?.Trim(),
            ProjectName = request.ProjectName?.Trim(),
            Status = TeamProposalStatus.Draft,
            CreatedBy = userId
        };
        foreach (var enrollment in composition.Value)
        {
            proposal.Members.Add(new TeamProposalMember
            {
                ProposalId = proposal.Id,
                Proposal = proposal,
                ClassId = classId,
                StudentId = enrollment.StudentId,
                ClassStudent = enrollment,
                IsLeader = enrollment.StudentId == request.LeaderStudentId,
                IsIncluded = true,
                CountsTowardOpenProposal = true
            });
        }
        _context.TeamProposals.Add(proposal);
        AddHistory(proposal, null, TeamProposalStatus.Draft, "Created", null, userId, now);
        ClassOutbox.Enqueue(_context, "TeamProposal.Created.v1", classId, new { ProposalId = proposal.Id }, now);
        try { await _context.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException) { return Failure(ErrorCodes.TeamProposalMembershipConflict, "A selected student already belongs to another open proposal."); }
        return Result.Success(ToDto(proposal));
    }

    public async Task<Result<TeamProposalDto>> SubmitStudentProposalAsync(
        Guid classId,
        SubmitStudentTeamProposalRequest request,
        Guid userId,
        string role,
        CancellationToken cancellationToken = default)
    {
        if (!IsRole(role, SystemRoles.Student))
        {
            return Failure(ErrorCodes.ClassAccessDenied, "Only a student can submit a team proposal.");
        }

        var studentId = await GetStudentIdAsync(userId, cancellationToken);
        if (!studentId.HasValue)
        {
            return Failure(ErrorCodes.ClassAccessDenied, "The current account is not linked to a student profile.");
        }

        if (!request.StudentIds.Contains(studentId.Value))
        {
            return Failure(
                ErrorCodes.ClassAccessDenied,
                "The proposing student must be included in the proposal.");
        }

        var projectName = request.IsProjectNameSameAsGroup
            ? request.GroupName.Trim()
            : request.ProjectName.Trim();
        var textError = ValidateStudentSubmissionText(
            request.GroupName,
            projectName,
            request.Description);
        if (textError != null)
        {
            return Failure(textError.Value.Code, textError.Value.Message);
        }

        var memberCount = request.StudentIds.Distinct().Count();
        if (memberCount is < 4 or > 6)
        {
            return Failure(
                ErrorCodes.TeamProposalInvalid,
                "A proposal must contain 4 to 6 members.");
        }

        try
        {
            return await _unitOfWork.ExecuteInSerializableTransactionAsync(async transactionCancellationToken =>
            {
                var targetClass = await _context.Classes
                    .FirstOrDefaultAsync(item => item.Id == classId, transactionCancellationToken);
                if (targetClass == null)
                {
                    return Failure(ErrorCodes.ClassNotFound, "The requested class was not found.");
                }

                var composition = await LoadAndValidateCompositionAsync(
                    classId,
                    request.StudentIds,
                    request.LeaderStudentId,
                    null,
                    transactionCancellationToken);
                if (composition.IsFailure)
                {
                    return Failure(composition.Error.Code, composition.Error.Message);
                }

                var groupName = request.GroupName.Trim();
                var hasExistingProposalName = await _context.TeamProposals.AnyAsync(item =>
                    item.ClassId == classId &&
                    item.TeamName.ToLower() == groupName.ToLower() &&
                    item.Status != TeamProposalStatus.Rejected &&
                    item.Status != TeamProposalStatus.Cancelled,
                    transactionCancellationToken);
                if (hasExistingProposalName)
                {
                    return Failure(
                        ErrorCodes.TeamNameDuplicated,
                        "A non-rejected proposal with this group name already exists in the class.");
                }

                var hasExistingTeamName = await _context.Teams.AnyAsync(item =>
                    item.ClassId == classId && item.TeamName.ToLower() == groupName.ToLower(),
                    transactionCancellationToken);
                if (hasExistingTeamName)
                {
                    return Failure(
                        ErrorCodes.TeamNameDuplicated,
                        "A team with this group name already exists in the class.");
                }

                var now = DateTime.UtcNow;
                var proposal = new TeamProposal
                {
                    ClassId = classId,
                    ProposedByStudentId = studentId.Value,
                    TeamName = groupName,
                    ProjectName = projectName,
                    Description = request.Description.Trim(),
                    Status = TeamProposalStatus.Pending,
                    SubmittedAtUtc = now,
                    CreatedBy = userId
                };
                foreach (var enrollment in composition.Value)
                {
                    proposal.Members.Add(new TeamProposalMember
                    {
                        ProposalId = proposal.Id,
                        Proposal = proposal,
                        ClassId = classId,
                        StudentId = enrollment.StudentId,
                        ClassStudent = enrollment,
                        IsLeader = enrollment.StudentId == request.LeaderStudentId,
                        IsIncluded = true,
                        CountsTowardOpenProposal = true
                    });
                }

                AddHistory(
                    proposal,
                    null,
                    TeamProposalStatus.Pending,
                    "Submitted",
                    null,
                    userId,
                    now);
                _context.TeamProposals.Add(proposal);
                ClassOutbox.Enqueue(_context, "TeamProposal.Submitted.v1", classId, new
                {
                    ProposalId = proposal.Id,
                    LecturerUserId = targetClass.PrimaryLecturerId,
                    AdminReviewRequired = false,
                    StudentUserIds = GetMemberUserIds(composition.Value)
                }, now);
                await _context.SaveChangesAsync(transactionCancellationToken);
                return Result.Success(ToDto(proposal));
            }, cancellationToken);
        }
        catch (DbUpdateException)
        {
            return Failure(
                ErrorCodes.TeamProposalMembershipConflict,
                "A selected student is already reserved by another team proposal or team.");
        }
        catch (SerializableTransactionConflictException)
        {
            return Failure(
                ErrorCodes.ClassConcurrencyConflict,
                "The proposal conflicted with another request. Refresh and try again.");
        }
    }

    public async Task<Result<TeamProposalDto>> UpdateAsync(
        Guid proposalId, UpdateTeamProposalRequest request, Guid userId, string role, CancellationToken cancellationToken = default)
    {
        if (!IsRole(role, SystemRoles.Student)) return Failure(ErrorCodes.ClassAccessDenied, "Only a student can update a team proposal.");
        if (!uint.TryParse(request.RowVersion, out var version)) return Failure(ErrorCodes.ClassValidationError, "A valid rowVersion is required.");
        try
        {
            return await _unitOfWork.ExecuteInSerializableTransactionAsync(async transactionCancellationToken =>
            {
                var studentId = await GetStudentIdAsync(userId, transactionCancellationToken);
                var proposal = await ProposalQuery(tracking: true).FirstOrDefaultAsync(item => item.Id == proposalId, transactionCancellationToken);
                if (proposal == null) return Failure(ErrorCodes.TeamProposalNotFound, "The requested proposal was not found.");
                if (!studentId.HasValue || proposal.ProposedByStudentId != studentId) return Failure(ErrorCodes.ClassAccessDenied, "Only the proposing student can update this proposal.");
                if (proposal.Status is not (TeamProposalStatus.Draft or TeamProposalStatus.NeedsRevision)) return Failure(ErrorCodes.TeamProposalStateInvalid, "Only Draft or NeedsRevision proposals can be updated.");
                if (proposal.Version != version) return Failure(ErrorCodes.ClassConcurrencyConflict, "The proposal changed concurrently. Refresh and try again.");
                if (!request.MemberIds.Contains(studentId.Value)) return Failure(ErrorCodes.ClassAccessDenied, "The proposing student must remain included in the proposal.");
                var updateMemberCount = request.MemberIds.Distinct().Count();
                if (updateMemberCount is < 4 or > 6)
                {
                    return Failure(
                        ErrorCodes.TeamProposalInvalid,
                        "A proposal must contain 4 to 6 members.");
                }

                var composition = await LoadAndValidateCompositionAsync(
                    proposal.ClassId,
                    request.MemberIds,
                    request.LeaderStudentId,
                    proposal.Id,
                    transactionCancellationToken);
                if (composition.IsFailure) return Failure(composition.Error.Code, composition.Error.Message);
                var textError = ValidateStudentSubmissionText(
                    request.TeamName,
                    request.ProjectName ?? string.Empty,
                    request.Description ?? string.Empty);
                if (textError != null) return Failure(textError.Value.Code, textError.Value.Message);

                var normalizedTeamName = request.TeamName.Trim().ToLower();
                var hasDuplicateProposalName = await _context.TeamProposals.AnyAsync(item =>
                    item.ClassId == proposal.ClassId &&
                    item.Id != proposal.Id &&
                    item.TeamName.ToLower() == normalizedTeamName &&
                    item.Status != TeamProposalStatus.Rejected &&
                    item.Status != TeamProposalStatus.Cancelled,
                    transactionCancellationToken);
                if (hasDuplicateProposalName)
                {
                    return Failure(
                        ErrorCodes.TeamNameDuplicated,
                        "A non-rejected proposal with this group name already exists in the class.");
                }

                var hasDuplicateTeamName = await _context.Teams.AnyAsync(item =>
                    item.ClassId == proposal.ClassId &&
                    item.TeamName.ToLower() == normalizedTeamName,
                    transactionCancellationToken);
                if (hasDuplicateTeamName)
                {
                    return Failure(
                        ErrorCodes.TeamNameDuplicated,
                        "A team with this group name already exists in the class.");
                }

                var previousLeader = proposal.Members.FirstOrDefault(member =>
                    member.IsLeader && member.StudentId != request.LeaderStudentId);
                if (previousLeader != null)
                {
                    // Keep the partial unique leader index valid while changing leaders.
                    previousLeader.IsLeader = false;
                    await _context.SaveChangesAsync(transactionCancellationToken);
                }

                proposal.TeamName = request.TeamName.Trim();
                proposal.Description = request.Description?.Trim();
                proposal.ProjectName = request.ProjectName?.Trim();
                proposal.UpdatedBy = userId;
                proposal.UpdatedAt = DateTime.UtcNow;
                var desired = composition.Value.ToDictionary(item => item.StudentId);
                foreach (var existing in proposal.Members)
                {
                    existing.IsIncluded = desired.ContainsKey(existing.StudentId);
                    existing.CountsTowardOpenProposal = existing.IsIncluded;
                    existing.IsLeader = existing.IsIncluded && existing.StudentId == request.LeaderStudentId;
                }
                foreach (var enrollment in composition.Value.Where(item => proposal.Members.All(member => member.StudentId != item.StudentId)))
                {
                    proposal.Members.Add(new TeamProposalMember
                    {
                        ProposalId = proposal.Id, Proposal = proposal, ClassId = proposal.ClassId, StudentId = enrollment.StudentId,
                        ClassStudent = enrollment, IsLeader = enrollment.StudentId == request.LeaderStudentId, IsIncluded = true,
                        CountsTowardOpenProposal = true
                    });
                }
                AddHistory(proposal, proposal.Status, proposal.Status, "Updated", null, userId, DateTime.UtcNow);
                await _context.SaveChangesAsync(transactionCancellationToken);
                return Result.Success(ToDto(proposal));
            }, cancellationToken);
        }
        catch (DbUpdateConcurrencyException) { return Failure(ErrorCodes.ClassConcurrencyConflict, "The proposal changed concurrently. Refresh and try again."); }
        catch (SerializableTransactionConflictException) { return Failure(ErrorCodes.ClassConcurrencyConflict, "The proposal changed concurrently. Refresh and try again."); }
        catch (DbUpdateException) { return Failure(ErrorCodes.TeamProposalMembershipConflict, "A selected student belongs to another open proposal."); }
    }

    public async Task<Result<TeamProposalDto>> SubmitAsync(
        Guid proposalId, SubmitTeamProposalRequest request, Guid userId, string role, CancellationToken cancellationToken = default)
    {
        if (!IsRole(role, SystemRoles.Student)) return Failure(ErrorCodes.ClassAccessDenied, "Only a student can submit a team proposal.");
        if (!uint.TryParse(request.RowVersion, out var version)) return Failure(ErrorCodes.ClassValidationError, "A valid rowVersion is required.");
        var studentId = await GetStudentIdAsync(userId, cancellationToken);
        var proposal = await ProposalQuery(tracking: true).FirstOrDefaultAsync(item => item.Id == proposalId, cancellationToken);
        if (proposal == null) return Failure(ErrorCodes.TeamProposalNotFound, "The requested proposal was not found.");
        if (!studentId.HasValue || proposal.ProposedByStudentId != studentId) return Failure(ErrorCodes.ClassAccessDenied, "Only the proposing student can submit this proposal.");
        if (proposal.Status is not (TeamProposalStatus.Draft or TeamProposalStatus.NeedsRevision)) return Failure(ErrorCodes.TeamProposalStateInvalid, "Only Draft or NeedsRevision proposals can be submitted.");
        if (proposal.Version != version) return Failure(ErrorCodes.ClassConcurrencyConflict, "The proposal changed concurrently. Refresh and try again.");
        var members = proposal.Members.Where(member => member.IsIncluded).ToArray();
        if (members.Length is < 4 or > 6)
        {
            return Failure(
                ErrorCodes.TeamProposalInvalid,
                "A proposal must contain 4 to 6 members.");
        }

        var composition = await LoadAndValidateCompositionAsync(
            proposal.ClassId,
            members.Select(item => item.StudentId).ToArray(),
            members.SingleOrDefault(item => item.IsLeader)?.StudentId ?? Guid.Empty,
            proposal.Id,
            cancellationToken);
        if (composition.IsFailure) return Failure(composition.Error.Code, composition.Error.Message);
        var textError = ValidateStudentSubmissionText(
            proposal.TeamName,
            proposal.ProjectName ?? string.Empty,
            proposal.Description ?? string.Empty);
        if (textError != null)
        {
            return Failure(textError.Value.Code, textError.Value.Message);
        }

        var previous = proposal.Status;
        proposal.Status = TeamProposalStatus.Pending;
        proposal.SubmittedAtUtc = DateTime.UtcNow;
        proposal.UpdatedBy = userId;
        AddHistory(proposal, previous, proposal.Status, previous == TeamProposalStatus.NeedsRevision ? "Resubmitted" : "Submitted", null, userId, DateTime.UtcNow);
        ClassOutbox.Enqueue(_context, "TeamProposal.Submitted.v1", proposal.ClassId, new
        {
            ProposalId = proposal.Id,
            LecturerUserId = proposal.Class.PrimaryLecturerId,
            AdminReviewRequired = false,
            StudentUserIds = GetMemberUserIds(members)
        }, DateTime.UtcNow);
        try { await _context.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException) { return Failure(ErrorCodes.ClassConcurrencyConflict, "The proposal changed concurrently. Refresh and try again."); }
        return Result.Success(ToDto(proposal));
    }

    public async Task<Result<TeamProposalDto>> ReviewAsync(
        Guid proposalId, ReviewTeamProposalRequest request, Guid userId, string role, CancellationToken cancellationToken = default)
    {
        if (!uint.TryParse(request.RowVersion, out var version)) return Failure(ErrorCodes.ClassValidationError, "A valid rowVersion is required.");
        var decisionInput = string.IsNullOrWhiteSpace(request.Status)
            ? request.Decision
            : request.Status;
        if (!Enum.TryParse<TeamProposalStatus>(decisionInput, true, out var decision) || decision is not (TeamProposalStatus.Approved or TeamProposalStatus.NeedsRevision or TeamProposalStatus.Rejected))
            return Failure(ErrorCodes.ClassValidationError, "Decision must be Approved, NeedsRevision, or Rejected.");
        var comment = request.Comment?.Trim();
        if (decision != TeamProposalStatus.Approved && (string.IsNullOrWhiteSpace(comment) || comment.Length is < 3 or > 1_000))
            return Failure(ErrorCodes.ClassValidationError, "A review comment between 3 and 1000 characters is required.");
        if (!string.IsNullOrWhiteSpace(comment) && (comment.Length is < 3 or > 1_000))
            return Failure(ErrorCodes.ClassValidationError, "Review comment must be between 3 and 1000 characters.");

        try
        {
            return await _unitOfWork.ExecuteInSerializableTransactionAsync(async transactionCancellationToken =>
            {
                var proposal = await ProposalQuery(tracking: true).FirstOrDefaultAsync(item => item.Id == proposalId, transactionCancellationToken);
                if (proposal == null) return Failure(ErrorCodes.TeamProposalNotFound, "The requested proposal was not found.");
                if (proposal.Status != TeamProposalStatus.Pending) return Failure(ErrorCodes.TeamProposalStateInvalid, "Only a Pending proposal can be reviewed.");
                if (proposal.Version != version) return Failure(ErrorCodes.ClassConcurrencyConflict, "The proposal changed concurrently. Refresh and try again.");
                var mutationError = ClassStateRules.GetMutationError(proposal.Class.Status);
                if (mutationError != null) return Failure(mutationError.Code, mutationError.Message);

                var included = proposal.Members.Where(member => member.IsIncluded).ToArray();
                if (included.Length is < 4 or > 6)
                {
                    return Failure(
                        ErrorCodes.TeamProposalInvalid,
                        "A proposal must contain 4 to 6 members.");
                }

                var isAdmin = IsRole(role, SystemRoles.Admin);
                var isAssignedLecturer = IsRole(role, SystemRoles.Lecturer) && proposal.Class.PrimaryLecturerId == userId;
                if (!isAdmin && !isAssignedLecturer)
                {
                    return Failure(ErrorCodes.ClassAccessDenied, "Only an administrator or assigned lecturer can review this proposal.");
                }

                var now = DateTime.UtcNow;
                var previous = proposal.Status;
                if (decision == TeamProposalStatus.Approved)
                {
                    var leaderId = included.SingleOrDefault(member => member.IsLeader)?.StudentId ?? Guid.Empty;
                    var composition = await LoadAndValidateCompositionAsync(
                        proposal.ClassId,
                        included.Select(member => member.StudentId).ToArray(),
                        leaderId,
                        proposal.Id,
                        transactionCancellationToken);
                    if (composition.IsFailure) return Failure(composition.Error.Code, composition.Error.Message);
                    var teamCode = await CreateNextTeamCodeAsync(proposal.Class, transactionCancellationToken);
                    var team = new Team
                    {
                        ClassId = proposal.ClassId,
                        TeamCode = teamCode,
                        TeamName = proposal.TeamName,
                        Description = proposal.Description,
                        Status = TeamStatus.Active,
                        CreatedById = userId,
                        CreatedBy = userId
                    };
                    foreach (var enrollment in composition.Value)
                    {
                        team.TeamMembers.Add(new TeamMember
                        {
                            TeamId = team.Id, Team = team, ClassId = proposal.ClassId, StudentId = enrollment.StudentId,
                            ClassStudent = enrollment, RoleInTeam = enrollment.StudentId == leaderId ? TeamMemberRole.Leader : TeamMemberRole.Member,
                            CountsTowardActiveTeam = true, JoinedAt = now, CreatedById = userId
                        });
                    }
                    _context.Teams.Add(team);
                    _context.Projects.Add(new Project
                    {
                        TeamId = team.Id,
                        Team = team,
                        Name = proposal.ProjectName ?? proposal.TeamName,
                        Description = proposal.Description,
                        Status = ProjectStatus.Draft,
                        CreatedById = userId,
                        CreatedBy = userId
                    });
                    proposal.ApprovedTeamId = team.Id;
                    proposal.ApprovedTeam = team;
                }

                proposal.Status = decision;
                proposal.ReviewedAtUtc = now;
                proposal.ReviewedByUserId = userId;
                proposal.LatestReviewComment = comment;
                proposal.UpdatedBy = userId;
                foreach (var member in proposal.Members)
                    member.CountsTowardOpenProposal = decision == TeamProposalStatus.NeedsRevision && member.IsIncluded;
                AddHistory(proposal, previous, decision, "Reviewed", comment, userId, now);
                ClassOutbox.Enqueue(_context, "TeamProposal.Reviewed.v1", proposal.ClassId, new
                {
                    ProposalId = proposal.Id,
                    Decision = decision.ToString(),
                    proposal.ApprovedTeamId,
                    StudentUserIds = GetMemberUserIds(included),
                    TeamLeaderUserId = included
                        .SingleOrDefault(member => member.IsLeader)?
                        .ClassStudent.Student.UserId
                }, now);
                await _context.SaveChangesAsync(transactionCancellationToken);
                return Result.Success(ToDto(proposal));
            }, cancellationToken);
        }
        catch (DbUpdateConcurrencyException) { return Failure(ErrorCodes.ClassConcurrencyConflict, "The proposal was reviewed concurrently. Refresh the page."); }
        catch (SerializableTransactionConflictException) { return Failure(ErrorCodes.ClassConcurrencyConflict, "The proposal was reviewed concurrently. Refresh the page."); }
        catch (DbUpdateException) { return Failure(ErrorCodes.TeamApprovalConflict, "The proposal could not be approved because a member was assigned concurrently."); }
    }

    public async Task<Result<TeamProposalDto>> CancelAsync(
        Guid proposalId, CancelTeamProposalRequest request, Guid userId, string role, CancellationToken cancellationToken = default)
    {
        if (!IsRole(role, SystemRoles.Student)) return Failure(ErrorCodes.ClassAccessDenied, "Only a student can cancel a team proposal.");
        if (!uint.TryParse(request.RowVersion, out var version)) return Failure(ErrorCodes.ClassValidationError, "A valid rowVersion is required.");
        var reason = request.Reason.Trim();
        if (reason.Length is < 3 or > 1_000) return Failure(ErrorCodes.ClassValidationError, "Cancellation reason must be between 3 and 1000 characters.");
        var studentId = await GetStudentIdAsync(userId, cancellationToken);
        var proposal = await ProposalQuery(tracking: true).FirstOrDefaultAsync(item => item.Id == proposalId, cancellationToken);
        if (proposal == null) return Failure(ErrorCodes.TeamProposalNotFound, "The requested proposal was not found.");
        if (!studentId.HasValue || proposal.ProposedByStudentId != studentId.Value)
            return Failure(ErrorCodes.ClassAccessDenied, "Only the proposing student can cancel this proposal.");
        if (proposal.Status is not (TeamProposalStatus.Draft or TeamProposalStatus.Pending or TeamProposalStatus.NeedsRevision))
            return Failure(ErrorCodes.TeamProposalStateInvalid, "This proposal can no longer be cancelled.");
        if (proposal.Version != version) return Failure(ErrorCodes.ClassConcurrencyConflict, "The proposal changed concurrently. Refresh and try again.");

        var previous = proposal.Status;
        var now = DateTime.UtcNow;
        proposal.Status = TeamProposalStatus.Cancelled;
        proposal.UpdatedAt = now;
        proposal.UpdatedBy = userId;
        proposal.LatestReviewComment = reason;
        foreach (var member in proposal.Members) member.CountsTowardOpenProposal = false;
        AddHistory(proposal, previous, TeamProposalStatus.Cancelled, "Cancelled", reason, userId, now);
        ClassOutbox.Enqueue(_context, "TeamProposal.Cancelled.v1", proposal.ClassId, new { ProposalId = proposal.Id, Reason = reason }, now);
        try { await _context.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException) { return Failure(ErrorCodes.ClassConcurrencyConflict, "The proposal changed concurrently. Refresh and try again."); }
        return Result.Success(ToDto(proposal));
    }

    public async Task<Result<IReadOnlyCollection<TeamProposalHistoryDto>>> GetHistoryAsync(
        Guid proposalId, Guid userId, string role, CancellationToken cancellationToken = default)
    {
        var proposal = await ProposalQuery().FirstOrDefaultAsync(item => item.Id == proposalId, cancellationToken);
        if (proposal == null) return Result.Failure<IReadOnlyCollection<TeamProposalHistoryDto>>(new Error(ErrorCodes.TeamProposalNotFound, "The requested proposal was not found."));
        var studentId = IsRole(role, SystemRoles.Student) ? await GetStudentIdAsync(userId, cancellationToken) : null;
        var allowed = IsRole(role, SystemRoles.Admin) ||
                      (IsRole(role, SystemRoles.Lecturer) && proposal.Class.PrimaryLecturerId == userId) ||
                      (studentId.HasValue && proposal.Members.Any(member => member.StudentId == studentId && member.IsIncluded));
        if (!allowed) return Result.Failure<IReadOnlyCollection<TeamProposalHistoryDto>>(new Error(ErrorCodes.ClassAccessDenied, "You cannot view this proposal history."));
        var history = proposal.History.OrderByDescending(item => item.OccurredAtUtc).Select(item => new TeamProposalHistoryDto
        {
            Id = item.Id, FromStatus = item.FromStatus?.ToString(), ToStatus = item.ToStatus.ToString(), Action = item.Action,
            Comment = item.Comment, PerformedByUserId = item.PerformedByUserId, OccurredAtUtc = item.OccurredAtUtc
        }).ToArray();
        return Result.Success<IReadOnlyCollection<TeamProposalHistoryDto>>(history);
    }

    private async Task<Result<List<ClassStudent>>> LoadAndValidateCompositionAsync(
        Guid classId,
        IReadOnlyCollection<Guid> idsInput,
        Guid leaderId,
        Guid? currentProposalId,
        CancellationToken cancellationToken)
    {
        var ids = idsInput.Distinct().ToArray();
        if (ids.Length != idsInput.Count)
        {
            return CompositionFailure("Student IDs must be unique.", ErrorCodes.ClassValidationError);
        }

        if (ids.Length is < 4 or > 6)
        {
            return CompositionFailure("A proposal must contain 4 to 6 unique members.");
        }
        if (!ids.Contains(leaderId)) return CompositionFailure("The proposed leader must be one of the members.");
        var targetClass = await _context.Classes.AsNoTracking().FirstOrDefaultAsync(item => item.Id == classId, cancellationToken);
        if (targetClass == null) return Result.Failure<List<ClassStudent>>(new Error(ErrorCodes.ClassNotFound, "The requested class was not found."));
        var mutationError = ClassStateRules.GetMutationError(targetClass.Status);
        if (mutationError != null) return Result.Failure<List<ClassStudent>>(mutationError);
        var enrollments = await _context.ClassStudents.Include(item => item.Student)
            .Where(item => item.ClassId == classId && ids.Contains(item.StudentId) && item.EnrollmentStatus == EnrollmentStatus.Active)
            .ToListAsync(cancellationToken);
        if (enrollments.Count != ids.Length) return CompositionFailure("Every proposed member must be actively enrolled in the class.");
        if (await _context.TeamMembers.AsNoTracking().AnyAsync(item => item.ClassId == classId && ids.Contains(item.StudentId) && item.CountsTowardActiveTeam, cancellationToken))
            return CompositionFailure("A proposed member already belongs to an active team.", ErrorCodes.TeamMembershipConflict);
        if (await _context.TeamProposalMembers.AsNoTracking().AnyAsync(item =>
                item.ClassId == classId && ids.Contains(item.StudentId) && item.CountsTowardOpenProposal &&
                (!currentProposalId.HasValue || item.ProposalId != currentProposalId.Value), cancellationToken))
            return CompositionFailure("A proposed member already belongs to another open proposal.", ErrorCodes.TeamProposalMembershipConflict);
        var registeredMajors = await RegisteredStudentMajorResolver.LoadByEmailAsync(
            _context,
            enrollments.Select(item => item.Student.Email),
            cancellationToken);
        var majors = enrollments.Select(item =>
        {
            var major = StudentEnrollmentRules.ResolveEffectiveMajorCode(
                item.MajorCodeAtEnrollment,
                item.Student.MajorCode);
            if (!MajorCodes.IsValid(major) &&
                !string.IsNullOrWhiteSpace(item.Student.Email) &&
                registeredMajors.TryGetValue(item.Student.Email, out var registeredMajor))
            {
                major = registeredMajor;
                item.MajorCodeAtEnrollment = registeredMajor;
                item.MajorVerificationStatus = EnrollmentMajorVerificationStatus.Unverified;
                item.UpdatedAt = DateTime.UtcNow;
            }

            return major;
        }).ToArray();
        if (!majors.Any(IsBusinessMajor) || !majors.Any(IsTechnologyMajor))
            return CompositionFailure("A team must include at least one GROUP_1 major and one GROUP_2 major.", ErrorCodes.TeamMajorCompositionInvalid);
        return Result.Success(enrollments);
    }

    private IQueryable<TeamProposal> ProposalQuery(bool tracking = false)
    {
        var query = tracking ? _context.TeamProposals.AsQueryable() : _context.TeamProposals.AsNoTracking();
        return query.Include(item => item.Class)
            .Include(item => item.Members).ThenInclude(member => member.ClassStudent).ThenInclude(enrollment => enrollment.Student)
            .Include(item => item.History);
    }

    private static TeamProposalDto ToDto(TeamProposal proposal) => new()
    {
        Id = proposal.Id, ClassId = proposal.ClassId, TeamName = proposal.TeamName, Description = proposal.Description,
        ProjectName = proposal.ProjectName, Status = proposal.Status.ToString(), LatestReviewComment = proposal.LatestReviewComment,
        ApprovedTeamId = proposal.ApprovedTeamId, RowVersion = proposal.Version.ToString(),
        Members = proposal.Members.Where(member => member.IsIncluded).Select(member => new TeamProposalMemberDto
        {
            StudentId = member.StudentId, RollNumber = member.ClassStudent.Student.RollNumber ?? string.Empty,
            FullName = member.ClassStudent.Student.FullName,
            MajorCode = StudentEnrollmentRules.ResolveEffectiveMajorCode(
                member.ClassStudent.MajorCodeAtEnrollment,
                member.ClassStudent.Student.MajorCode) ?? string.Empty,
            IsLeader = member.IsLeader
        }).ToArray()
    };

    private void AddHistory(TeamProposal proposal, TeamProposalStatus? from, TeamProposalStatus to, string action, string? comment, Guid userId, DateTime occurredAt)
    {
        var history = new TeamProposalHistory
        {
            ProposalId = proposal.Id, Proposal = proposal, FromStatus = from, ToStatus = to, Action = action,
            Comment = comment, PerformedByUserId = userId, OccurredAtUtc = occurredAt,
            SnapshotJson = JsonSerializer.Serialize(new { proposal.TeamName, proposal.Description, proposal.ProjectName, Members = proposal.Members.Where(item => item.IsIncluded).Select(item => new { item.StudentId, item.IsLeader }) })
        };
        _context.TeamProposalHistory.Add(history);
    }

    private async Task<Guid?> GetStudentIdAsync(Guid userId, CancellationToken cancellationToken) =>
        await _context.Students.AsNoTracking().Where(item => item.UserId == userId).Select(item => (Guid?)item.Id).FirstOrDefaultAsync(cancellationToken);
    private static (string Code, string Message)? ValidateText(string teamName, string? description, string? projectName)
    {
        if (teamName.Trim().Length is < 3 or > 100) return (ErrorCodes.ClassValidationError, "Team name must be between 3 and 100 characters.");
        if ((description?.Trim().Length ?? 0) > 1_000) return (ErrorCodes.ClassValidationError, "Description cannot exceed 1000 characters.");
        if ((projectName?.Trim().Length ?? 0) > 200) return (ErrorCodes.ClassValidationError, "Project name cannot exceed 200 characters.");
        return null;
    }

    private static (string Code, string Message)? ValidateStudentSubmissionText(
        string groupName,
        string projectName,
        string description)
    {
        if (groupName.Trim().Length is < 3 or > 60)
        {
            return (ErrorCodes.ClassValidationError, "Group name must be between 3 and 60 characters.");
        }

        if (projectName.Trim().Length is < 3 or > 60)
        {
            return (ErrorCodes.ClassValidationError, "Project name must be between 3 and 60 characters.");
        }

        if (description.Trim().Length is < 20 or > 500)
        {
            return (ErrorCodes.ClassValidationError, "Project description must be between 20 and 500 characters.");
        }

        return null;
    }

    private async Task<string> CreateNextTeamCodeAsync(Class targetClass, CancellationToken cancellationToken)
    {
        const int maximumTeamCodeLength = 50;
        const string suffixPrefix = "_TEAM_";
        var maximumClassCodeLength = maximumTeamCodeLength - suffixPrefix.Length - 10;
        var classCode = targetClass.ClassCode.Trim();
        if (classCode.Length > maximumClassCodeLength)
        {
            classCode = classCode[..maximumClassCodeLength];
        }

        var prefix = $"{classCode}{suffixPrefix}";
        var existingCodes = await _context.Teams
            .IgnoreQueryFilters()
            .Where(team => team.ClassId == targetClass.Id && team.TeamCode.StartsWith(prefix))
            .Select(team => team.TeamCode)
            .ToListAsync(cancellationToken);

        var highestSequence = 0;
        foreach (var code in existingCodes)
        {
            var suffix = code[prefix.Length..];
            if (int.TryParse(suffix, out var sequence) && sequence > highestSequence)
            {
                highestSequence = sequence;
            }
        }

        return $"{prefix}{highestSequence + 1}";
    }

    private static IReadOnlyCollection<Guid> GetMemberUserIds(IEnumerable<ClassStudent> enrollments)
    {
        return enrollments
            .Where(enrollment => enrollment.Student.UserId.HasValue)
            .Select(enrollment => enrollment.Student.UserId!.Value)
            .Distinct()
            .ToArray();
    }

    private static IReadOnlyCollection<Guid> GetMemberUserIds(IEnumerable<TeamProposalMember> members)
    {
        return members
            .Where(member => member.ClassStudent.Student.UserId.HasValue)
            .Select(member => member.ClassStudent.Student.UserId!.Value)
            .Distinct()
            .ToArray();
    }

    private static Result<List<ClassStudent>> CompositionFailure(string message, string code = ErrorCodes.TeamProposalInvalid) => Result.Failure<List<ClassStudent>>(new Error(code, message));
    private static bool IsBusinessMajor(string? code) =>
        !string.IsNullOrWhiteSpace(code) && GroupOneMajorCodes.Contains(code.Trim());

    private static bool IsTechnologyMajor(string? code) =>
        !string.IsNullOrWhiteSpace(code) && GroupTwoMajorCodes.Contains(code.Trim());

    private static bool IsRole(string role, string expected) => string.Equals(role, expected, StringComparison.OrdinalIgnoreCase);
    private static Result<TeamProposalDto> Failure(string code, string message) => Result.Failure<TeamProposalDto>(new Error(code, message));
    private static Result<IReadOnlyCollection<TeamProposalDto>> FailureList(string code, string message) => Result.Failure<IReadOnlyCollection<TeamProposalDto>>(new Error(code, message));

}
