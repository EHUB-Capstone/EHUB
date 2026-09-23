using System.Text.Json;
using EHub.Application.Common.Interfaces.Persistence;
using EHub.Application.Common.Interfaces.Services;
using EHub.Application.Features.Classes.Common;
using EHub.Contracts.Checkpoints;
using EHub.Domain.Entities;
using EHub.Domain.Enums;
using EHub.Shared.Errors;
using EHub.Shared.Results;
using Microsoft.EntityFrameworkCore;

namespace EHub.Application.Features.Checkpoints.LecturerManagement;

public sealed class LecturerCheckpointManagementHandler(
    IApplicationDbContext context,
    IDateTimeProvider dateTimeProvider) : ILecturerCheckpointManagementHandler
{
    public async Task<Result<LecturerCheckpointOverviewResponse>> GetAsync(
        GetLecturerCheckpointsRequest request,
        Guid lecturerId,
        CancellationToken cancellationToken = default)
    {
        var classesQuery = context.Classes
            .AsNoTracking()
            .Where(item => item.PrimaryLecturerId == lecturerId ||
                item.ClassLecturers.Any(assignment => assignment.LecturerId == lecturerId));

        if (!string.IsNullOrWhiteSpace(request.Semester))
        {
            var semester = request.Semester.Trim().ToUpperInvariant();
            var term = semester switch
            {
                "SP" or "SPRING" => SemesterTerm.Spring,
                "SU" or "SUMMER" => SemesterTerm.Summer,
                "FA" or "FALL" => SemesterTerm.Fall,
                _ => (SemesterTerm?)null
            };
            if (!term.HasValue)
            {
                return Failure<LecturerCheckpointOverviewResponse>(
                    ErrorCodes.ClassValidationError,
                    "Semester must be SP, SU, or FA.");
            }
            classesQuery = classesQuery.Where(item => item.Semester.Term == term.Value);
        }

        if (request.Year.HasValue)
        {
            classesQuery = classesQuery.Where(item => item.Semester.Year == request.Year.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.SubjectCode))
        {
            var subject = request.SubjectCode.Trim().ToUpperInvariant();
            classesQuery = classesQuery.Where(item => item.Course.Code.ToUpper() == subject);
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim().ToLowerInvariant();
            if (search.Length > 100) search = search[..100];
            classesQuery = classesQuery.Where(item => item.ClassCode.ToLower().Contains(search) ||
                item.Course.Code.ToLower().Contains(search) ||
                item.Course.Name.ToLower().Contains(search));
        }

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            if (!Enum.TryParse<ClassStatus>(request.Status, true, out var requestedStatus))
            {
                return Failure<LecturerCheckpointOverviewResponse>(
                    ErrorCodes.ClassValidationError,
                    "Status filter must be Draft, Active, Inactive, Completed, or Archived.");
            }
            classesQuery = classesQuery.Where(item => item.Status == requestedStatus);
        }
        else
        {
            classesQuery = classesQuery.Where(item => item.Status == ClassStatus.Draft || item.Status == ClassStatus.Active);
        }

        var classRows = await classesQuery
            .OrderBy(item => item.Course.Code)
            .ThenBy(item => item.ClassIndex)
            .Select(item => new ClassRow(
                item.Id,
                item.ClassCode,
                item.CourseId,
                item.Course.Code,
                item.Course.Name,
                item.Semester.Code,
                item.Semester.Year))
            .ToArrayAsync(cancellationToken);

        var courseIds = classRows.Select(item => item.CourseId).Distinct().ToArray();
        var checkpointRows = courseIds.Length == 0
            ? Array.Empty<CheckpointRow>()
            : await context.Checkpoints
                .AsNoTracking()
                .Where(item => item.CourseId.HasValue && courseIds.Contains(item.CourseId.Value) &&
                    item.ClassId == null && item.Status != CheckpointStatus.Archived)
                .OrderBy(item => item.CheckpointNumber)
                .Select(item => new CheckpointRow(
                    item.Id,
                    item.CourseId!.Value,
                    item.CheckpointNumber,
                    item.Name,
                    item.Description))
                .ToArrayAsync(cancellationToken);

        var selectedClasses = request.ClassId.HasValue
            ? classRows.Where(item => item.Id == request.ClassId.Value).ToArray()
            : classRows;
        var selectedCourseIds = selectedClasses.Select(item => item.CourseId).Distinct().ToHashSet();
        var availableCheckpoints = checkpointRows
            .Where(item => selectedCourseIds.Contains(item.CourseId))
            .ToArray();
        var selectedCheckpoints = request.CheckpointId.HasValue
            ? availableCheckpoints.Where(item => item.Id == request.CheckpointId.Value).ToArray()
            : request.CheckpointNumber.HasValue
                ? availableCheckpoints.Where(item => item.Number == request.CheckpointNumber.Value).ToArray()
                : availableCheckpoints;

        var selectedClassIds = selectedClasses.Select(item => item.Id).ToArray();
        var selectedCheckpointIds = selectedCheckpoints.Select(item => item.Id).ToArray();
        var storedSchedules = selectedClassIds.Length == 0 || selectedCheckpointIds.Length == 0
            ? Array.Empty<ClassCheckpointSchedule>()
            : await context.ClassCheckpointSchedules
                .AsNoTracking()
                .Where(item => selectedClassIds.Contains(item.ClassId) && selectedCheckpointIds.Contains(item.CheckpointId))
                .ToArrayAsync(cancellationToken);
        var scheduleByKey = storedSchedules.ToDictionary(item => (item.ClassId, item.CheckpointId));

        var teams = selectedClassIds.Length == 0
            ? Array.Empty<TeamRow>()
            : await context.Teams
                .AsNoTracking()
                .Where(item => selectedClassIds.Contains(item.ClassId) && item.Status == TeamStatus.Active)
                .OrderBy(item => item.TeamName)
                .Select(item => new TeamRow(item.Id, item.ClassId, item.TeamName))
                .ToArrayAsync(cancellationToken);
        var teamIds = teams.Select(item => item.Id).ToArray();

        var submissionRows = teamIds.Length == 0 || selectedCheckpointIds.Length == 0
            ? Array.Empty<SubmissionRow>()
            : await context.Submissions
                .AsNoTracking()
                .Where(item => teamIds.Contains(item.TeamId) && selectedCheckpointIds.Contains(item.CheckpointId))
                .Select(item => new SubmissionRow(
                    item.Id,
                    item.TeamId,
                    item.CheckpointId,
                    item.Status,
                    item.SubmittedAt,
                    item.CreatedAt))
                .ToArrayAsync(cancellationToken);
        var submissionIds = submissionRows.Select(item => item.Id).ToArray();
        var fileRows = submissionIds.Length == 0
            ? Array.Empty<FileRow>()
            : await context.SubmissionFiles
                .AsNoTracking()
                .Where(item => submissionIds.Contains(item.SubmissionId))
                .Select(item => new FileRow(item.Id, item.SubmissionId, item.OriginalName, item.UploadedAt))
                .ToArrayAsync(cancellationToken);

        var now = EnsureUtc(dateTimeProvider.UtcNow);
        var schedules = selectedClasses
            .SelectMany(@class => selectedCheckpoints
                .Where(checkpoint => checkpoint.CourseId == @class.CourseId)
                .Select(checkpoint => ToScheduleResponse(
                    @class,
                    checkpoint,
                    scheduleByKey.GetValueOrDefault((@class.Id, checkpoint.Id)),
                    now)))
            .ToArray();

        var submissionsByKey = submissionRows
            .GroupBy(item => (item.TeamId, item.CheckpointId))
            .ToDictionary(group => group.Key, group => group.ToArray());
        var filesBySubmission = fileRows
            .GroupBy(item => item.SubmissionId)
            .ToDictionary(group => group.Key, group => group.ToArray());
        var classById = selectedClasses.ToDictionary(item => item.Id);

        var submissionResponses = teams
            .SelectMany(team => selectedCheckpoints
                .Where(checkpoint => checkpoint.CourseId == classById[team.ClassId].CourseId)
                .Select(checkpoint => BuildSubmissionResponse(
                    classById[team.ClassId],
                    team,
                    checkpoint,
                    scheduleByKey.GetValueOrDefault((team.ClassId, checkpoint.Id)),
                    submissionsByKey.GetValueOrDefault((team.Id, checkpoint.Id)) ?? Array.Empty<SubmissionRow>(),
                    filesBySubmission,
                    now)))
            .ToArray();

        return Result.Success(new LecturerCheckpointOverviewResponse
        {
            ServerTimeUtc = now,
            Classes = classRows.Select(item => new LecturerCheckpointClassResponse
            {
                Id = item.Id,
                ClassCode = item.ClassCode,
                SubjectCode = item.SubjectCode,
                SubjectName = item.SubjectName,
                SemesterCode = item.SemesterCode,
                Year = item.Year
            }).ToArray(),
            Checkpoints = availableCheckpoints.Select(item => new LecturerCheckpointDefinitionResponse
            {
                Id = item.Id,
                CourseId = item.CourseId,
                Number = item.Number,
                Title = item.Title,
                ShortDescription = item.Description
            }).ToArray(),
            Schedules = schedules,
            Submissions = submissionResponses
        });
    }

    public async Task<Result<ClassCheckpointScheduleResponse>> SaveScheduleAsync(
        Guid classId,
        Guid checkpointId,
        SaveClassCheckpointScheduleRequest request,
        Guid lecturerId,
        CancellationToken cancellationToken = default)
    {
        if (!request.StartDateUtc.HasValue || !request.EndDateUtc.HasValue)
        {
            return Failure<ClassCheckpointScheduleResponse>(
                ErrorCodes.ClassValidationError,
                "Start date and end date are required.");
        }

        var start = EnsureUtc(request.StartDateUtc.Value);
        var end = EnsureUtc(request.EndDateUtc.Value);
        if (start >= end)
        {
            return Failure<ClassCheckpointScheduleResponse>(
                ErrorCodes.ClassValidationError,
                "Start date must be earlier than end date.");
        }

        var classRow = await context.Classes
            .Where(item => item.Id == classId &&
                (item.PrimaryLecturerId == lecturerId || item.ClassLecturers.Any(link => link.LecturerId == lecturerId)))
            .Select(item => new ClassRow(
                item.Id,
                item.ClassCode,
                item.CourseId,
                item.Course.Code,
                item.Course.Name,
                item.Semester.Code,
                item.Semester.Year))
            .SingleOrDefaultAsync(cancellationToken);
        if (classRow is null)
        {
            return Failure<ClassCheckpointScheduleResponse>(
                ErrorCodes.ClassAccessDenied,
                "You do not have permission to manage this class.");
        }

        var checkpoint = await context.Checkpoints
            .Where(item => item.Id == checkpointId && item.ClassId == null &&
                item.CourseId == classRow.CourseId && item.Status != CheckpointStatus.Archived)
            .Select(item => new CheckpointRow(
                item.Id,
                item.CourseId!.Value,
                item.CheckpointNumber,
                item.Name,
                item.Description))
            .SingleOrDefaultAsync(cancellationToken);
        if (checkpoint is null)
        {
            return Failure<ClassCheckpointScheduleResponse>(
                ErrorCodes.CommonNotFoundError,
                "The checkpoint is not configured for this class subject.");
        }

        var schedule = await context.ClassCheckpointSchedules
            .SingleOrDefaultAsync(item => item.ClassId == classId && item.CheckpointId == checkpointId, cancellationToken);
        var isNew = schedule is null;
        var now = EnsureUtc(dateTimeProvider.UtcNow);
        var oldStart = schedule?.StartDateUtc;
        var oldEnd = schedule?.EndDateUtc;
        var isReopen = schedule is not null && schedule.EndDateUtc < now;
        if (isReopen && end <= now)
        {
            return Failure<ClassCheckpointScheduleResponse>(
                ErrorCodes.ClassValidationError,
                "A reopened checkpoint must end in the future.");
        }

        if (schedule is null)
        {
            schedule = new ClassCheckpointSchedule
            {
                ClassId = classId,
                CheckpointId = checkpointId,
                StartDateUtc = start,
                EndDateUtc = end,
                CreatedBy = lecturerId
            };
            context.ClassCheckpointSchedules.Add(schedule);
        }
        else
        {
            schedule.StartDateUtc = start;
            schedule.EndDateUtc = end;
            schedule.UpdatedBy = lecturerId;
            if (isReopen)
            {
                schedule.ReopenCount++;
                schedule.LastReopenedAtUtc = now;
            }
        }

        context.ClassAuditLogs.Add(new ClassAuditLog
        {
            ClassId = classId,
            Action = isReopen ? "CheckpointReopened" : isNew ? "CheckpointScheduled" : "CheckpointScheduleUpdated",
            PerformedByUserId = lecturerId,
            OccurredAtUtc = now,
            DetailsJson = JsonSerializer.Serialize(new
            {
                checkpointId,
                checkpointNumber = checkpoint.Number,
                oldStartDateUtc = oldStart,
                oldEndDateUtc = oldEnd,
                newStartDateUtc = start,
                newEndDateUtc = end
            })
        });

        await QueueDeadlineNotificationsAsync(classId, checkpointId, checkpoint.Number,
            start, end, now, isNew || oldStart != start || oldEnd != end, cancellationToken);

        await context.SaveChangesAsync(cancellationToken);
        return Result.Success(ToScheduleResponse(classRow, checkpoint, schedule, now));
    }

    public async Task<Result<BulkClassCheckpointScheduleResponse>> SaveBulkScheduleAsync(
        BulkSaveClassCheckpointScheduleRequest request,
        Guid lecturerId,
        CancellationToken cancellationToken = default)
    {
        if (!request.StartDateUtc.HasValue || !request.EndDateUtc.HasValue)
        {
            return Failure<BulkClassCheckpointScheduleResponse>(
                ErrorCodes.ClassValidationError, "Start date and end date are required.");
        }

        var start = EnsureUtc(request.StartDateUtc.Value);
        var end = EnsureUtc(request.EndDateUtc.Value);
        if (start >= end)
        {
            return Failure<BulkClassCheckpointScheduleResponse>(
                ErrorCodes.ClassValidationError, "Start date must be earlier than end date.");
        }

        if (request.CheckpointNumber <= 0 || request.ExpectedClassIds is null ||
            request.ExpectedClassIds.Count == 0 ||
            request.ExpectedClassIds.Distinct().Count() != request.ExpectedClassIds.Count)
        {
            return Failure<BulkClassCheckpointScheduleResponse>(
                ErrorCodes.ClassValidationError, "A checkpoint and the expected classes are required.");
        }

        // Resolve the current scope from the lecturer's assignments, never from submitted class IDs.
        var scope = await GetAsync(new GetLecturerCheckpointsRequest
        {
            Semester = request.Semester,
            Year = request.Year,
            SubjectCode = request.SubjectCode,
            Status = request.Status,
            Search = request.Search,
            CheckpointNumber = request.CheckpointNumber
        }, lecturerId, cancellationToken);
        if (scope.IsFailure)
            return Result.Failure<BulkClassCheckpointScheduleResponse>(scope.Error);

        var targets = scope.Value.Schedules.ToArray();
        if (targets.Length == 0)
        {
            return Failure<BulkClassCheckpointScheduleResponse>(
                ErrorCodes.CommonNotFoundError, "This checkpoint is not configured for any class in your scope.");
        }

        var targetIds = targets.Select(item => item.ClassId).OrderBy(item => item).ToArray();
        var expectedIds = request.ExpectedClassIds.OrderBy(item => item).ToArray();
        if (targetIds.Distinct().Count() != targets.Length || !targetIds.SequenceEqual(expectedIds))
        {
            return Failure<BulkClassCheckpointScheduleResponse>(
                ErrorCodes.ClassAccessDenied, "The class selection changed or includes a class outside your scope. Refresh and try again.");
        }

        var now = EnsureUtc(dateTimeProvider.UtcNow);
        var checkpointIds = targets.Select(item => item.CheckpointId).Distinct().ToArray();
        var existing = await context.ClassCheckpointSchedules
            .Where(item => targetIds.Contains(item.ClassId) && checkpointIds.Contains(item.CheckpointId))
            .ToArrayAsync(cancellationToken);
        if (existing.Any(item => item.EndDateUtc < now) && end <= now)
        {
            return Failure<BulkClassCheckpointScheduleResponse>(
                ErrorCodes.ClassValidationError, "A reopened checkpoint must end in the future.");
        }

        var existingByKey = existing.ToDictionary(item => (item.ClassId, item.CheckpointId));
        var definitionsById = scope.Value.Checkpoints.ToDictionary(item => item.Id);
        var classesById = scope.Value.Classes.ToDictionary(item => item.Id);
        var responses = new List<ClassCheckpointScheduleResponse>(targets.Length);
        foreach (var target in targets)
        {
            var oldSchedule = existingByKey.GetValueOrDefault((target.ClassId, target.CheckpointId));
            var definition = definitionsById[target.CheckpointId];
            var isReopen = oldSchedule is not null && oldSchedule.EndDateUtc < now;
            var schedule = oldSchedule;
            if (schedule is null)
            {
                schedule = new ClassCheckpointSchedule
                {
                    ClassId = target.ClassId,
                    CheckpointId = target.CheckpointId,
                    StartDateUtc = start,
                    EndDateUtc = end,
                    CreatedBy = lecturerId
                };
                context.ClassCheckpointSchedules.Add(schedule);
            }
            else
            {
                schedule.StartDateUtc = start;
                schedule.EndDateUtc = end;
                schedule.UpdatedBy = lecturerId;
                if (isReopen)
                {
                    schedule.ReopenCount++;
                    schedule.LastReopenedAtUtc = now;
                }
            }

            context.ClassAuditLogs.Add(new ClassAuditLog
            {
                ClassId = target.ClassId,
                Action = isReopen ? "CheckpointReopened" : oldSchedule is null ? "CheckpointScheduled" : "CheckpointScheduleUpdated",
                PerformedByUserId = lecturerId,
                OccurredAtUtc = now,
                DetailsJson = JsonSerializer.Serialize(new
                {
                    checkpointId = target.CheckpointId,
                    checkpointNumber = definition.Number,
                    oldStartDateUtc = target.StartDateUtc,
                    oldEndDateUtc = target.EndDateUtc,
                    newStartDateUtc = start,
                    newEndDateUtc = end
                })
            });

            await QueueDeadlineNotificationsAsync(target.ClassId, target.CheckpointId,
                definition.Number, start, end, now,
                oldSchedule is null || target.StartDateUtc != start || target.EndDateUtc != end,
                cancellationToken);

            var @class = classesById[target.ClassId];
            responses.Add(ToScheduleResponse(
                new ClassRow(@class.Id, @class.ClassCode, definition.CourseId,
                    @class.SubjectCode, @class.SubjectName, @class.SemesterCode, @class.Year),
                new CheckpointRow(definition.Id, definition.CourseId, definition.Number,
                    definition.Title, definition.ShortDescription),
                schedule,
                now));
        }

        // EF Core commits all schedule and audit changes in one SaveChanges transaction.
        await context.SaveChangesAsync(cancellationToken);
        return Result.Success(new BulkClassCheckpointScheduleResponse
        {
            AppliedClassCount = responses.Count,
            Schedules = responses
        });
    }

    private async Task QueueDeadlineNotificationsAsync(
        Guid classId,
        Guid checkpointId,
        int checkpointNumber,
        DateTime start,
        DateTime end,
        DateTime now,
        bool scheduleChanged,
        CancellationToken cancellationToken)
    {
        if (!scheduleChanged) return;

        // Existing pending reminders belong to the old window. Keep their audit trail,
        // but stop delivery before creating the new window's reminder.
        var pending = await context.OutboxMessages
            .Where(item => item.AggregateId == classId &&
                item.Type == CheckpointDeadlineEvents.DeadlineReminder &&
                item.Status == OutboxMessageStatus.Pending)
            .ToArrayAsync(cancellationToken);
        foreach (var message in pending)
        {
            using var payload = JsonDocument.Parse(message.PayloadJson);
            if (!payload.RootElement.TryGetProperty("data", out var data) ||
                !data.TryGetProperty("checkpointId", out var value) ||
                !value.TryGetGuid(out var pendingCheckpointId) || pendingCheckpointId != checkpointId)
                continue;

            message.Status = OutboxMessageStatus.Processed;
            message.ProcessedAtUtc = now;
            message.LastError = "Superseded by a checkpoint schedule update";
        }

        if (end <= now) return;

        var notification = new { checkpointId, checkpointNumber, startDateUtc = start, endDateUtc = end };
        ClassOutbox.Enqueue(context, CheckpointDeadlineEvents.ScheduleChanged,
            classId, notification, now);
        var reminderAt = end.Subtract(CheckpointDeadlineEvents.ReminderLeadTime);
        if (reminderAt > now)
        {
            ClassOutbox.Enqueue(context, CheckpointDeadlineEvents.DeadlineReminder,
                classId, notification, now, reminderAt);
        }
    }

    private static ClassCheckpointScheduleResponse ToScheduleResponse(
        ClassRow @class,
        CheckpointRow checkpoint,
        ClassCheckpointSchedule? schedule,
        DateTime now) => new()
    {
        Id = schedule?.Id,
        ClassId = @class.Id,
        ClassCode = @class.ClassCode,
        CheckpointId = checkpoint.Id,
        CheckpointNumber = checkpoint.Number,
        CheckpointTitle = checkpoint.Title,
        StartDateUtc = schedule?.StartDateUtc,
        EndDateUtc = schedule?.EndDateUtc,
        Status = ScheduleStatus(schedule, now),
        CanReopen = schedule is not null && now > schedule.EndDateUtc,
        ReopenCount = schedule?.ReopenCount ?? 0
    };

    private static LecturerCheckpointSubmissionResponse BuildSubmissionResponse(
        ClassRow @class,
        TeamRow team,
        CheckpointRow checkpoint,
        ClassCheckpointSchedule? schedule,
        IReadOnlyCollection<SubmissionRow> submissions,
        IReadOnlyDictionary<Guid, FileRow[]> filesBySubmission,
        DateTime now)
    {
        var latest = submissions
            .OrderByDescending(item => item.SubmittedAt ?? item.CreatedAt)
            .FirstOrDefault();
        var earliestFile = submissions
            .SelectMany(item => filesBySubmission.GetValueOrDefault(item.Id) ?? Array.Empty<FileRow>())
            .OrderBy(item => item.UploadedAt)
            .FirstOrDefault();

        return new LecturerCheckpointSubmissionResponse
        {
            ClassId = @class.Id,
            ClassCode = @class.ClassCode,
            TeamId = team.Id,
            TeamName = team.Name,
            CheckpointId = checkpoint.Id,
            CheckpointNumber = checkpoint.Number,
            CheckpointTitle = checkpoint.Title,
            Status = SubmissionPresentationStatus(schedule, submissions, now),
            LatestSubmissionAtUtc = latest?.SubmittedAt,
            EarliestSubmittedFile = earliestFile is null ? null : new LecturerCheckpointFileResponse
            {
                Id = earliestFile.Id,
                OriginalName = earliestFile.OriginalName,
                UploadedAtUtc = earliestFile.UploadedAt
            }
        };
    }

    private static string ScheduleStatus(ClassCheckpointSchedule? schedule, DateTime now)
    {
        if (schedule is null) return "NotScheduled";
        if (now < schedule.StartDateUtc) return "Upcoming";
        return now <= schedule.EndDateUtc ? "Open" : "Closed";
    }

    private static string SubmissionPresentationStatus(
        ClassCheckpointSchedule? schedule,
        IReadOnlyCollection<SubmissionRow> submissions,
        DateTime now)
    {
        var scheduleStatus = ScheduleStatus(schedule, now);
        if (scheduleStatus == "Upcoming") return "Upcoming";
        if (submissions.Any(item => item.SubmittedAt.HasValue && item.Status != SubmissionStatus.Draft))
            return "Submitted";
        if (submissions.Any(item => item.Status == SubmissionStatus.Draft))
            return "Draft";
        if (scheduleStatus == "Open") return "Pending";
        return scheduleStatus;
    }

    private static DateTime EnsureUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };

    private static Result<T> Failure<T>(string code, string message) =>
        Result.Failure<T>(new Error(code, message));

    private sealed record ClassRow(Guid Id, string ClassCode, Guid CourseId, string SubjectCode, string SubjectName, string SemesterCode, int Year);
    private sealed record CheckpointRow(Guid Id, Guid CourseId, int Number, string Title, string? Description);
    private sealed record TeamRow(Guid Id, Guid ClassId, string Name);
    private sealed record SubmissionRow(Guid Id, Guid TeamId, Guid CheckpointId, SubmissionStatus Status, DateTime? SubmittedAt, DateTime CreatedAt);
    private sealed record FileRow(Guid Id, Guid SubmissionId, string OriginalName, DateTime UploadedAt);
}
