using System.Security.Cryptography;
using System.Text.Json;
using EHub.Application.Common.Exceptions;
using EHub.Application.Common.Interfaces.Identity;
using EHub.Application.Common.Interfaces.Persistence;
using EHub.Application.Common.Interfaces.Services;
using EHub.Application.Features.Classes.Common;
using EHub.Contracts.Mentors;
using EHub.Domain.Entities;
using EHub.Domain.Enums;
using EHub.Shared.Constants;
using EHub.Shared.Errors;
using EHub.Shared.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace EHub.Application.Features.Admin.Mentors;

public sealed class MentorAdminHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUser,
    IPasswordHasher passwordHasher,
    IDateTimeProvider dateTimeProvider,
    IUnitOfWork unitOfWork) : IMentorAdminHandler
{
    private const long MaximumFileSize = 5 * 1024 * 1024;
    private static readonly TimeSpan SessionLifetime = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan ProcessingLease = TimeSpan.FromMinutes(5);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public Task<Result<(byte[] FileBytes, string ContentType, string FileName)>> GetTemplateAsync(CancellationToken cancellationToken = default)
    {
        if (!TryGetAdminId(out _)) return Task.FromResult(Failure<(byte[], string, string)>(ErrorCodes.CommonUnauthorizedError, "An authenticated administrator is required."));
        const string contentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
        return Task.FromResult(Result.Success((MentorImportTemplateBuilder.Build(), contentType, "Danh_sach_Mentor_FA26_mau.xlsx")));
    }

    public async Task<Result<MentorImportPreviewResponse>> PreviewImportAsync(Guid semesterId, IFormFile file, CancellationToken cancellationToken = default)
    {
        if (!TryGetAdminId(out var adminId)) return Failure<MentorImportPreviewResponse>(ErrorCodes.CommonUnauthorizedError, "An authenticated administrator is required.");
        if (semesterId == Guid.Empty || !await context.Semesters.AsNoTracking().AnyAsync(item => item.Id == semesterId, cancellationToken))
            return Failure<MentorImportPreviewResponse>(ErrorCodes.SemesterNotFound, "The selected semester was not found.");
        if (file is null || file.Length == 0 || file.Length > MaximumFileSize)
            return Failure<MentorImportPreviewResponse>(ErrorCodes.MentorImportFileInvalid, "Select a non-empty .xlsx file not exceeding 5 MB.");
        if (!Path.GetExtension(file.FileName).Equals(".xlsx", StringComparison.OrdinalIgnoreCase))
            return Failure<MentorImportPreviewResponse>(ErrorCodes.MentorImportFileInvalid, "Only .xlsx mentor workbooks are allowed.");
        var security = ExcelWorkbookSecurity.Validate(file);
        if (security.IsFailure || security.Value != ExcelWorkbookKind.OpenXml)
            return Failure<MentorImportPreviewResponse>(ErrorCodes.MentorImportFileInvalid, security.IsFailure ? security.Error.Message : "Only .xlsx mentor workbooks are allowed.");

        var parse = MentorImportWorkbookParser.Parse(file);
        if (parse.IsFailure) return Result.Failure<MentorImportPreviewResponse>(parse.Error);
        var rows = parse.Value;
        await ValidateImportRowsAsync(semesterId, rows, cancellationToken);
        var errorCount = rows.Count(item => !item.IsValid);
        var actionable = rows.Count(item => item.IsValid && item.Status != "AlreadyInSemester");
        var canCommit = errorCount == 0 && actionable > 0;
        var sessionId = Guid.Empty;
        if (canCommit)
        {
            var now = dateTimeProvider.UtcNow;
            sessionId = Guid.NewGuid();
            context.MentorImportSessions.Add(new MentorImportSession
            {
                Id = sessionId,
                AdminUserId = adminId,
                SemesterId = semesterId,
                RowsJson = JsonSerializer.Serialize(rows, JsonOptions),
                CreatedAtUtc = now,
                ExpiresAtUtc = now.Add(SessionLifetime)
            });
            await context.SaveChangesAsync(cancellationToken);
        }

        return Result.Success(new MentorImportPreviewResponse
        {
            SessionId = sessionId,
            SemesterId = semesterId,
            TotalRows = rows.Count,
            CreateCount = rows.Count(item => item.Status == "Create"),
            UpdateCount = rows.Count(item => item.Status == "Update"),
            AddToSemesterCount = rows.Count(item => item.Status == "AddToSemester"),
            ErrorCount = errorCount,
            CanCommit = canCommit,
            Rows = rows.Select(ToPreview).ToArray()
        });
    }

    public async Task<Result<MentorImportCommitResponse>> CommitImportAsync(CommitMentorImportRequest request, CancellationToken cancellationToken = default)
    {
        if (!TryGetAdminId(out var adminId)) return Failure<MentorImportCommitResponse>(ErrorCodes.CommonUnauthorizedError, "An authenticated administrator is required.");
        var lease = await AcquireImportSessionAsync(request.SessionId, adminId, cancellationToken);
        if (lease.IsFailure) return Result.Failure<MentorImportCommitResponse>(lease.Error);
        var session = lease.Value;
        MentorImportCandidate[] rows;
        try { rows = JsonSerializer.Deserialize<MentorImportCandidate[]>(session.RowsJson, JsonOptions) ?? []; }
        catch (JsonException) { return await ReleaseImportFailure(session, ErrorCodes.MentorImportSessionInvalid, "The import session is damaged. Preview the workbook again.", cancellationToken); }
        if (rows.Length == 0)
            return await ReleaseImportFailure(session, ErrorCodes.MentorImportSessionInvalid, "The import session contains no mentor rows. Preview the workbook again.", cancellationToken);

        try
        {
            var response = await unitOfWork.ExecuteInTransactionAsync(async token =>
            {
                await ValidateImportRowsAsync(session.SemesterId, rows, token);
                if (rows.Any(item => !item.IsValid)) throw new MentorAdminConflictException("Mentor accounts changed after preview.");
                var role = await context.Roles.FirstOrDefaultAsync(item => item.Name == SystemRoles.Mentor, token)
                    ?? throw new InvalidOperationException("The Mentor role has not been seeded.");
                var emails = rows.Select(item => item.Email).Distinct().ToArray();
                var users = await context.Users.Include(item => item.UserRoles).ThenInclude(item => item.Role)
                    .Include(item => item.MentorProfile)
                    .Where(item => emails.Contains(item.NormalizedEmail)).ToListAsync(token);
                var byEmail = users.ToDictionary(item => item.NormalizedEmail, StringComparer.OrdinalIgnoreCase);
                var pendingRegistrations = await context.PendingRegistrations
                    .Where(item => emails.Contains(item.NormalizedEmail) && item.Status == PendingRegistrationStatus.Pending)
                    .ToListAsync(token);
                var pendingByEmail = pendingRegistrations
                    .ToDictionary(item => item.NormalizedEmail, StringComparer.OrdinalIgnoreCase);
                var semesterStaff = await context.SemesterStaffAssignments
                    .Where(item => item.SemesterId == session.SemesterId && item.Role == SemesterStaffRole.Mentor &&
                                   users.Select(user => user.Id).Contains(item.UserId)).ToListAsync(token);
                var created = 0;
                var updated = 0;
                var assigned = 0;
                var now = dateTimeProvider.UtcNow;
                foreach (var row in rows)
                {
                    if (!byEmail.TryGetValue(row.Email, out var user))
                    {
                        user = new User
                        {
                            FullName = row.FullName,
                            Email = row.Email,
                            NormalizedEmail = row.Email,
                            Phone = row.Phone,
                            PasswordHash = passwordHasher.Hash(Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))),
                            Status = UserStatus.Active,
                            // Admin-provisioned accounts use the existing Forgot Password flow to set their first password.
                            IsEmailVerified = true,
                            CreatedBy = adminId
                        };
                        user.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = role.Id, AssignedAt = now, AssignedBy = adminId, Role = role });
                        user.MentorProfile = NewProfile(user, row, adminId);
                        context.Users.Add(user);
                        byEmail[row.Email] = user;
                        created++;
                    }
                    else
                    {
                        user.FullName = row.FullName;
                        if (row.MentorType == MentorType.Enterprise) user.Phone = row.Phone;
                        user.UpdatedBy = adminId;
                        if (user.Status == UserStatus.PendingApproval) user.Status = UserStatus.Active;
                        ApplyProfile(user.MentorProfile!, row, adminId);
                        updated++;
                    }

                    var staff = semesterStaff.FirstOrDefault(item => item.UserId == user.Id);
                    if (staff is null)
                    {
                        staff = new SemesterStaffAssignment
                        {
                            SemesterId = session.SemesterId,
                            UserId = user.Id,
                            User = user,
                            Role = SemesterStaffRole.Mentor,
                            Status = SemesterStaffStatus.Active,
                            CreatedBy = adminId
                        };
                        context.SemesterStaffAssignments.Add(staff);
                        semesterStaff.Add(staff);
                        assigned++;
                    }
                    else if (staff.Status != SemesterStaffStatus.Active)
                    {
                        staff.Status = SemesterStaffStatus.Active;
                        staff.UpdatedBy = adminId;
                        assigned++;
                    }

                    CancelPendingRegistration(row.Email, user.Id, pendingByEmail, now, adminId);
                }
                session.Status = MentorAdminSessionStatus.Consumed;
                session.ConsumedAtUtc = now;
                session.ProcessingStartedAtUtc = null;
                await context.SaveChangesAsync(token);
                return new MentorImportCommitResponse { CreatedCount = created, UpdatedCount = updated, SemesterAssignmentCount = assigned };
            }, cancellationToken);
            return Result.Success(response);
        }
        catch (MentorAdminConflictException)
        {
            await ResetImportSessionAsync(session.Id, cancellationToken);
            return Failure<MentorImportCommitResponse>(ErrorCodes.MentorImportConflict, "Mentor data changed after preview. Preview the workbook again.");
        }
        catch (DbUpdateException)
        {
            await ResetImportSessionAsync(session.Id, cancellationToken);
            return Failure<MentorImportCommitResponse>(ErrorCodes.MentorImportConflict, "Mentor data changed while the import was being committed. Preview again.");
        }
        catch
        {
            await ResetImportSessionAsync(session.Id, cancellationToken);
            throw;
        }
    }

    public async Task<Result<MentorAllocationPreviewResponse>> PreviewAllocationAsync(PreviewMentorAllocationRequest request, CancellationToken cancellationToken = default)
    {
        if (!TryGetAdminId(out var adminId)) return Failure<MentorAllocationPreviewResponse>(ErrorCodes.CommonUnauthorizedError, "An authenticated administrator is required.");
        if (request.SemesterId == Guid.Empty || !await context.Semesters.AsNoTracking().AnyAsync(item => item.Id == request.SemesterId, cancellationToken))
            return Failure<MentorAllocationPreviewResponse>(ErrorCodes.SemesterNotFound, "The selected semester was not found.");

        var classesQuery = context.Classes.AsNoTracking().Where(item => item.SemesterId == request.SemesterId && item.Status == ClassStatus.Active);
        if (request.ClassIds.Count > 0)
        {
            var distinctIds = request.ClassIds.Distinct().ToArray();
            classesQuery = classesQuery.Where(item => distinctIds.Contains(item.Id));
            if (await classesQuery.CountAsync(cancellationToken) != distinctIds.Length)
                return Failure<MentorAllocationPreviewResponse>(ErrorCodes.MentorAllocationInvalid, "Every selected class must be active and belong to the selected semester.");
        }
        var classes = await classesQuery.OrderBy(item => item.ClassCode).Select(item => new { item.Id, item.ClassCode }).ToListAsync(cancellationToken);
        if (classes.Count == 0) return Failure<MentorAllocationPreviewResponse>(ErrorCodes.MentorAllocationInvalid, "The selected scope has no active classes.");
        var classIds = classes.Select(item => item.Id).ToArray();
        var teams = await context.Teams.AsNoTracking().Where(item => classIds.Contains(item.ClassId) && item.Status == TeamStatus.Active)
            .OrderBy(item => item.TeamCode).Select(item => new AllocationTeam(item.Id, item.ClassId, item.TeamCode, item.TeamName)).ToListAsync(cancellationToken);
        if (teams.Count == 0) return Failure<MentorAllocationPreviewResponse>(ErrorCodes.MentorAllocationInvalid, "The selected classes have no active teams.");

        var mentors = await context.MentorProfiles.AsNoTracking()
            .Where(profile => profile.Status == MentorProfileStatus.Active && profile.User.Status == UserStatus.Active &&
                context.SemesterStaffAssignments.Any(staff => staff.SemesterId == request.SemesterId && staff.UserId == profile.UserId && staff.Role == SemesterStaffRole.Mentor && staff.Status == SemesterStaffStatus.Active))
            .Select(profile => new AllocationMentor(profile.Id, profile.User.FullName, profile.User.Email, profile.Type)).ToListAsync(cancellationToken);
        var active = await context.MentorAssignments.AsNoTracking()
            .Where(item => item.Status == MentorAssignmentStatus.Active && item.EndedAt == null && item.Team.Class.SemesterId == request.SemesterId)
            .Select(item => new { item.TeamId, item.MentorProfileId, item.Slot }).ToListAsync(cancellationToken);

        var seed = request.Seed ?? RandomNumberGenerator.GetInt32(int.MaxValue);
        var random = new Random(seed);
        var result = new List<MentorAllocationRowPreview>();
        var warnings = new List<string>();
        foreach (var type in new[] { MentorType.Enterprise, MentorType.Academic })
        {
            var pool = mentors.Where(item => item.Type == type).ToArray();
            var missing = teams.Where(team => active.All(item => item.TeamId != team.Id || item.Slot != type)).OrderBy(_ => random.Next()).ToArray();
            if (missing.Length > 0 && pool.Length == 0)
            {
                warnings.Add($"No active {type} mentors are available for {missing.Length} missing team slots.");
                continue;
            }
            var loads = pool.ToDictionary(item => item.Id, item => active.Count(assignment => assignment.MentorProfileId == item.Id));
            foreach (var team in missing)
            {
                var minimum = loads.Values.Min();
                var eligible = pool.Where(item => loads[item.Id] == minimum).OrderBy(_ => random.Next()).ToArray();
                var mentor = eligible[0];
                loads[mentor.Id]++;
                var classCode = classes.First(item => item.Id == team.ClassId).ClassCode;
                result.Add(new MentorAllocationRowPreview
                {
                    TeamId = team.Id, TeamCode = team.Code, TeamName = team.Name, ClassId = team.ClassId, ClassCode = classCode,
                    MentorType = type.ToString(), MentorProfileId = mentor.Id, MentorName = mentor.Name, MentorEmail = mentor.Email,
                    ResultingSemesterLoad = loads[mentor.Id]
                });
            }
        }

        var canCommit = warnings.Count == 0 && result.Count > 0;
        var sessionId = Guid.Empty;
        if (canCommit)
        {
            var now = dateTimeProvider.UtcNow;
            sessionId = Guid.NewGuid();
            context.MentorAllocationSessions.Add(new MentorAllocationSession
            {
                Id = sessionId, AdminUserId = adminId, SemesterId = request.SemesterId,
                ClassIdsJson = JsonSerializer.Serialize(classIds, JsonOptions), RowsJson = JsonSerializer.Serialize(result, JsonOptions), Seed = seed,
                CreatedAtUtc = now, ExpiresAtUtc = now.Add(SessionLifetime)
            });
            await context.SaveChangesAsync(cancellationToken);
        }
        return Result.Success(new MentorAllocationPreviewResponse
        {
            SessionId = sessionId, SemesterId = request.SemesterId, Seed = seed, TeamCount = teams.Count,
            MissingEnterpriseCount = result.Count(item => item.MentorType == MentorType.Enterprise.ToString()),
            MissingAcademicCount = result.Count(item => item.MentorType == MentorType.Academic.ToString()),
            CanCommit = canCommit, Warnings = warnings, Assignments = result
        });
    }

    public async Task<Result<MentorAllocationCommitResponse>> CommitAllocationAsync(CommitMentorAllocationRequest request, CancellationToken cancellationToken = default)
    {
        if (!TryGetAdminId(out var adminId)) return Failure<MentorAllocationCommitResponse>(ErrorCodes.CommonUnauthorizedError, "An authenticated administrator is required.");
        var lease = await AcquireAllocationSessionAsync(request.SessionId, adminId, cancellationToken);
        if (lease.IsFailure) return Result.Failure<MentorAllocationCommitResponse>(lease.Error);
        var session = lease.Value;
        MentorAllocationRowPreview[] rows;
        try { rows = JsonSerializer.Deserialize<MentorAllocationRowPreview[]>(session.RowsJson, JsonOptions) ?? []; }
        catch (JsonException) { return await ReleaseAllocationFailure(session, ErrorCodes.MentorAllocationSessionInvalid, "The allocation session is damaged. Generate a new preview.", cancellationToken); }
        if (rows.Length == 0)
            return await ReleaseAllocationFailure(session, ErrorCodes.MentorAllocationSessionInvalid, "The allocation session contains no assignments. Generate a new preview.", cancellationToken);
        try
        {
            var response = await unitOfWork.ExecuteInSerializableTransactionAsync(async token =>
            {
                var teamIds = rows.Select(item => item.TeamId).Distinct().ToArray();
                var mentorIds = rows.Select(item => item.MentorProfileId).Distinct().ToArray();
                var teams = await context.Teams.Include(item => item.Class).Where(item => teamIds.Contains(item.Id)).ToListAsync(token);
                var mentors = await context.MentorProfiles.Include(item => item.User).Where(item => mentorIds.Contains(item.Id)).ToListAsync(token);
                var active = await context.MentorAssignments
                    .Where(item => item.Team.Class.SemesterId == session.SemesterId &&
                                   item.Status == MentorAssignmentStatus.Active && item.EndedAt == null)
                    .ToListAsync(token);
                var staffUserIds = await context.SemesterStaffAssignments.AsNoTracking()
                    .Where(item => item.SemesterId == session.SemesterId && item.Role == SemesterStaffRole.Mentor && item.Status == SemesterStaffStatus.Active)
                    .Select(item => item.UserId).ToListAsync(token);

                foreach (var mentorRows in rows.GroupBy(item => item.MentorProfileId))
                {
                    var expectedLoadBeforeCommit = mentorRows.Max(item => item.ResultingSemesterLoad) - mentorRows.Count();
                    var currentLoad = active.Count(item => item.MentorProfileId == mentorRows.Key);
                    if (currentLoad != expectedLoadBeforeCommit)
                        throw new MentorAdminConflictException("Mentor loads changed after the allocation preview was generated.");
                }

                foreach (var row in rows)
                {
                    var type = Enum.Parse<MentorType>(row.MentorType, true);
                    var team = teams.FirstOrDefault(item => item.Id == row.TeamId);
                    var mentor = mentors.FirstOrDefault(item => item.Id == row.MentorProfileId);
                    if (team is null || team.Status != TeamStatus.Active || team.Class.SemesterId != session.SemesterId ||
                        mentor is null || mentor.Type != type || mentor.Status != MentorProfileStatus.Active || mentor.User.Status != UserStatus.Active ||
                        !staffUserIds.Contains(mentor.UserId) || active.Any(item => item.TeamId == row.TeamId && item.Slot == type))
                        throw new MentorAdminConflictException("The allocation preview is stale.");
                }
                var now = dateTimeProvider.UtcNow;
                foreach (var row in rows)
                {
                    var type = Enum.Parse<MentorType>(row.MentorType, true);
                    context.MentorAssignments.Add(new MentorAssignment
                    {
                        TeamId = row.TeamId, MentorProfileId = row.MentorProfileId, AssignedById = adminId,
                        AssignedAt = now, Status = MentorAssignmentStatus.Active, Slot = type,
                        Note = "Balanced semester allocation", CreatedBy = adminId
                    });
                }
                foreach (var group in rows.GroupBy(item => item.ClassId))
                {
                    context.ClassAuditLogs.Add(new ClassAuditLog
                    {
                        ClassId = group.Key, Action = "MENTORS_BALANCED_ASSIGNED", PerformedByUserId = adminId,
                        OccurredAtUtc = now, DetailsJson = JsonSerializer.Serialize(new { session.Id, session.Seed, AssignmentCount = group.Count() })
                    });
                    ClassOutbox.Enqueue(context, "Team.MentorAssignmentChanged.v1", group.Key,
                        new { AllocationSessionId = session.Id, AssignmentCount = group.Count(), Action = "BalancedAssigned" }, now);
                }
                session.Status = MentorAdminSessionStatus.Consumed;
                session.ConsumedAtUtc = now;
                session.ProcessingStartedAtUtc = null;
                await context.SaveChangesAsync(token);
                return Result.Success(new MentorAllocationCommitResponse { CreatedCount = rows.Length, SkippedCount = 0 });
            }, cancellationToken);
            return response;
        }
        catch (MentorAdminConflictException)
        {
            await ResetAllocationSessionAsync(session.Id, cancellationToken);
            return Failure<MentorAllocationCommitResponse>(ErrorCodes.MentorAllocationConflict, "Teams or mentors changed after preview. Generate a new allocation preview.");
        }
        catch (DbUpdateException)
        {
            await ResetAllocationSessionAsync(session.Id, cancellationToken);
            return Failure<MentorAllocationCommitResponse>(ErrorCodes.MentorAllocationConflict, "The allocation changed concurrently. Generate a new preview.");
        }
        catch (SerializableTransactionConflictException)
        {
            await ResetAllocationSessionAsync(session.Id, cancellationToken);
            return Failure<MentorAllocationCommitResponse>(ErrorCodes.MentorAllocationConflict, "The allocation changed concurrently. Generate a new preview.");
        }
        catch
        {
            await ResetAllocationSessionAsync(session.Id, cancellationToken);
            throw;
        }
    }

    private async Task ValidateImportRowsAsync(Guid semesterId, IReadOnlyCollection<MentorImportCandidate> rows, CancellationToken cancellationToken)
    {
        var emails = rows.Where(item => item.IsValid).Select(item => item.Email).Distinct().ToArray();
        var users = await context.Users.IgnoreQueryFilters().AsNoTracking().Include(item => item.UserRoles).ThenInclude(item => item.Role)
            .Include(item => item.MentorProfile).Where(item => emails.Contains(item.NormalizedEmail)).ToListAsync(cancellationToken);
        var staffIds = await context.SemesterStaffAssignments.AsNoTracking()
            .Where(item => item.SemesterId == semesterId && item.Role == SemesterStaffRole.Mentor && item.Status == SemesterStaffStatus.Active)
            .Select(item => item.UserId).ToListAsync(cancellationToken);
        var byEmail = users.ToDictionary(item => item.NormalizedEmail, StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows.Where(item => item.IsValid))
        {
            if (!byEmail.TryGetValue(row.Email, out var user)) { row.Status = "Create"; row.Message = "A new Mentor account will be created and added to this semester."; continue; }
            var isMentor = user.UserRoles.Any(item => item.Role.Name == SystemRoles.Mentor);
            var isLecturer = user.UserRoles.Any(item => item.Role.Name == SystemRoles.Lecturer);
            if (user.IsDeleted) row.MarkInvalid("A deleted account already uses this login email.");
            else if (!isMentor || user.MentorProfile is null) row.MarkInvalid("This login email belongs to an account that is not a Mentor.");
            else if (row.MentorType == MentorType.Academic && isLecturer) row.MarkInvalid("Academic mentors must use a Mentor-only account, not a Lecturer account.");
            else if (user.MentorProfile.Type != row.MentorType) row.MarkInvalid($"The existing Mentor is {user.MentorProfile.Type}, but this row is {row.MentorType}.");
            else if (user.Status is UserStatus.Blocked or UserStatus.Rejected or UserStatus.Inactive) row.MarkInvalid($"The existing Mentor account is {user.Status} and cannot be imported.");
            else if (staffIds.Contains(user.Id)) { row.Status = "Update"; row.Message = "The existing Mentor profile will be updated for this semester."; }
            else { row.Status = "AddToSemester"; row.Message = "The existing Mentor will be updated and added to this semester."; }
        }
    }

    private static MentorProfile NewProfile(User user, MentorImportCandidate row, Guid adminId)
    {
        var profile = new MentorProfile { UserId = user.Id, User = user, Type = row.MentorType, Status = MentorProfileStatus.Active, CreatedBy = adminId };
        ApplyProfile(profile, row, adminId);
        return profile;
    }
    private static void ApplyProfile(MentorProfile profile, MentorImportCandidate row, Guid adminId)
    {
        profile.Type = row.MentorType; profile.DateOfBirth = row.DateOfBirth; profile.ContractType = row.ContractType;
        profile.EducationLevel = row.EducationLevel; profile.CurrentAddress = row.CurrentAddress; profile.FptEmail = row.FptEmail;
        profile.Organization = row.Organization; profile.Department = row.Department; profile.JobTitle = row.JobTitle;
        profile.Status = MentorProfileStatus.Active; profile.UpdatedBy = adminId;
    }
    private static void CancelPendingRegistration(
        string email,
        Guid completedUserId,
        IReadOnlyDictionary<string, PendingRegistration> pendingByEmail,
        DateTime now,
        Guid adminId)
    {
        if (!pendingByEmail.TryGetValue(email, out var pending)) return;

        pending.Status = PendingRegistrationStatus.Cancelled;
        pending.CompletedUserId = completedUserId;
        pending.CompletedAtUtc = now;
        pending.UpdatedBy = adminId;
    }
    private static MentorImportRowPreview ToPreview(MentorImportCandidate row) => new()
    {
        RowNumber = row.RowNumber, SheetName = row.SheetName, MentorType = row.MentorType.ToString(), FullName = row.FullName,
        Email = row.Email, FptEmail = row.FptEmail, Phone = row.Phone, DateOfBirth = row.DateOfBirth,
        ContractType = row.ContractType, EducationLevel = row.EducationLevel, CurrentAddress = row.CurrentAddress,
        Organization = row.Organization, Department = row.Department, JobTitle = row.JobTitle,
        Status = row.Status, IsValid = row.IsValid, Message = row.Message
    };

    private async Task<Result<MentorImportSession>> AcquireImportSessionAsync(Guid id, Guid adminId, CancellationToken token)
    {
        if (id == Guid.Empty) return Failure<MentorImportSession>(ErrorCodes.MentorImportSessionInvalid, "A valid import session is required.");
        var session = await context.MentorImportSessions.FirstOrDefaultAsync(item => item.Id == id, token);
        if (session is null || session.AdminUserId != adminId) return Failure<MentorImportSession>(ErrorCodes.MentorImportSessionInvalid, "The import session is invalid or belongs to another administrator.");
        var now = dateTimeProvider.UtcNow;
        if (session.ExpiresAtUtc <= now) return Failure<MentorImportSession>(ErrorCodes.MentorImportSessionExpired, "The import session has expired.");
        if (session.Status == MentorAdminSessionStatus.Consumed) return Failure<MentorImportSession>(ErrorCodes.MentorImportSessionInvalid, "The import session has already been used.");
        if (session.Status == MentorAdminSessionStatus.Processing && session.ProcessingStartedAtUtc?.Add(ProcessingLease) > now)
            return Failure<MentorImportSession>(ErrorCodes.MentorImportSessionAlreadyProcessing, "The import session is already being processed.");
        session.Status = MentorAdminSessionStatus.Processing; session.ProcessingStartedAtUtc = now;
        try { await context.SaveChangesAsync(token); return Result.Success(session); }
        catch (DbUpdateConcurrencyException) { context.ClearChanges(); return Failure<MentorImportSession>(ErrorCodes.MentorImportSessionAlreadyProcessing, "The import session is already being processed."); }
    }
    private async Task<Result<MentorAllocationSession>> AcquireAllocationSessionAsync(Guid id, Guid adminId, CancellationToken token)
    {
        if (id == Guid.Empty) return Failure<MentorAllocationSession>(ErrorCodes.MentorAllocationSessionInvalid, "A valid allocation session is required.");
        var session = await context.MentorAllocationSessions.FirstOrDefaultAsync(item => item.Id == id, token);
        if (session is null || session.AdminUserId != adminId) return Failure<MentorAllocationSession>(ErrorCodes.MentorAllocationSessionInvalid, "The allocation session is invalid or belongs to another administrator.");
        var now = dateTimeProvider.UtcNow;
        if (session.ExpiresAtUtc <= now) return Failure<MentorAllocationSession>(ErrorCodes.MentorAllocationSessionExpired, "The allocation session has expired.");
        if (session.Status == MentorAdminSessionStatus.Consumed) return Failure<MentorAllocationSession>(ErrorCodes.MentorAllocationSessionInvalid, "The allocation session has already been used.");
        if (session.Status == MentorAdminSessionStatus.Processing && session.ProcessingStartedAtUtc?.Add(ProcessingLease) > now)
            return Failure<MentorAllocationSession>(ErrorCodes.MentorAllocationSessionAlreadyProcessing, "The allocation session is already being processed.");
        session.Status = MentorAdminSessionStatus.Processing; session.ProcessingStartedAtUtc = now;
        try { await context.SaveChangesAsync(token); return Result.Success(session); }
        catch (DbUpdateConcurrencyException) { context.ClearChanges(); return Failure<MentorAllocationSession>(ErrorCodes.MentorAllocationSessionAlreadyProcessing, "The allocation session is already being processed."); }
    }
    private async Task<Result<MentorImportCommitResponse>> ReleaseImportFailure(MentorImportSession session, string code, string message, CancellationToken token)
    { session.Status = MentorAdminSessionStatus.Available; session.ProcessingStartedAtUtc = null; await context.SaveChangesAsync(token); return Failure<MentorImportCommitResponse>(code, message); }
    private async Task<Result<MentorAllocationCommitResponse>> ReleaseAllocationFailure(MentorAllocationSession session, string code, string message, CancellationToken token)
    { session.Status = MentorAdminSessionStatus.Available; session.ProcessingStartedAtUtc = null; await context.SaveChangesAsync(token); return Failure<MentorAllocationCommitResponse>(code, message); }
    private async Task ResetImportSessionAsync(Guid id, CancellationToken token)
    { context.ClearChanges(); var session = await context.MentorImportSessions.FirstOrDefaultAsync(item => item.Id == id, token); if (session?.Status == MentorAdminSessionStatus.Processing) { session.Status = MentorAdminSessionStatus.Available; session.ProcessingStartedAtUtc = null; await context.SaveChangesAsync(token); } }
    private async Task ResetAllocationSessionAsync(Guid id, CancellationToken token)
    { context.ClearChanges(); var session = await context.MentorAllocationSessions.FirstOrDefaultAsync(item => item.Id == id, token); if (session?.Status == MentorAdminSessionStatus.Processing) { session.Status = MentorAdminSessionStatus.Available; session.ProcessingStartedAtUtc = null; await context.SaveChangesAsync(token); } }
    private bool TryGetAdminId(out Guid id)
    { id = currentUser.UserId ?? Guid.Empty; return currentUser.IsAuthenticated && id != Guid.Empty && currentUser.Roles.Any(role => role.Equals(SystemRoles.Admin, StringComparison.OrdinalIgnoreCase)); }
    private static Result<T> Failure<T>(string code, string message) => Result.Failure<T>(new Error(code, message));
    private sealed record AllocationTeam(Guid Id, Guid ClassId, string Code, string Name);
    private sealed record AllocationMentor(Guid Id, string Name, string Email, MentorType Type);
    private sealed class MentorAdminConflictException(string message) : Exception(message);
}
