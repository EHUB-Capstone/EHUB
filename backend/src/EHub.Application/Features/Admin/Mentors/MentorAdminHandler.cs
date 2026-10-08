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

    public async Task<Result<IReadOnlyCollection<IncompleteMentorResponse>>> GetIncompleteMasterMentorsAsync(CancellationToken cancellationToken = default)
    {
        if (!TryGetAdminId(out _)) return Failure<IReadOnlyCollection<IncompleteMentorResponse>>(ErrorCodes.CommonUnauthorizedError, "An authenticated administrator is required.");
        var drafts = await context.MentorImportDrafts.AsNoTracking()
            .Where(item => item.SemesterId == null && item.Status == MentorImportDraftStatus.NeedsCompletion)
            .OrderBy(item => item.FullName).ToListAsync(cancellationToken);
        IReadOnlyCollection<IncompleteMentorResponse> response = drafts.Select(item => new IncompleteMentorResponse
        {
            Id = item.Id,
            FullName = item.FullName,
            MentorType = item.Type.ToString(),
            Email = item.Email,
            MissingFields = MentorDraftFields.GetMissing(item),
            UpdatedAtUtc = item.UpdatedAt ?? item.CreatedAt
        }).ToArray();
        return Result.Success(response);
    }

    // A null semesterId previews an import into the master mentor list (accounts and profiles only, no semester).
    public async Task<Result<MentorImportPreviewResponse>> PreviewImportAsync(Guid? semesterId, IFormFile file, CancellationToken cancellationToken = default)
    {
        if (!TryGetAdminId(out var adminId)) return Failure<MentorImportPreviewResponse>(ErrorCodes.CommonUnauthorizedError, "An authenticated administrator is required.");
        if (semesterId is { } requestedSemesterId &&
            (requestedSemesterId == Guid.Empty || !await context.Semesters.AsNoTracking().AnyAsync(item => item.Id == requestedSemesterId, cancellationToken)))
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
        var actionable = rows.Count(IsActionable);
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
            AddToSemesterCount = rows.Count(item => item.WillAddToSemester),
            NeedsCompletionCount = rows.Count(item => item.WillSaveDraft),
            CompleteDraftCount = rows.Count(item => item.WillCompleteDraft),
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
            var response = await unitOfWork.ExecuteInSerializableTransactionAsync(async token =>
            {
                await ValidateImportRowsAsync(session.SemesterId, rows, token);
                if (rows.Any(item => !item.IsValid)) throw new MentorAdminConflictException("Mentor accounts changed after preview.");
                var role = rows.Any(item => !string.IsNullOrWhiteSpace(item.Email))
                    ? await context.Roles.FirstOrDefaultAsync(item => item.Name == SystemRoles.Mentor, token)
                        ?? throw new InvalidOperationException("The Mentor role has not been seeded.")
                    : null;
                var emails = rows.Where(item => !string.IsNullOrWhiteSpace(item.Email)).Select(item => item.Email).Distinct().ToArray();
                var users = await context.Users.Include(item => item.UserRoles).ThenInclude(item => item.Role)
                    .Include(item => item.MentorProfile)
                    .Where(item => emails.Contains(item.NormalizedEmail)).ToListAsync(token);
                var byEmail = users.ToDictionary(item => item.NormalizedEmail, StringComparer.OrdinalIgnoreCase);
                var pendingRegistrations = await context.PendingRegistrations
                    .Where(item => emails.Contains(item.NormalizedEmail) && item.Status == PendingRegistrationStatus.Pending)
                    .ToListAsync(token);
                var pendingByEmail = pendingRegistrations
                    .ToDictionary(item => item.NormalizedEmail, StringComparer.OrdinalIgnoreCase);
                var semesterStaff = session.SemesterId is { } staffSemesterId
                    ? await context.SemesterStaffAssignments
                        .Where(item => item.SemesterId == staffSemesterId && item.Role == SemesterStaffRole.Mentor &&
                                       users.Select(user => user.Id).Contains(item.UserId)).ToListAsync(token)
                    : [];
                var created = 0;
                var updated = 0;
                var assigned = 0;
                var draftsSaved = 0;
                var draftsCompleted = 0;
                var now = dateTimeProvider.UtcNow;
                foreach (var row in rows)
                {
                    if (row.WillSaveDraft)
                    {
                        MentorImportDraft draft;
                        if (row.DraftId is { } existingDraftId)
                        {
                            draft = await context.MentorImportDrafts.FirstOrDefaultAsync(item => item.Id == existingDraftId &&
                                (item.SemesterId == session.SemesterId || item.SemesterId == null) && item.Status == MentorImportDraftStatus.NeedsCompletion, token)
                                ?? throw new MentorAdminConflictException("An incomplete mentor changed after preview.");
                        }
                        else
                        {
                            draft = new MentorImportDraft
                            {
                                SemesterId = session.SemesterId,
                                Type = row.MentorType,
                                FullName = row.FullName,
                                NormalizedFullName = row.NormalizedFullName,
                                CreatedBy = adminId
                            };
                            context.MentorImportDrafts.Add(draft);
                        }
                        ApplyDraft(draft, row, adminId);
                        draftsSaved++;
                        continue;
                    }

                    MentorImportDraft? completingDraft = null;
                    if (row.DraftId is { } draftId)
                    {
                        completingDraft = await context.MentorImportDrafts.FirstOrDefaultAsync(item => item.Id == draftId &&
                            (item.SemesterId == session.SemesterId || item.SemesterId == null) && item.Status == MentorImportDraftStatus.NeedsCompletion, token)
                            ?? throw new MentorAdminConflictException("An incomplete mentor changed after preview.");
                        ApplyDraft(completingDraft, row, adminId);
                        ApplyDraftFallback(row, completingDraft);
                    }

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
                        user.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = role!.Id, AssignedAt = now, AssignedBy = adminId, Role = role });
                        user.MentorProfile = NewProfile(user, row, adminId);
                        context.Users.Add(user);
                        byEmail[row.Email] = user;
                        created++;
                    }
                    else
                    {
                        user.FullName = row.FullName;
                        if (row.MentorType == MentorType.Enterprise && row.PresentColumns.Contains("phone") && !string.IsNullOrWhiteSpace(row.Phone))
                            user.Phone = row.Phone;
                        user.UpdatedBy = adminId;
                        if (user.Status == UserStatus.PendingApproval) user.Status = UserStatus.Active;
                        ApplyProfile(user.MentorProfile!, row, adminId);
                        updated++;
                    }

                    if (completingDraft is not null)
                    {
                        completingDraft.Status = MentorImportDraftStatus.Converted;
                        completingDraft.ConvertedMentorProfile = user.MentorProfile;
                        completingDraft.ConvertedMentorProfileId = user.MentorProfile!.Id;
                        completingDraft.ConvertedAtUtc = now;
                        completingDraft.UpdatedBy = adminId;
                        draftsCompleted++;
                    }

                    if (session.SemesterId is { } targetSemesterId)
                    {
                        var staff = semesterStaff.FirstOrDefault(item => item.UserId == user.Id);
                        if (staff is null)
                        {
                            staff = new SemesterStaffAssignment
                            {
                                SemesterId = targetSemesterId,
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
                    }

                    CancelPendingRegistration(row.Email, user.Id, pendingByEmail, now, adminId);
                }
                session.Status = MentorAdminSessionStatus.Consumed;
                session.ConsumedAtUtc = now;
                session.ProcessingStartedAtUtc = null;
                await context.SaveChangesAsync(token);
                return new MentorImportCommitResponse
                {
                    CreatedCount = created,
                    UpdatedCount = updated,
                    SemesterAssignmentCount = assigned,
                    DraftSavedCount = draftsSaved,
                    DraftCompletedCount = draftsCompleted
                };
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
        catch (SerializableTransactionConflictException)
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
        if (!TryParseStrategy(request.Strategy, out var strategy))
            return Failure<MentorAllocationPreviewResponse>(ErrorCodes.MentorAllocationInvalid, $"The allocation strategy must be {MentorAllocationStrategies.Balanced} or {MentorAllocationStrategies.Random}.");
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
            .Select(item => new { item.Id, item.TeamId, item.MentorProfileId, MentorName = item.MentorProfile.User.FullName, item.Slot, SubjectCode = item.Team.Class.Course.Code }).ToListAsync(cancellationToken);
        var teamIdsInScope = teams.Select(item => item.Id).ToArray();
        var subjectByTeam = await context.Teams.AsNoTracking().Where(item => teamIdsInScope.Contains(item.Id))
            .Select(item => new { item.Id, SubjectCode = item.Class.Course.Code }).ToDictionaryAsync(item => item.Id, item => item.SubjectCode, cancellationToken);
        var mentorIdsInPool = mentors.Select(item => item.Id).ToArray();
        var contractByMentor = await context.MentorProfiles.AsNoTracking().Where(item => mentorIdsInPool.Contains(item.Id))
            .Select(item => new { item.Id, item.ContractType }).ToDictionaryAsync(item => item.Id, item => item.ContractType, cancellationToken);

        var edits = new List<ManualEdit>();
        foreach (var edit in request.Edits)
        {
            if (!Enum.TryParse<MentorType>(edit.MentorType, true, out var editSlot) || !Enum.IsDefined(editSlot))
                return Failure<MentorAllocationPreviewResponse>(ErrorCodes.MentorAllocationInvalid, "Every edit must name the mentor type Enterprise or Academic.");
            edits.Add(new ManualEdit(edit.TeamId, editSlot, edit.MentorProfileId, edit.Replace, edit.Reason));
        }
        var manual = MentorManualEditPlanner.Plan(
            edits, teams, mentors,
            active.Select(item => new ExistingSlot(item.Id, item.TeamId, item.Slot, item.MentorProfileId, item.MentorName)).ToArray());

        var seed = request.Seed ?? RandomNumberGenerator.GetInt32(int.MaxValue);
        var replacedIds = manual.Assignments.Where(item => item.Replaces is not null).Select(item => item.Replaces!.AssignmentId).ToHashSet();
        var remaining = active.Where(item => !replacedIds.Contains(item.Id))
            .Select(item => new AllocationExistingAssignment(item.TeamId, item.MentorProfileId, item.Slot)).ToArray();
        var manualSlots = manual.Assignments.Select(item => new AllocationExistingAssignment(item.Team.Id, item.Mentor.Id, item.Slot)).ToArray();
        var excluded = manual.Exclusions.Select(item => (item.TeamId, item.Slot)).ToHashSet();

        // Order of precedence: hand edits, then the previous semester's mentors for continuing teams, then the engine
        // fills what is still empty. Each step counts what the earlier steps already placed in the mentor's load.
        var retention = MentorRetentionPlanner.Plan(await MentorRetentionDataLoader.LoadAsync(
            context, request.SemesterId, teams.Select(item => item.Id).ToArray(), remaining.Concat(manualSlots).ToArray(), cancellationToken));
        var retainedKept = retention.Retained.Where(item => !excluded.Contains((item.Team.Id, item.Slot))).ToArray();
        var engineExisting = remaining.Concat(manualSlots)
            .Concat(retainedKept.Select(item => new AllocationExistingAssignment(item.Team.Id, item.Mentor.Id, item.Slot)))
            .Concat(excluded.Select(item => new AllocationExistingAssignment(item.TeamId, Guid.Empty, item.Slot)))
            .ToArray();
        var outcome = MentorAllocationEngine.Allocate(teams, mentors, engineExisting, seed, strategy);
        var warnings = outcome.Warnings;

        var loadBefore = active.GroupBy(item => item.MentorProfileId).ToDictionary(group => group.Key, group => group.Count());
        var runningLoads = remaining.GroupBy(item => item.MentorProfileId).ToDictionary(group => group.Key, group => group.Count());
        int NextLoad(Guid mentorId) => runningLoads[mentorId] = runningLoads.GetValueOrDefault(mentorId) + 1;
        string ClassCodeOf(Guid classId) => classes.First(item => item.Id == classId).ClassCode;

        var result = new List<MentorAllocationRowPreview>();
        foreach (var edit in manual.Assignments)
        {
            result.Add(new MentorAllocationRowPreview
            {
                TeamId = edit.Team.Id, TeamCode = edit.Team.Code, TeamName = edit.Team.Name, ClassId = edit.Team.ClassId, ClassCode = ClassCodeOf(edit.Team.ClassId),
                MentorType = edit.Slot.ToString(), MentorProfileId = edit.Mentor.Id, MentorName = edit.Mentor.Name, MentorEmail = edit.Mentor.Email,
                ResultingSemesterLoad = NextLoad(edit.Mentor.Id), Source = MentorAllocationSources.Manual,
                ReplacesAssignmentId = edit.Replaces?.AssignmentId, ReplacesMentorProfileId = edit.Replaces?.MentorProfileId,
                ReplacesMentorName = edit.Replaces?.MentorName, ReplaceReason = edit.Reason,
                MentorLoadBefore = loadBefore.GetValueOrDefault(edit.Mentor.Id)
            });
        }
        foreach (var retained in retainedKept)
        {
            result.Add(new MentorAllocationRowPreview
            {
                TeamId = retained.Team.Id, TeamCode = retained.Team.Code, TeamName = retained.Team.Name,
                ClassId = teams.First(item => item.Id == retained.Team.Id).ClassId, ClassCode = retained.Team.ClassCode,
                MentorType = retained.Slot.ToString(), MentorProfileId = retained.Mentor.Id,
                MentorName = retained.Mentor.Name, MentorEmail = retained.Mentor.Email,
                ResultingSemesterLoad = NextLoad(retained.Mentor.Id), Source = MentorAllocationSources.Retained,
                MentorLoadBefore = loadBefore.GetValueOrDefault(retained.Mentor.Id)
            });
        }
        result.AddRange(outcome.Proposals.Select(proposal => new MentorAllocationRowPreview
        {
            TeamId = proposal.Team.Id, TeamCode = proposal.Team.Code, TeamName = proposal.Team.Name, ClassId = proposal.Team.ClassId,
            ClassCode = ClassCodeOf(proposal.Team.ClassId),
            MentorType = proposal.Mentor.Type.ToString(), MentorProfileId = proposal.Mentor.Id,
            MentorName = proposal.Mentor.Name, MentorEmail = proposal.Mentor.Email,
            ResultingSemesterLoad = proposal.ResultingLoad, Source = MentorAllocationSources.Allocated,
            MentorLoadBefore = loadBefore.GetValueOrDefault(proposal.Mentor.Id)
        }));
        var skipped = retention.Skipped.Select(ToSkippedPreview).ToList();
        var teamsInScope = teams.ToDictionary(item => item.Id);
        var existingAssignments = active
            .Where(item => teamsInScope.ContainsKey(item.TeamId))
            .OrderBy(item => teamsInScope[item.TeamId].Code, StringComparer.Ordinal).ThenBy(item => item.Slot)
            .Select(item => new MentorAllocationExistingPreview
            {
                AssignmentId = item.Id, TeamId = item.TeamId, TeamCode = teamsInScope[item.TeamId].Code, TeamName = teamsInScope[item.TeamId].Name,
                ClassId = teamsInScope[item.TeamId].ClassId, ClassCode = ClassCodeOf(teamsInScope[item.TeamId].ClassId), SubjectCode = item.SubjectCode,
                MentorType = item.Slot.ToString(), MentorProfileId = item.MentorProfileId, MentorName = item.MentorName,
                Replaced = replacedIds.Contains(item.Id)
            }).ToList();
        var conflicts = manual.Conflicts.Select(item => ToConflictPreview(item, active.FirstOrDefault(slot => item.Team != null && slot.TeamId == item.Team.Id && slot.Slot == item.Slot)?.Id)).ToList();

        // Slots that still have no mentor once everything proposed is applied (for example no mentor of that type is active).
        var covered = remaining.Select(item => (item.TeamId, item.Slot))
            .Concat(result.Select(row => (row.TeamId, Slot: Enum.Parse<MentorType>(row.MentorType, true))))
            .ToHashSet();
        var unfilled = new List<MentorAllocationUnfilledPreview>();
        foreach (var slot in new[] { MentorType.Enterprise, MentorType.Academic })
        {
            foreach (var team in teams.Where(item => !covered.Contains((item.Id, slot))))
            {
                unfilled.Add(new MentorAllocationUnfilledPreview
                {
                    TeamId = team.Id, TeamCode = team.Code, TeamName = team.Name, ClassId = team.ClassId,
                    ClassCode = ClassCodeOf(team.ClassId),
                    SubjectCode = subjectByTeam.GetValueOrDefault(team.Id, string.Empty), MentorType = slot.ToString()
                });
            }
        }

        var mentorLoads = MentorLoadSummaryBuilder.Build(
            mentors.Select(item => new LoadMentor(item.Id, item.Name, item.Email, item.Type, contractByMentor.GetValueOrDefault(item.Id))).ToArray(),
            active.Select(item => new LoadAssignment(item.MentorProfileId, item.SubjectCode)).ToArray(),
            result.Select(row => new LoadAssignment(row.MentorProfileId, subjectByTeam.GetValueOrDefault(row.TeamId, string.Empty))).ToArray(),
            active.Where(item => replacedIds.Contains(item.Id)).Select(item => new LoadAssignment(item.MentorProfileId, item.SubjectCode)).ToArray())
            .Select(item => new MentorAllocationMentorLoad
            {
                MentorProfileId = item.Mentor.Id, MentorName = item.Mentor.Name, MentorEmail = item.Mentor.Email,
                MentorType = item.Mentor.Type.ToString(), ContractType = item.Mentor.ContractType,
                Subjects = item.Subjects.Select(subject => new MentorAllocationSubjectLoad { SubjectCode = subject.SubjectCode, Before = subject.Before, Added = subject.Added, Removed = subject.Removed }).ToArray(),
                TotalBefore = item.TotalBefore, TotalAfter = item.TotalAfter
            }).ToList();

        // A missing mentor type no longer blocks saving: what can be assigned is saved and the rest is reported as unfilled.
        var canCommit = result.Count > 0;
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
            RetainedCount = retainedKept.Length, Strategy = strategy.ToString(), ReplacementCount = replacedIds.Count,
            UnfilledEnterpriseCount = unfilled.Count(item => item.MentorType == nameof(MentorType.Enterprise)),
            UnfilledAcademicCount = unfilled.Count(item => item.MentorType == nameof(MentorType.Academic)),
            CanCommit = canCommit, Warnings = warnings, Assignments = result, Skipped = skipped,
            Unfilled = unfilled, MentorLoads = mentorLoads, Conflicts = conflicts, ExistingAssignments = existingAssignments
        });
    }

    private static bool TryParseStrategy(string? value, out AllocationStrategy strategy)
    {
        strategy = AllocationStrategy.Balanced;
        if (string.IsNullOrWhiteSpace(value) || string.Equals(value.Trim(), MentorAllocationStrategies.Balanced, StringComparison.OrdinalIgnoreCase)) return true;
        if (!string.Equals(value.Trim(), MentorAllocationStrategies.Random, StringComparison.OrdinalIgnoreCase)) return false;
        strategy = AllocationStrategy.Random;
        return true;
    }

    private static MentorAllocationConflictPreview ToConflictPreview(ManualConflict conflict, Guid? currentAssignmentId) => new()
    {
        TeamId = conflict.Team?.Id, TeamCode = conflict.Team?.Code ?? string.Empty, TeamName = conflict.Team?.Name ?? string.Empty,
        MentorType = conflict.Slot.ToString(),
        CurrentAssignmentId = conflict.Current?.AssignmentId ?? currentAssignmentId, CurrentMentorProfileId = conflict.Current?.MentorProfileId,
        CurrentMentorName = conflict.Current?.MentorName,
        ProposedMentorProfileId = conflict.Proposed?.Id, ProposedMentorName = conflict.Proposed?.Name,
        Kind = conflict.Kind.ToString(), Message = conflict.Message
    };

    private static MentorAllocationSkippedPreview ToSkippedPreview(RetentionSkip skip) => new()
    {
        TeamId = skip.Team?.Id, TeamCode = skip.Team?.Code ?? string.Empty, TeamName = skip.Team?.Name ?? string.Empty,
        ClassCode = skip.Team?.ClassCode ?? string.Empty, SourceTeamCode = skip.SourceTeamCode,
        MentorType = skip.Slot.ToString(), MentorProfileId = skip.MentorProfileId,
        MentorName = skip.MentorName, MentorEmail = skip.MentorEmail,
        Reason = skip.Reason.ToString(),
        Message = skip.Reason switch
        {
            MentorRetentionSkipReason.MentorNotActiveInSemester => "The previous mentor is not active in this semester. Assign a new mentor.",
            MentorRetentionSkipReason.MentorUnavailable => "The previous mentor's profile or account is no longer available.",
            MentorRetentionSkipReason.SlotAlreadyFilled => "This slot already has a different mentor, so the existing mentor is kept.",
            MentorRetentionSkipReason.NoContinuedTeam => "The previous team has no continued team in this semester, so its mentor is not carried over.",
            _ => string.Empty
        }
    };

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

                // The preview is only valid while the mentors carry the teams they carried when it was generated.
                foreach (var mentorRows in rows.GroupBy(item => item.MentorProfileId))
                {
                    var currentLoad = active.Count(item => item.MentorProfileId == mentorRows.Key);
                    var recordedLoad = mentorRows.First().MentorLoadBefore;
                    var expectedLoadBeforeCommit = recordedLoad ?? mentorRows.Max(item => item.ResultingSemesterLoad) - mentorRows.Count();
                    if (currentLoad != expectedLoadBeforeCommit)
                        throw new MentorAdminConflictException("Mentor loads changed after the allocation preview was generated.");
                }

                var replacements = new Dictionary<MentorAllocationRowPreview, MentorAssignment>();
                foreach (var row in rows)
                {
                    var type = Enum.Parse<MentorType>(row.MentorType, true);
                    var team = teams.FirstOrDefault(item => item.Id == row.TeamId);
                    var mentor = mentors.FirstOrDefault(item => item.Id == row.MentorProfileId);
                    if (team is null || team.Status != TeamStatus.Active || team.Class.SemesterId != session.SemesterId ||
                        mentor is null || mentor.Type != type || mentor.Status != MentorProfileStatus.Active || mentor.User.Status != UserStatus.Active ||
                        !staffUserIds.Contains(mentor.UserId))
                        throw new MentorAdminConflictException("The allocation preview is stale.");

                    var occupant = active.FirstOrDefault(item => item.TeamId == row.TeamId && item.Slot == type);
                    if (row.ReplacesAssignmentId is { } replacedId)
                    {
                        // The mentor being replaced must still be exactly the one the admin saw and chose to replace.
                        if (occupant is null || occupant.Id != replacedId || occupant.MentorProfileId != row.ReplacesMentorProfileId ||
                            string.IsNullOrWhiteSpace(row.ReplaceReason))
                            throw new MentorAdminConflictException("The mentor to replace changed after the allocation preview was generated.");
                        replacements[row] = occupant;
                    }
                    else if (occupant is not null)
                    {
                        throw new MentorAdminConflictException("The allocation preview is stale.");
                    }
                }

                var now = dateTimeProvider.UtcNow;
                // End the replaced assignments first and save, so the unique "one active mentor per slot" rule is never
                // violated. Both steps stay inside this serializable transaction and are rolled back together on failure.
                foreach (var (row, previous) in replacements)
                {
                    var reason = row.ReplaceReason!.Trim();
                    previous.Status = MentorAssignmentStatus.Ended;
                    previous.EndedAt = now;
                    previous.UpdatedBy = adminId;
                    previous.Note = string.IsNullOrWhiteSpace(previous.Note) ? $"Ended: {reason}" : $"{previous.Note}\nEnded: {reason}";
                    context.ClassAuditLogs.Add(new ClassAuditLog
                    {
                        ClassId = row.ClassId, Action = "MENTOR_ASSIGNMENT_ENDED", PerformedByUserId = adminId, OccurredAtUtc = now,
                        DetailsJson = JsonSerializer.Serialize(new { row.TeamId, previous.MentorProfileId, Slot = previous.Slot.ToString(), Reason = reason, AllocationSessionId = session.Id })
                    });
                    ClassOutbox.Enqueue(context, "Team.MentorAssignmentChanged.v1", row.ClassId, new { row.TeamId, Action = "Ended" }, now);
                }
                if (replacements.Count > 0) await context.SaveChangesAsync(token);

                foreach (var row in rows)
                {
                    var type = Enum.Parse<MentorType>(row.MentorType, true);
                    context.MentorAssignments.Add(new MentorAssignment
                    {
                        TeamId = row.TeamId, MentorProfileId = row.MentorProfileId, AssignedById = adminId,
                        AssignedAt = now, Status = MentorAssignmentStatus.Active, Slot = type,
                        Note = row.Source switch
                        {
                            MentorAllocationSources.Retained => "Retained from the previous semester's team",
                            MentorAllocationSources.Manual when row.ReplacesAssignmentId is not null => $"Replaced {row.ReplacesMentorName}: {row.ReplaceReason!.Trim()}",
                            MentorAllocationSources.Manual => "Assigned manually from the allocation preview",
                            _ => "Balanced semester allocation"
                        },
                        CreatedBy = adminId
                    });
                }
                foreach (var group in rows.GroupBy(item => item.ClassId))
                {
                    context.ClassAuditLogs.Add(new ClassAuditLog
                    {
                        ClassId = group.Key, Action = "MENTORS_BALANCED_ASSIGNED", PerformedByUserId = adminId,
                        OccurredAtUtc = now, DetailsJson = JsonSerializer.Serialize(new
                        {
                            session.Id, session.Seed, AssignmentCount = group.Count(),
                            RetainedCount = group.Count(item => item.Source == MentorAllocationSources.Retained),
                            ManualCount = group.Count(item => item.Source == MentorAllocationSources.Manual),
                            ReplacedCount = group.Count(item => item.ReplacesAssignmentId is not null)
                        })
                    });
                    ClassOutbox.Enqueue(context, "Team.MentorAssignmentChanged.v1", group.Key,
                        new { AllocationSessionId = session.Id, AssignmentCount = group.Count(), Action = "BalancedAssigned" }, now);
                }
                session.Status = MentorAdminSessionStatus.Consumed;
                session.ConsumedAtUtc = now;
                session.ProcessingStartedAtUtc = null;
                await context.SaveChangesAsync(token);
                return Result.Success(new MentorAllocationCommitResponse { CreatedCount = rows.Length, SkippedCount = 0, EndedCount = replacements.Count });
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

    private async Task ValidateImportRowsAsync(Guid? semesterId, IReadOnlyCollection<MentorImportCandidate> rows, CancellationToken cancellationToken)
    {
        var isMasterList = semesterId is null;
        foreach (var row in rows.Where(item => item.IsValid)) row.ResetPlannedAction();
        var emails = rows.Where(item => item.IsValid && !string.IsNullOrWhiteSpace(item.Email)).Select(item => item.Email).Distinct().ToArray();
        var users = await context.Users.IgnoreQueryFilters().AsNoTracking().Include(item => item.UserRoles).ThenInclude(item => item.Role)
            .Include(item => item.MentorProfile).Where(item => emails.Contains(item.NormalizedEmail)).ToListAsync(cancellationToken);
        var staffIds = isMasterList
            ? []
            : await context.SemesterStaffAssignments.AsNoTracking()
                .Where(item => item.SemesterId == semesterId && item.Role == SemesterStaffRole.Mentor && item.Status == SemesterStaffStatus.Active)
                .Select(item => item.UserId).ToListAsync(cancellationToken);
        var byEmail = users.ToDictionary(item => item.NormalizedEmail, StringComparer.OrdinalIgnoreCase);
        var normalizedNames = rows.Where(item => item.IsValid).Select(item => item.NormalizedFullName).Distinct().ToArray();
        // A master-list import only sees master drafts. A semester import prefers that semester's own draft and
        // falls back to a master draft of the same name, so an incomplete master mentor can be completed from either place.
        var drafts = await context.MentorImportDrafts.AsNoTracking()
            .Where(item => (item.SemesterId == semesterId || item.SemesterId == null) && item.Status == MentorImportDraftStatus.NeedsCompletion &&
                normalizedNames.Contains(item.NormalizedFullName))
            .ToListAsync(cancellationToken);
        var draftsByKey = drafts.GroupBy(item => DraftMatchKey(item.Type, item.NormalizedFullName))
            .ToDictionary(group => group.Key, group =>
            {
                var own = group.Where(item => item.SemesterId == semesterId).ToArray();
                return own.Length > 0 ? own : group.ToArray();
            }, StringComparer.Ordinal);
        var incomingCounts = rows.Where(item => item.IsValid)
            .GroupBy(item => DraftMatchKey(item.MentorType, item.NormalizedFullName))
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);

        foreach (var row in rows.Where(item => item.IsValid))
        {
            var key = DraftMatchKey(row.MentorType, row.NormalizedFullName);
            var matchingDrafts = draftsByKey.GetValueOrDefault(key) ?? [];
            if (matchingDrafts.Length > 1)
            {
                row.MarkInvalid("More than one incomplete Mentor has this name and type. Resolve the duplicate before importing.");
                continue;
            }
            if (matchingDrafts.Length == 1 && incomingCounts[key] > 1)
            {
                row.MarkInvalid("More than one workbook row could update the same incomplete Mentor. Add unique emails or resolve the duplicate first.");
                continue;
            }
            if (matchingDrafts.Length == 0 && string.IsNullOrWhiteSpace(row.Email) && incomingCounts[key] > 1)
            {
                row.MarkInvalid("Mentors without email must have unique names within the same mentor type.");
                continue;
            }

            row.DraftId = matchingDrafts.SingleOrDefault()?.Id;
            if (string.IsNullOrWhiteSpace(row.Email))
            {
                row.WillSaveDraft = true;
                row.Status = row.DraftId is null ? "NeedsCompletion" : "UpdateIncomplete";
                row.Message = row.DraftId is null
                    ? "The available data will be saved. Add a login email in a later import to activate this Mentor."
                    : "The existing incomplete Mentor will be updated with the available data.";
                if (isMasterList) row.Message += " This Mentor is kept in the master list and is not in any semester yet.";
                continue;
            }

            if (!byEmail.TryGetValue(row.Email, out var user))
            {
                row.WillCreateAccount = true;
                row.WillAddToSemester = !isMasterList;
                SetAccountAction(row, "Create", isMasterList
                    ? "A new Mentor account will be created."
                    : "A new Mentor account will be created and added to this semester.");
                continue;
            }
            var isMentor = user.UserRoles.Any(item => item.Role.Name == SystemRoles.Mentor);
            var isLecturer = user.UserRoles.Any(item => item.Role.Name == SystemRoles.Lecturer);
            if (user.IsDeleted) row.MarkInvalid("A deleted account already uses this login email.");
            else if (!isMentor || user.MentorProfile is null) row.MarkInvalid("This login email belongs to an account that is not a Mentor.");
            else if (row.MentorType == MentorType.Academic && isLecturer) row.MarkInvalid("Academic mentors must use a Mentor-only account, not a Lecturer account.");
            else if (user.MentorProfile.Type != row.MentorType) row.MarkInvalid($"The existing Mentor is {user.MentorProfile.Type}, but this row is {row.MentorType}.");
            else if (user.Status is UserStatus.Blocked or UserStatus.Rejected or UserStatus.Inactive) row.MarkInvalid($"The existing Mentor account is {user.Status} and cannot be imported.");
            else if (isMasterList || staffIds.Contains(user.Id))
            {
                row.WillUpdateAccount = true;
                SetAccountAction(row, "Update", isMasterList
                    ? "The existing Mentor profile will be updated."
                    : "The existing Mentor profile will be updated for this semester.");
            }
            else
            {
                row.WillUpdateAccount = true;
                row.WillAddToSemester = true;
                SetAccountAction(row, "AddToSemester", "The existing Mentor will be updated and added to this semester.");
            }
        }
    }

    private static void SetAccountAction(MentorImportCandidate row, string status, string message)
    {
        if (row.DraftId is null)
        {
            row.Status = status;
            row.Message = message;
            return;
        }
        row.WillCompleteDraft = true;
        row.Status = "CompleteIncomplete";
        row.Message = $"{message} The matching incomplete Mentor record will be completed.";
    }

    private static MentorProfile NewProfile(User user, MentorImportCandidate row, Guid adminId)
    {
        var profile = new MentorProfile { UserId = user.Id, User = user, Type = row.MentorType, Status = MentorProfileStatus.Active, CreatedBy = adminId };
        ApplyProfile(profile, row, adminId, applyAll: true);
        return profile;
    }
    private static void ApplyProfile(MentorProfile profile, MentorImportCandidate row, Guid adminId, bool applyAll = false)
    {
        profile.Type = row.MentorType;
        if ((applyAll || row.PresentColumns.Contains("dateofbirth")) && row.DateOfBirth is not null) profile.DateOfBirth = row.DateOfBirth;
        if ((applyAll || row.PresentColumns.Contains("contracttype")) && row.ContractType is not null) profile.ContractType = row.ContractType;
        if ((applyAll || row.PresentColumns.Contains("educationlevel")) && row.EducationLevel is not null) profile.EducationLevel = row.EducationLevel;
        if ((applyAll || row.PresentColumns.Contains("address")) && row.CurrentAddress is not null) profile.CurrentAddress = row.CurrentAddress;
        if ((applyAll || row.PresentColumns.Contains("fptemail")) && row.FptEmail is not null) profile.FptEmail = row.FptEmail;
        if ((applyAll || row.PresentColumns.Contains("organization")) && row.Organization is not null) profile.Organization = row.Organization;
        if ((applyAll || row.PresentColumns.Contains("department")) && row.Department is not null) profile.Department = row.Department;
        if ((applyAll || row.PresentColumns.Contains("jobtitle")) && row.JobTitle is not null) profile.JobTitle = row.JobTitle;
        profile.Status = MentorProfileStatus.Active; profile.UpdatedBy = adminId;
    }

    private static void ApplyDraft(MentorImportDraft draft, MentorImportCandidate row, Guid adminId)
    {
        draft.FullName = row.FullName;
        draft.NormalizedFullName = row.NormalizedFullName;
        draft.Type = row.MentorType;
        if (!string.IsNullOrWhiteSpace(row.SourceOrdinal)) draft.SourceOrdinal = row.SourceOrdinal;
        if (!string.IsNullOrWhiteSpace(row.Email)) draft.Email = row.Email;
        if (row.PresentColumns.Contains("fptemail") && row.FptEmail is not null) draft.FptEmail = row.FptEmail;
        if (row.PresentColumns.Contains("phone") && row.Phone is not null) draft.Phone = row.Phone;
        if (row.PresentColumns.Contains("dateofbirth") && row.DateOfBirth is not null) draft.DateOfBirth = row.DateOfBirth;
        if (row.PresentColumns.Contains("contracttype") && row.ContractType is not null) draft.ContractType = row.ContractType;
        if (row.PresentColumns.Contains("educationlevel") && row.EducationLevel is not null) draft.EducationLevel = row.EducationLevel;
        if (row.PresentColumns.Contains("address") && row.CurrentAddress is not null) draft.CurrentAddress = row.CurrentAddress;
        if (row.PresentColumns.Contains("organization") && row.Organization is not null) draft.Organization = row.Organization;
        if (row.PresentColumns.Contains("department") && row.Department is not null) draft.Department = row.Department;
        if (row.PresentColumns.Contains("jobtitle") && row.JobTitle is not null) draft.JobTitle = row.JobTitle;
        draft.UpdatedBy = adminId;
    }

    private static void ApplyDraftFallback(MentorImportCandidate row, MentorImportDraft draft)
    {
        row.FptEmail ??= draft.FptEmail;
        row.Phone ??= draft.Phone;
        row.DateOfBirth ??= draft.DateOfBirth;
        row.ContractType ??= draft.ContractType;
        row.EducationLevel ??= draft.EducationLevel;
        row.CurrentAddress ??= draft.CurrentAddress;
        row.Organization ??= draft.Organization;
        row.Department ??= draft.Department;
        row.JobTitle ??= draft.JobTitle;
    }

    private static bool IsActionable(MentorImportCandidate row) => row.IsValid &&
        (row.WillCreateAccount || row.WillUpdateAccount || row.WillAddToSemester || row.WillSaveDraft || row.WillCompleteDraft);

    private static string DraftMatchKey(MentorType type, string normalizedFullName) => $"{type}:{normalizedFullName}";
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
        MissingFields = MissingFields(row),
        Status = row.Status, IsValid = row.IsValid, Message = row.Message
    };

    private static IReadOnlyCollection<string> MissingFields(MentorImportCandidate row)
    {
        var fields = new List<string>();
        if (string.IsNullOrWhiteSpace(row.Email)) fields.Add(row.MentorType == MentorType.Academic ? "Email công việc" : "Email");
        if (row.MentorType == MentorType.Enterprise)
        {
            if (row.DateOfBirth is null) fields.Add("Ngày tháng năm sinh");
            if (string.IsNullOrWhiteSpace(row.Phone)) fields.Add("SDT");
            if (string.IsNullOrWhiteSpace(row.ContractType)) fields.Add("Loại HĐ");
            if (string.IsNullOrWhiteSpace(row.EducationLevel)) fields.Add("Trình độ học vấn");
            if (string.IsNullOrWhiteSpace(row.CurrentAddress)) fields.Add("Địa chỉ hiện nay");
            if (string.IsNullOrWhiteSpace(row.JobTitle)) fields.Add("Vị trí, Chức danh");
            if (string.IsNullOrWhiteSpace(row.Organization)) fields.Add("Công ty");
        }
        else
        {
            if (string.IsNullOrWhiteSpace(row.Department)) fields.Add("Phòng ban trực tiếp");
            if (string.IsNullOrWhiteSpace(row.JobTitle)) fields.Add("Chức danh (VN)");
        }
        return fields;
    }

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
    private sealed class MentorAdminConflictException(string message) : Exception(message);
}
