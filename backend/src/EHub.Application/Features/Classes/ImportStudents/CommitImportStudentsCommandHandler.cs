using System.Text.Json;
using EHub.Application.Common.Exceptions;
using EHub.Application.Common.Interfaces.Persistence;
using EHub.Application.Features.Classes.Common;
using EHub.Contracts.Classes;
using EHub.Domain.Entities;
using EHub.Domain.Enums;
using EHub.Shared.Constants;
using EHub.Shared.Errors;
using EHub.Shared.Results;
using Microsoft.EntityFrameworkCore;

namespace EHub.Application.Features.Classes.ImportStudents;

public sealed class CommitImportStudentsCommandHandler : ICommitImportStudentsCommandHandler
{
    private static readonly TimeSpan ProcessingLease = TimeSpan.FromMinutes(5);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IApplicationDbContext _context;
    private readonly IUnitOfWork _unitOfWork;

    public CommitImportStudentsCommandHandler(
        IApplicationDbContext context,
        IUnitOfWork unitOfWork)
    {
        _context = context;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<ImportStudentsCommitResponse>> HandleAsync(
        Guid classId,
        CommitImportStudentsRequest request,
        Guid currentUserId,
        string currentUserRole,
        CancellationToken cancellationToken = default)
    {
        var isAdmin = string.Equals(currentUserRole, SystemRoles.Admin, StringComparison.OrdinalIgnoreCase);
        var isLecturer = string.Equals(currentUserRole, SystemRoles.Lecturer, StringComparison.OrdinalIgnoreCase);
        if (!isAdmin && !isLecturer)
        {
            return Failure(ErrorCodes.ClassAccessDenied, "You do not have permission to commit student imports.");
        }

        if (request.SessionId == Guid.Empty)
        {
            return Failure(ErrorCodes.ClassValidationError, "A valid import sessionId is required.");
        }

        var session = await _context.ClassImportSessions
            .FirstOrDefaultAsync(candidate => candidate.Id == request.SessionId, cancellationToken);

        if (session == null)
        {
            return Failure(ErrorCodes.ClassImportSessionInvalid, "Import session is invalid or has already been consumed.");
        }

        if (session.UserId != currentUserId || session.ClassId != classId)
        {
            return Failure(ErrorCodes.ClassImportSessionInvalid, "Import session does not belong to the current user and class.");
        }

        var now = DateTime.UtcNow;
        if (session.ExpiresAtUtc <= now)
        {
            return Failure(ErrorCodes.ClassImportSessionExpired, "Import session has expired. Preview the file again.");
        }

        if (session.Status == ClassImportSessionStatus.Consumed)
        {
            return Failure(ErrorCodes.ClassImportSessionInvalid, "Import session has already been consumed.");
        }

        if (session.Status == ClassImportSessionStatus.Processing &&
            session.ProcessingStartedAtUtc.HasValue &&
            session.ProcessingStartedAtUtc.Value.Add(ProcessingLease) > now)
        {
            return Failure(ErrorCodes.ClassImportSessionAlreadyProcessing, "Import session is already being committed.");
        }

        session.Status = ClassImportSessionStatus.Processing;
        session.ProcessingStartedAtUtc = now;
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            _context.ClearChanges();
            return Failure(ErrorCodes.ClassImportSessionAlreadyProcessing, "Import session is already being committed.");
        }

        var targetClass = await _context.Classes
            .FirstOrDefaultAsync(@class => @class.Id == classId, cancellationToken);

        if (targetClass == null)
        {
            await ReleaseAsync(session, cancellationToken);
            return Failure(ErrorCodes.ClassNotFound, "The requested class was not found.");
        }

        var mutationError = ClassStateRules.GetMutationError(targetClass.Status);
        if (mutationError != null)
        {
            await ReleaseAsync(session, cancellationToken);
            return Failure(mutationError.Code, mutationError.Message);
        }

        if (isLecturer && targetClass.PrimaryLecturerId != currentUserId)
        {
            await ReleaseAsync(session, cancellationToken);
            return Failure(ErrorCodes.ClassAccessDenied, "You can only import students to your assigned class.");
        }

        ImportStudentRowPreviewDto[] rows;
        try
        {
            rows = JsonSerializer.Deserialize<ImportStudentRowPreviewDto[]>(session.ValidRowsJson, JsonOptions) ?? [];
        }
        catch (JsonException)
        {
            await ReleaseAsync(session, cancellationToken);
            return Failure(ErrorCodes.ClassImportSessionInvalid, "Import session payload is invalid. Preview the file again.");
        }

        if (rows.Length == 0)
        {
            await ReleaseAsync(session, cancellationToken);
            return Failure(ErrorCodes.ClassImportNoValidRows, "No valid student rows are available to commit.");
        }

        var isTeamAssignment = rows.Any(row => !string.IsNullOrWhiteSpace(row.GroupName));

        try
        {
            var response = isTeamAssignment
                ? await _unitOfWork.ExecuteInSerializableTransactionAsync(
                    transactionCancellationToken => CommitTeamAssignmentsAsync(
                        targetClass,
                        session,
                        rows,
                        currentUserId,
                        transactionCancellationToken),
                    cancellationToken)
                : await _unitOfWork.ExecuteInTransactionAsync(
                    transactionCancellationToken => CommitRowsAsync(
                        targetClass,
                        session,
                        rows,
                        request.SynchronizeProfileMajors,
                        currentUserId,
                        transactionCancellationToken),
                    cancellationToken);

            return Result.Success(response);
        }
        catch (DbUpdateException)
        {
            await ResetAfterFailureAsync(request.SessionId, cancellationToken);
            return Failure(
                isTeamAssignment ? ErrorCodes.TeamMembershipConflict : ErrorCodes.ClassStudentEnrollmentConflict,
                isTeamAssignment
                    ? "The team assignment conflicted with another team update. Preview the file again."
                    : "The import conflicted with another enrollment update. Preview the file again.");
        }
        catch (SerializableTransactionConflictException)
        {
            await ResetAfterFailureAsync(request.SessionId, cancellationToken);
            return Failure(
                ErrorCodes.ClassConcurrencyConflict,
                "The class team data changed concurrently. Preview the file again.");
        }
        catch
        {
            await ResetAfterFailureAsync(request.SessionId, cancellationToken);
            throw;
        }
    }

    private async Task<ImportStudentsCommitResponse> CommitRowsAsync(
        Class targetClass,
        ClassImportSession session,
        IReadOnlyCollection<ImportStudentRowPreviewDto> rows,
        bool synchronizeProfileMajors,
        Guid currentUserId,
        CancellationToken cancellationToken)
    {
        var codes = rows.Select(row => row.StudentCode).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var emails = rows.Select(row => row.Email).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var profiles = await _context.Students
            .Where(student =>
                (student.NormalizedRollNumber != null && codes.Contains(student.NormalizedRollNumber)) ||
                (student.RollNumber != null && codes.Contains(student.RollNumber)) ||
                (student.Email != null && emails.Contains(student.Email.ToLower())))
            .ToListAsync(cancellationToken);

        var profileIds = profiles.Select(student => student.Id).ToArray();
        var enrollments = profileIds.Length == 0
            ? []
            : await _context.ClassStudents
                .Include(enrollment => enrollment.Class)
                .Where(enrollment =>
                    profileIds.Contains(enrollment.StudentId) &&
                    (enrollment.ClassId == targetClass.Id ||
                     (enrollment.CountsTowardCourseSemesterLimit &&
                      enrollment.SemesterId == targetClass.SemesterId &&
                      enrollment.CourseId == targetClass.CourseId)))
                .ToListAsync(cancellationToken);

        var profilesByCode = profiles
            .Where(student => !string.IsNullOrWhiteSpace(student.NormalizedRollNumber ?? student.RollNumber))
            .GroupBy(student => student.NormalizedRollNumber ?? student.RollNumber!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.OrdinalIgnoreCase);
        var profilesByEmail = profiles
            .Where(student => !string.IsNullOrWhiteSpace(student.Email))
            .GroupBy(student => student.Email!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.OrdinalIgnoreCase);

        var insertedCount = 0;
        var updatedCount = 0;
        var reEnrolledCount = 0;
        var synchronizedMajorCount = 0;
        var errors = new List<ImportStudentCommitErrorDto>();
        var importedStudents = new List<(string Email, string FullName, Guid? UserId)>();

        foreach (var row in rows)
        {
            profilesByCode.TryGetValue(row.StudentCode, out var codeProfiles);
            var profileByCode = codeProfiles?.Count == 1 ? codeProfiles[0] : null;
            profilesByEmail.TryGetValue(row.Email, out var emailProfiles);
            var profileByEmail = emailProfiles?.Count == 1 ? emailProfiles[0] : null;

            if (StudentImportIdentityRules.HasConflict(
                    codeProfiles?.Count ?? 0,
                    emailProfiles?.Count ?? 0,
                    profileByCode,
                    profileByEmail,
                    row.StudentCode,
                    row.Email))
            {
                errors.Add(RowError(row, ErrorCodes.ClassStudentIdentityConflict, "Student code and email no longer identify one unique student profile."));
                continue;
            }

            var profile = profileByCode ?? profileByEmail;
            var currentEnrollment = profile == null
                ? null
                : enrollments.FirstOrDefault(enrollment =>
                    enrollment.StudentId == profile.Id && enrollment.ClassId == targetClass.Id);
            if (currentEnrollment != null && currentEnrollment.EnrollmentStatus != EnrollmentStatus.Dropped)
            {
                errors.Add(RowError(
                    row,
                    ErrorCodes.ClassStudentAlreadyEnrolled,
                    "Student already has an enrollment in this class."));
                continue;
            }

            var conflict = profile == null
                ? null
                : enrollments.FirstOrDefault(enrollment =>
                    enrollment.StudentId == profile.Id &&
                    enrollment.ClassId != targetClass.Id &&
                    enrollment.CountsTowardCourseSemesterLimit);
            if (conflict != null)
            {
                errors.Add(RowError(
                    row,
                    ErrorCodes.ClassStudentEnrollmentConflict,
                    $"Student is already enrolled in class '{conflict.Class.ClassCode}' for the same course and semester."));
                continue;
            }

            var rowUpdated = false;
            if (profile == null)
            {
                profile = new Student
                {
                    RollNumber = row.StudentCode,
                    NormalizedRollNumber = row.StudentCode,
                    FullName = row.FullName,
                    Email = row.Email,
                    MajorCode = MajorCodes.IsValid(row.MajorCode) ? row.MajorCode : null,
                    Status = StudentStatus.Active,
                    CreatedBy = currentUserId
                };
                _context.Students.Add(profile);
                profilesByCode[row.StudentCode] = [profile];
                profilesByEmail[row.Email] = [profile];
            }
            else
            {
                if (StudentImportIdentityRules.CompleteMissingIdentity(profile, row.StudentCode, row.Email))
                {
                    profile.UpdatedAt = DateTime.UtcNow;
                    profile.UpdatedBy = currentUserId;
                    profilesByCode[row.StudentCode] = [profile];
                    profilesByEmail[row.Email] = [profile];
                    rowUpdated = true;
                }

                if (synchronizeProfileMajors &&
                    profile.UserId.HasValue &&
                    MajorCodes.IsValid(row.MajorCode))
                {
                    var importedMajor = row.MajorCode.Trim().ToUpperInvariant();
                    var registeredMajor = profile.MajorCode?.Trim().ToUpperInvariant();
                    if (!string.Equals(importedMajor, registeredMajor, StringComparison.OrdinalIgnoreCase))
                    {
                        profile.MajorCode = importedMajor;
                        profile.UpdatedAt = DateTime.UtcNow;
                        profile.UpdatedBy = currentUserId;
                        synchronizedMajorCount++;
                    }
                }
            }

            if (currentEnrollment != null)
            {
                currentEnrollment.EnrollmentStatus = EnrollmentStatus.Active;
                currentEnrollment.CountsTowardCourseSemesterLimit = true;
                currentEnrollment.CompletedAtUtc = null;
                currentEnrollment.CompletedByUserId = null;
                currentEnrollment.MajorCodeAtEnrollment = row.MajorCode;
                currentEnrollment.MajorVerificationStatus = EnrollmentMajorVerificationStatus.Unverified;
                currentEnrollment.MajorVerifiedAtUtc = null;
                currentEnrollment.MajorVerifiedByUserId = null;
                currentEnrollment.UpdatedAt = DateTime.UtcNow;
                rowUpdated = true;
                reEnrolledCount++;
            }
            else
            {
                currentEnrollment = new ClassStudent
                {
                    ClassId = targetClass.Id,
                    StudentId = profile.Id,
                    SemesterId = targetClass.SemesterId,
                    CourseId = targetClass.CourseId,
                    EnrollmentStatus = EnrollmentStatus.Active,
                    CountsTowardCourseSemesterLimit = true,
                    MajorCodeAtEnrollment = row.MajorCode,
                    MajorVerificationStatus = EnrollmentMajorVerificationStatus.Unverified,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                _context.ClassStudents.Add(currentEnrollment);
                enrollments.Add(currentEnrollment);
                insertedCount++;
            }

            if (rowUpdated)
            {
                updatedCount++;
            }

            importedStudents.Add((profile.Email ?? row.Email, profile.FullName, profile.UserId));
        }

        session.Status = ClassImportSessionStatus.Consumed;
        session.ConsumedAtUtc = DateTime.UtcNow;
        session.ProcessingStartedAtUtc = null;

        _context.ClassAuditLogs.Add(new ClassAuditLog
        {
            ClassId = targetClass.Id,
            Action = "STUDENT_IMPORT_COMMITTED",
            PerformedByUserId = currentUserId,
            OccurredAtUtc = DateTime.UtcNow,
            DetailsJson = JsonSerializer.Serialize(new
            {
                SessionId = session.Id,
                InsertedCount = insertedCount,
                UpdatedCount = updatedCount,
                ReEnrolledCount = reEnrolledCount,
                SynchronizedMajorCount = synchronizedMajorCount,
                SynchronizeProfileMajors = synchronizeProfileMajors,
                ErrorCount = errors.Count
            }, JsonOptions)
        });
        ClassOutbox.Enqueue(_context, "Class.StudentRosterImported.v1", targetClass.Id, new
        {
            SessionId = session.Id,
            InsertedCount = insertedCount,
            UpdatedCount = updatedCount,
            ReEnrolledCount = reEnrolledCount,
            ErrorCount = errors.Count,
            StudentUserIds = importedStudents
                .Where(student => student.UserId.HasValue)
                .Select(student => student.UserId!.Value)
                .Distinct()
                .ToArray(),
            StudentRecipients = importedStudents
                .Where(student => !string.IsNullOrWhiteSpace(student.Email))
                .Select(student => new { student.Email, student.FullName })
                .Distinct()
                .ToArray()
        });

        await _context.SaveChangesAsync(cancellationToken);

        return new ImportStudentsCommitResponse
        {
            ImportMode = "StudentRoster",
            InsertedCount = insertedCount,
            UpdatedCount = updatedCount,
            SynchronizedMajorCount = synchronizedMajorCount,
            SkippedCount = errors.Count,
            ErrorCount = errors.Count,
            Errors = errors
        };
    }

    private async Task<ImportStudentsCommitResponse> CommitTeamAssignmentsAsync(
        Class targetClass,
        ClassImportSession session,
        IReadOnlyCollection<ImportStudentRowPreviewDto> rows,
        Guid currentUserId,
        CancellationToken cancellationToken)
    {
        var codes = rows.Select(row => row.StudentCode).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var emails = rows.Select(row => row.Email).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var profiles = await _context.Students
            .Where(student =>
                (student.NormalizedRollNumber != null && codes.Contains(student.NormalizedRollNumber)) ||
                (student.RollNumber != null && codes.Contains(student.RollNumber)) ||
                (student.Email != null && emails.Contains(student.Email.ToLower())))
            .ToListAsync(cancellationToken);
        var profilesByCode = profiles
            .Where(student => !string.IsNullOrWhiteSpace(student.NormalizedRollNumber ?? student.RollNumber))
            .GroupBy(student => student.NormalizedRollNumber ?? student.RollNumber!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.OrdinalIgnoreCase);
        var profilesByEmail = profiles
            .Where(student => !string.IsNullOrWhiteSpace(student.Email))
            .GroupBy(student => student.Email!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.OrdinalIgnoreCase);
        var profileIds = profiles.Select(student => student.Id).ToArray();
        List<ClassStudent> enrollments = profileIds.Length == 0
            ? []
            : await _context.ClassStudents
                .Include(enrollment => enrollment.Class)
                .Include(enrollment => enrollment.Student)
                .Where(enrollment =>
                    profileIds.Contains(enrollment.StudentId) &&
                    (enrollment.ClassId == targetClass.Id ||
                     (enrollment.CountsTowardCourseSemesterLimit &&
                      enrollment.SemesterId == targetClass.SemesterId &&
                      enrollment.CourseId == targetClass.CourseId)))
                .ToListAsync(cancellationToken);
        var assignedStudentIds = (await _context.TeamMembers
                .AsNoTracking()
                .Where(member =>
                    member.ClassId == targetClass.Id &&
                    profileIds.Contains(member.StudentId) &&
                    member.CountsTowardActiveTeam)
                .Select(member => member.StudentId)
                .ToListAsync(cancellationToken))
            .ToHashSet();
        var proposedStudentIds = (await _context.TeamProposalMembers
                .AsNoTracking()
                .Where(member =>
                    member.ClassId == targetClass.Id &&
                    profileIds.Contains(member.StudentId) &&
                    member.CountsTowardOpenProposal)
                .Select(member => member.StudentId)
                .ToListAsync(cancellationToken))
            .ToHashSet();
        var existingTeamNames = (await _context.Teams
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(team => team.ClassId == targetClass.Id)
                .Select(team => team.TeamName)
                .ToListAsync(cancellationToken))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var existingProposalNames = (await _context.TeamProposals
                .AsNoTracking()
                .Where(proposal =>
                    proposal.ClassId == targetClass.Id &&
                    proposal.Status != TeamProposalStatus.Rejected &&
                    proposal.Status != TeamProposalStatus.Cancelled)
                .Select(proposal => proposal.TeamName)
                .ToListAsync(cancellationToken))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var prefix = CreateTeamCodePrefix(targetClass.ClassCode);
        var existingCodes = await _context.Teams
            .IgnoreQueryFilters()
            .Where(team => team.ClassId == targetClass.Id && team.TeamCode.StartsWith(prefix))
            .Select(team => team.TeamCode)
            .ToListAsync(cancellationToken);
        var nextSequence = GetHighestTeamSequence(existingCodes, prefix);
        var now = DateTime.UtcNow;
        var createdTeamCount = 0;
        var createdMembershipCount = 0;
        var createdProjectCount = 0;
        var insertedCount = 0;
        var updatedCount = 0;
        var reEnrolledCount = 0;
        var errors = new List<ImportStudentCommitErrorDto>();
        var importedStudents = new List<(string Email, string FullName, Guid? UserId)>();

        foreach (var group in rows
                     .Where(row => !string.IsNullOrWhiteSpace(row.GroupName))
                     .GroupBy(row => row.GroupName!, StringComparer.OrdinalIgnoreCase)
                     .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase))
        {
            var groupRows = group.ToArray();
            var groupError = ValidateTeamAssignmentGroup(
                group.Key,
                groupRows,
                existingTeamNames,
                existingProposalNames);
            if (groupError != null)
            {
                errors.AddRange(groupRows.Select(row => RowError(row, groupError.Value.Code, groupError.Value.Message)));
                continue;
            }

            var resolvedMembers = new List<(ImportStudentRowPreviewDto Row, Student? Profile, ClassStudent? Enrollment)>();
            foreach (var row in groupRows)
            {
                profilesByCode.TryGetValue(row.StudentCode, out var codeProfiles);
                var profileByCode = codeProfiles?.Count == 1 ? codeProfiles[0] : null;
                profilesByEmail.TryGetValue(row.Email, out var emailProfiles);
                var profileByEmail = emailProfiles?.Count == 1 ? emailProfiles[0] : null;
                if (StudentImportIdentityRules.HasConflict(
                        codeProfiles?.Count ?? 0,
                        emailProfiles?.Count ?? 0,
                        profileByCode,
                        profileByEmail,
                        row.StudentCode,
                        row.Email))
                {
                    groupError = (
                        ErrorCodes.ClassStudentIdentityConflict,
                        $"Student '{row.StudentCode}' no longer identifies one unique student profile.");
                    break;
                }

                var profile = profileByCode ?? profileByEmail;
                var currentEnrollment = profile == null
                    ? null
                    : enrollments.FirstOrDefault(enrollment =>
                        enrollment.StudentId == profile.Id && enrollment.ClassId == targetClass.Id);
                var enrollmentConflict = profile == null
                    ? null
                    : enrollments.FirstOrDefault(enrollment =>
                        enrollment.StudentId == profile.Id &&
                        enrollment.ClassId != targetClass.Id &&
                        enrollment.CountsTowardCourseSemesterLimit);
                if (enrollmentConflict != null)
                {
                    groupError = (
                        ErrorCodes.ClassStudentEnrollmentConflict,
                        $"Student '{row.StudentCode}' is already enrolled in class '{enrollmentConflict.Class.ClassCode}' for the same course and semester.");
                    break;
                }

                if (currentEnrollment?.EnrollmentStatus == EnrollmentStatus.Completed)
                {
                    groupError = (
                        ErrorCodes.ClassStudentAlreadyEnrolled,
                        $"Student '{row.StudentCode}' has already completed this class and cannot be assigned to a new team.");
                    break;
                }

                if (profile != null && assignedStudentIds.Contains(profile.Id))
                {
                    groupError = (
                        ErrorCodes.TeamMembershipConflict,
                        $"Student '{row.StudentCode}' already belongs to an active team.");
                    break;
                }

                if (profile != null && proposedStudentIds.Contains(profile.Id))
                {
                    groupError = (
                        ErrorCodes.TeamProposalMembershipConflict,
                        $"Student '{row.StudentCode}' belongs to an open team proposal.");
                    break;
                }

                resolvedMembers.Add((row, profile, currentEnrollment));
            }

            if (groupError != null)
            {
                errors.AddRange(groupRows.Select(row => RowError(row, groupError.Value.Code, groupError.Value.Message)));
                continue;
            }

            var projectName = FirstNonEmpty(groupRows.Select(row => row.ProjectName))!;
            var zaloGroupUrl = FirstNonEmpty(groupRows.Select(row => row.ZaloGroupUrl));
            var projectDescription = FirstNonEmpty(groupRows.Select(row => row.ProjectDescription));
            var team = new Team
            {
                ClassId = targetClass.Id,
                Class = targetClass,
                TeamCode = $"{prefix}{++nextSequence}",
                TeamName = group.Key.Trim(),
                Status = TeamStatus.Active,
                CreatedById = currentUserId,
                CreatedBy = currentUserId,
                CreatedAt = now
            };

            foreach (var resolved in resolvedMembers)
            {
                var row = resolved.Row;
                var profile = resolved.Profile;
                var enrollment = resolved.Enrollment;
                var rowUpdated = false;
                var enrollmentChanged = false;
                if (profile == null)
                {
                    profile = new Student
                    {
                        RollNumber = row.StudentCode,
                        NormalizedRollNumber = row.StudentCode,
                        FullName = row.FullName,
                        Email = row.Email,
                        MajorCode = MajorCodes.IsValid(row.MajorCode) ? row.MajorCode : null,
                        Status = StudentStatus.Active,
                        CreatedBy = currentUserId
                    };
                    _context.Students.Add(profile);
                    profilesByCode[row.StudentCode] = [profile];
                    profilesByEmail[row.Email] = [profile];
                }
                else if (StudentImportIdentityRules.CompleteMissingIdentity(profile, row.StudentCode, row.Email))
                {
                    profile.UpdatedAt = now;
                    profile.UpdatedBy = currentUserId;
                    profilesByCode[row.StudentCode] = [profile];
                    profilesByEmail[row.Email] = [profile];
                    rowUpdated = true;
                }

                if (enrollment == null)
                {
                    enrollment = new ClassStudent
                    {
                        ClassId = targetClass.Id,
                        Class = targetClass,
                        StudentId = profile.Id,
                        Student = profile,
                        SemesterId = targetClass.SemesterId,
                        CourseId = targetClass.CourseId,
                        EnrollmentStatus = EnrollmentStatus.Active,
                        CountsTowardCourseSemesterLimit = true,
                        MajorCodeAtEnrollment = row.MajorCode,
                        MajorVerificationStatus = EnrollmentMajorVerificationStatus.Unverified,
                        CreatedAt = now,
                        UpdatedAt = now
                    };
                    _context.ClassStudents.Add(enrollment);
                    enrollments.Add(enrollment);
                    insertedCount++;
                    enrollmentChanged = true;
                }
                else if (enrollment.EnrollmentStatus == EnrollmentStatus.Dropped)
                {
                    enrollment.EnrollmentStatus = EnrollmentStatus.Active;
                    enrollment.CountsTowardCourseSemesterLimit = true;
                    enrollment.CompletedAtUtc = null;
                    enrollment.CompletedByUserId = null;
                    enrollment.MajorCodeAtEnrollment = row.MajorCode;
                    enrollment.MajorVerificationStatus = EnrollmentMajorVerificationStatus.Unverified;
                    enrollment.MajorVerifiedAtUtc = null;
                    enrollment.MajorVerifiedByUserId = null;
                    enrollment.UpdatedAt = now;
                    rowUpdated = true;
                    enrollmentChanged = true;
                    reEnrolledCount++;
                }

                if (rowUpdated)
                {
                    updatedCount++;
                }
                if (enrollmentChanged)
                {
                    importedStudents.Add((profile.Email ?? row.Email, profile.FullName, profile.UserId));
                }

                team.TeamMembers.Add(new TeamMember
                {
                    TeamId = team.Id,
                    Team = team,
                    ClassId = targetClass.Id,
                    StudentId = enrollment.StudentId,
                    ClassStudent = enrollment,
                    RoleInTeam = TeamMemberRole.Member,
                    CountsTowardActiveTeam = true,
                    JoinedAt = now,
                    CreatedById = currentUserId
                });
            }

            var project = new Project
            {
                TeamId = team.Id,
                Team = team,
                Name = projectName,
                Description = projectDescription,
                ZaloGroupUrl = zaloGroupUrl,
                Status = ProjectStatus.Draft,
                CreatedById = currentUserId,
                CreatedBy = currentUserId,
                CreatedAt = now
            };
            project.ActivityLogs.Add(new ProjectActivityLog
            {
                ProjectId = project.Id,
                Project = project,
                ActorUserId = currentUserId,
                Action = "IMPORTED_FROM_CLASS_FILE",
                Summary = "Created the project from the class team assignment import.",
                ChangedFieldsJson = JsonSerializer.Serialize(
                    new[] { "projectName", "description", "zaloGroupUrl" },
                    JsonOptions),
                OccurredAtUtc = now
            });
            team.Project = project;
            _context.Teams.Add(team);

            var memberUserIds = team.TeamMembers
                .Select(member => member.ClassStudent.Student.UserId)
                .Where(userId => userId.HasValue)
                .Select(userId => userId!.Value)
                .Distinct()
                .ToArray();
            ClassOutbox.Enqueue(_context, "Team.Created.v1", targetClass.Id, new
            {
                TeamId = team.Id,
                team.TeamName,
                StudentUserIds = memberUserIds
            }, now);
            ClassOutbox.Enqueue(_context, "ProjectWorkspace.Created.v1", targetClass.Id, new
            {
                ProjectId = project.Id,
                TeamId = team.Id,
                ClassId = targetClass.Id,
                SubjectId = targetClass.CourseId,
                SemesterId = targetClass.SemesterId,
                LeaderUserId = (Guid?)null
            }, now);

            existingTeamNames.Add(team.TeamName);
            foreach (var member in team.TeamMembers)
            {
                assignedStudentIds.Add(member.StudentId);
            }
            createdTeamCount++;
            createdMembershipCount += groupRows.Length;
            createdProjectCount++;
        }

        session.Status = ClassImportSessionStatus.Consumed;
        session.ConsumedAtUtc = now;
        session.ProcessingStartedAtUtc = null;
        _context.ClassAuditLogs.Add(new ClassAuditLog
        {
            ClassId = targetClass.Id,
            Action = "TEAM_ASSIGNMENT_IMPORT_COMMITTED",
            PerformedByUserId = currentUserId,
            OccurredAtUtc = now,
            DetailsJson = JsonSerializer.Serialize(new
            {
                SessionId = session.Id,
                CreatedTeamCount = createdTeamCount,
                CreatedMembershipCount = createdMembershipCount,
                CreatedProjectCount = createdProjectCount,
                InsertedCount = insertedCount,
                UpdatedCount = updatedCount,
                ReEnrolledCount = reEnrolledCount,
                ErrorCount = errors.Count
            }, JsonOptions)
        });
        if (importedStudents.Count > 0)
        {
            ClassOutbox.Enqueue(_context, "Class.StudentRosterImported.v1", targetClass.Id, new
            {
                SessionId = session.Id,
                InsertedCount = insertedCount,
                UpdatedCount = updatedCount,
                ReEnrolledCount = reEnrolledCount,
                ErrorCount = errors.Count,
                StudentUserIds = importedStudents
                    .Where(student => student.UserId.HasValue)
                    .Select(student => student.UserId!.Value)
                    .Distinct()
                    .ToArray(),
                StudentRecipients = importedStudents
                    .Where(student => !string.IsNullOrWhiteSpace(student.Email))
                    .Select(student => new { student.Email, student.FullName })
                    .Distinct()
                    .ToArray()
            }, now);
        }

        await _context.SaveChangesAsync(cancellationToken);

        return new ImportStudentsCommitResponse
        {
            ImportMode = "TeamAssignment",
            InsertedCount = insertedCount,
            UpdatedCount = updatedCount,
            CreatedTeamCount = createdTeamCount,
            CreatedMembershipCount = createdMembershipCount,
            CreatedProjectCount = createdProjectCount,
            SkippedCount = errors.Count,
            ErrorCount = errors.Count,
            Errors = errors
        };
    }

    private static (string Code, string Message)? ValidateTeamAssignmentGroup(
        string groupName,
        IReadOnlyCollection<ImportStudentRowPreviewDto> rows,
        IReadOnlySet<string> existingTeamNames,
        IReadOnlySet<string> existingProposalNames)
    {
        if (rows.Count is < 4 or > 6)
            return (ErrorCodes.ClassValidationError, $"Team '{groupName}' must contain 4 to 6 students.");
        if (existingTeamNames.Contains(groupName))
            return (ErrorCodes.TeamNameDuplicated, $"A team named '{groupName}' already exists in this class.");
        if (existingProposalNames.Contains(groupName))
            return (ErrorCodes.TeamNameDuplicated, $"An open team proposal named '{groupName}' already exists in this class.");

        var projectNames = DistinctNonEmpty(rows.Select(row => row.ProjectName));
        var zaloUrls = DistinctNonEmpty(rows.Select(row => row.ZaloGroupUrl));
        var descriptions = DistinctNonEmpty(rows.Select(row => row.ProjectDescription));
        if (projectNames.Length != 1 || projectNames[0].Length is < 3 or > 200)
            return (ErrorCodes.ClassValidationError, $"Team '{groupName}' must have one Project name between 3 and 200 characters.");
        if (zaloUrls.Length > 1 || (zaloUrls.Length == 1 && !IsValidZaloUrl(zaloUrls[0])))
            return (ErrorCodes.ClassValidationError, $"Team '{groupName}' must have at most one valid HTTPS zalo.me link.");
        if (descriptions.Length > 1 ||
            (descriptions.Length == 1 && descriptions[0].Length is < 20 or > 2_000))
            return (ErrorCodes.ClassValidationError, $"Team '{groupName}' must have at most one project description between 20 and 2000 characters.");

        return null;
    }

    private static string CreateTeamCodePrefix(string classCode)
    {
        const int maximumTeamCodeLength = 50;
        const string suffixPrefix = "_TEAM_";
        var maximumClassCodeLength = maximumTeamCodeLength - suffixPrefix.Length - 10;
        var normalizedClassCode = classCode.Trim();
        if (normalizedClassCode.Length > maximumClassCodeLength)
            normalizedClassCode = normalizedClassCode[..maximumClassCodeLength];
        return $"{normalizedClassCode}{suffixPrefix}";
    }

    private static int GetHighestTeamSequence(IEnumerable<string> codes, string prefix)
    {
        var highest = 0;
        foreach (var code in codes)
        {
            if (code.Length > prefix.Length &&
                int.TryParse(code[prefix.Length..], out var sequence) &&
                sequence > highest)
            {
                highest = sequence;
            }
        }
        return highest;
    }

    private static string[] DistinctNonEmpty(IEnumerable<string?> values) => values
        .Where(value => !string.IsNullOrWhiteSpace(value))
        .Select(value => value!.Trim())
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();

    private static string? FirstNonEmpty(IEnumerable<string?> values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim();

    private static bool IsValidZaloUrl(string value) =>
        value.Length <= 500 &&
        Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
        uri.Scheme == Uri.UriSchemeHttps &&
        (uri.Host.Equals("zalo.me", StringComparison.OrdinalIgnoreCase) ||
         uri.Host.EndsWith(".zalo.me", StringComparison.OrdinalIgnoreCase));

    private async Task ReleaseAsync(ClassImportSession session, CancellationToken cancellationToken)
    {
        session.Status = ClassImportSessionStatus.Available;
        session.ProcessingStartedAtUtc = null;
        await _context.SaveChangesAsync(cancellationToken);
    }

    private async Task ResetAfterFailureAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        try
        {
            _context.ClearChanges();
            var session = await _context.ClassImportSessions
                .FirstOrDefaultAsync(candidate => candidate.Id == sessionId, cancellationToken);
            if (session?.Status == ClassImportSessionStatus.Processing)
            {
                session.Status = ClassImportSessionStatus.Available;
                session.ProcessingStartedAtUtc = null;
                await _context.SaveChangesAsync(cancellationToken);
            }
        }
        catch
        {
            // The lease permits recovery if infrastructure is unavailable here.
        }
    }

    private static ImportStudentCommitErrorDto RowError(
        ImportStudentRowPreviewDto row,
        string errorCode,
        string errorMessage) => new()
    {
        RowNumber = row.RowNumber,
        StudentCode = row.StudentCode,
        ErrorCode = errorCode,
        ErrorMessage = errorMessage
    };

    private static Result<ImportStudentsCommitResponse> Failure(string code, string message) =>
        Result.Failure<ImportStudentsCommitResponse>(new Error(code, message));
}
