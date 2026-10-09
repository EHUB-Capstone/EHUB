using EHub.Application.Common.Interfaces.Persistence;
using EHub.Application.Common.Interfaces.Services;
using EHub.Contracts.Classes;
using EHub.Domain.Entities;
using EHub.Domain.Enums;
using EHub.Shared.Constants;
using EHub.Shared.Errors;
using EHub.Shared.Results;
using Microsoft.EntityFrameworkCore;

namespace EHub.Application.Features.Classes.CheckpointDeadlines;

public sealed class ClassCheckpointDeadlineHandler(IApplicationDbContext context, IDateTimeProvider clock) : IClassCheckpointDeadlineHandler
{
    public async Task<Result<IReadOnlyCollection<ClassCheckpointDeadlineClassResponse>>> GetAvailableClassesAsync(Guid userId, string role, CancellationToken cancellationToken = default)
    {
        var classes = await AuthorizedClasses(role, userId)
            .AsNoTracking()
            .OrderBy(item => item.Semester.Year).ThenBy(item => item.Semester.Code).ThenBy(item => item.ClassCode)
            .Select(item => new ClassCheckpointDeadlineClassResponse
            {
                ClassId = item.Id,
                ClassCode = item.ClassCode,
                SubjectName = item.Course.Name,
                SemesterCode = item.Semester.Code,
                SemesterStartDate = item.Semester.StartDate,
                SemesterEndDate = item.Semester.EndDate
            })
            .ToArrayAsync(cancellationToken);
        return Result.Success<IReadOnlyCollection<ClassCheckpointDeadlineClassResponse>>(classes);
    }

    public async Task<Result<IReadOnlyCollection<ClassCheckpointDeadlineResponse>>> GetAsync(Guid classId, Guid userId, string role, CancellationToken cancellationToken = default)
    {
        var targetClass = await FindAuthorizedClassAsync(classId, userId, role, cancellationToken);
        if (targetClass.IsFailure) return Result.Failure<IReadOnlyCollection<ClassCheckpointDeadlineResponse>>(targetClass.Error);

        var templates = await context.Checkpoints.AsNoTracking().Where(item => item.CourseId == targetClass.Value.CourseId && item.ClassId == null)
            .OrderBy(item => item.CheckpointNumber).ToArrayAsync(cancellationToken);
        var configured = await context.Checkpoints.AsNoTracking().Where(item => item.ClassId == classId)
            .ToDictionaryAsync(item => item.CheckpointNumber, cancellationToken);
        return Result.Success<IReadOnlyCollection<ClassCheckpointDeadlineResponse>>(templates.Select(template =>
        {
            var value = configured.GetValueOrDefault(template.CheckpointNumber);
            return ToResponse(value ?? template, value is null ? CheckpointStatus.Draft : value.Status);
        }).ToArray());
    }

    public async Task<Result<ClassCheckpointDeadlineResponse>> SaveAsync(Guid classId, int checkpointNumber, SaveClassCheckpointDeadlineRequest request, Guid userId, string role, CancellationToken cancellationToken = default)
    {
        if (checkpointNumber < 1) return Validation("Checkpoint number must be positive.");
        if (!Enum.TryParse<CheckpointStatus>(request.Status, true, out var requestedStatus) || requestedStatus is CheckpointStatus.Draft or CheckpointStatus.Closed)
            return Validation("Open and Closed are calculated automatically. Only Archived can be selected manually.");

        var now = clock.UtcNow;
        if (request.DueDate <= now) return Validation("Deadline must be in the future.");
        var targetClass = await FindAuthorizedClassAsync(classId, userId, role, cancellationToken);
        if (targetClass.IsFailure) return Result.Failure<ClassCheckpointDeadlineResponse>(targetClass.Error);
        var semesterValidation = ValidateSemesterWindow(targetClass.Value, request.DueDate, now);
        if (semesterValidation is not null) return Validation(semesterValidation);

        var template = await context.Checkpoints.AsNoTracking().FirstOrDefaultAsync(item => item.CourseId == targetClass.Value.CourseId && item.ClassId == null && item.CheckpointNumber == checkpointNumber, cancellationToken);
        if (template is null) return Result.Failure<ClassCheckpointDeadlineResponse>(ErrorCodes.CommonNotFoundError, "Checkpoint was not found for this class subject.");

        if (checkpointNumber > 1)
        {
            var previousSchedule = await context.Checkpoints.AsNoTracking().FirstOrDefaultAsync(item =>
                item.ClassId == classId && item.CheckpointNumber == checkpointNumber - 1, cancellationToken);
            if (previousSchedule?.DueDate is null)
                return Validation($"Configure the deadline for Checkpoint {checkpointNumber - 1} first.");
            if (request.DueDate <= previousSchedule.DueDate.Value)
                return Validation($"Deadline for Checkpoint {checkpointNumber} must be later than Checkpoint {checkpointNumber - 1}.");
        }

        var nextSchedule = await context.Checkpoints.AsNoTracking().FirstOrDefaultAsync(item =>
            item.ClassId == classId && item.CheckpointNumber == checkpointNumber + 1 && item.DueDate != null, cancellationToken);
        if (nextSchedule?.DueDate is not null && request.DueDate >= nextSchedule.DueDate.Value)
            return Validation($"Deadline for Checkpoint {checkpointNumber} must be earlier than Checkpoint {checkpointNumber + 1}.");

        var checkpoint = await context.Checkpoints.FirstOrDefaultAsync(item => item.ClassId == classId && item.CheckpointNumber == checkpointNumber, cancellationToken);
        if (checkpoint is null)
        {
            checkpoint = new Checkpoint
            {
                CourseId = targetClass.Value.CourseId, ClassId = classId, CheckpointNumber = template.CheckpointNumber,
                Name = template.Name, Description = template.Description, RequirementsJson = template.RequirementsJson,
                CreatedById = userId, OpenDate = now
            };
            context.Checkpoints.Add(checkpoint);
        }

        if (checkpoint.OpenDate is null) checkpoint.OpenDate = now;
        if (request.DueDate <= checkpoint.OpenDate.Value) return Validation("Deadline must be later than the checkpoint open time.");
        checkpoint.DueDate = request.DueDate;
        checkpoint.Status = requestedStatus == CheckpointStatus.Archived
            ? CheckpointStatus.Archived
            : CheckpointStatus.Open;
        checkpoint.UpdatedBy = userId;
        await context.SaveChangesAsync(cancellationToken);
        return Result.Success(ToResponse(checkpoint, checkpoint.Status));
    }

    public async Task<Result<ClassCheckpointDeadlineBulkResponse>> SaveForClassesAsync(int checkpointNumber, SaveClassCheckpointDeadlineBulkRequest request, Guid userId, string role, CancellationToken cancellationToken = default)
    {
        if (checkpointNumber < 1) return BulkValidation("Checkpoint number must be positive.");
        if (!Enum.TryParse<CheckpointStatus>(request.Status, true, out var requestedStatus) || requestedStatus is CheckpointStatus.Draft or CheckpointStatus.Closed)
            return BulkValidation("Open and Closed are calculated automatically. Only Archived can be selected manually.");
        var now = clock.UtcNow;
        if (request.DueDate <= now) return BulkValidation("Deadline must be in the future.");

        var allowedClasses = await AuthorizedClasses(role, userId).Include(item => item.Semester).ToArrayAsync(cancellationToken);
        var requestedIds = request.ClassIds.Distinct().ToArray();
        var targets = request.ApplyToAllAccessibleClasses
            ? allowedClasses
            : allowedClasses.Where(item => requestedIds.Contains(item.Id)).ToArray();
        if (targets.Length == 0) return BulkValidation("Select at least one class.");
        if (!request.ApplyToAllAccessibleClasses && targets.Length != requestedIds.Length)
            return Result.Failure<ClassCheckpointDeadlineBulkResponse>(ErrorCodes.ClassAccessDenied, "One or more selected classes cannot be managed by you.");

        foreach (var target in targets)
        {
            var semesterValidation = ValidateSemesterWindow(target, request.DueDate, now);
            if (semesterValidation is not null) return BulkValidation($"{target.ClassCode}: {semesterValidation}");
            var template = await context.Checkpoints.AsNoTracking().FirstOrDefaultAsync(item =>
                item.CourseId == target.CourseId && item.ClassId == null && item.CheckpointNumber == checkpointNumber, cancellationToken);
            if (template is null) return BulkValidation($"{target.ClassCode}: Checkpoint {checkpointNumber} was not found for this subject.");
            var orderingError = await ValidateCheckpointOrderAsync(target.Id, checkpointNumber, request.DueDate, cancellationToken);
            if (orderingError is not null) return BulkValidation($"{target.ClassCode}: {orderingError}");
        }

        foreach (var target in targets)
        {
            var saved = await SaveAsync(target.Id, checkpointNumber, request, userId, role, cancellationToken);
            if (saved.IsFailure) return Result.Failure<ClassCheckpointDeadlineBulkResponse>(saved.Error);
        }
        return Result.Success(new ClassCheckpointDeadlineBulkResponse { UpdatedCount = targets.Length, ClassIds = targets.Select(item => item.Id).ToArray() });
    }

    private async Task<Result<Class>> FindAuthorizedClassAsync(Guid classId, Guid userId, string role, CancellationToken cancellationToken)
    {
        var targetClass = await context.Classes.Include(item => item.Semester).Include(item => item.ClassLecturers).FirstOrDefaultAsync(item => item.Id == classId, cancellationToken);
        if (targetClass is null) return Result.Failure<Class>(ErrorCodes.ClassNotFound, "Class was not found.");
        var allowed = string.Equals(role, SystemRoles.Admin, StringComparison.OrdinalIgnoreCase) ||
            (string.Equals(role, SystemRoles.Lecturer, StringComparison.OrdinalIgnoreCase) &&
             (targetClass.PrimaryLecturerId == userId || targetClass.ClassLecturers.Any(item => item.LecturerId == userId)));
        return allowed ? Result.Success(targetClass) : Result.Failure<Class>(ErrorCodes.ClassAccessDenied, "You cannot manage checkpoint deadlines for this class.");
    }

    private static ClassCheckpointDeadlineResponse ToResponse(Checkpoint checkpoint, CheckpointStatus status) => new()
    {
        CheckpointNumber = checkpoint.CheckpointNumber, Title = checkpoint.Name, OpenDate = checkpoint.OpenDate, DueDate = checkpoint.DueDate, Status = status.ToString()
    };
    private static Result<ClassCheckpointDeadlineResponse> Validation(string message) => Result.Failure<ClassCheckpointDeadlineResponse>(ErrorCodes.ClassValidationError, message);
    private static Result<ClassCheckpointDeadlineBulkResponse> BulkValidation(string message) => Result.Failure<ClassCheckpointDeadlineBulkResponse>(ErrorCodes.ClassValidationError, message);

    private IQueryable<Class> AuthorizedClasses(string role, Guid userId)
    {
        if (string.Equals(role, SystemRoles.Admin, StringComparison.OrdinalIgnoreCase))
            return context.Classes.Where(item => item.Status == ClassStatus.Draft || item.Status == ClassStatus.Active);
        if (string.Equals(role, SystemRoles.Lecturer, StringComparison.OrdinalIgnoreCase))
            return context.Classes.Where(item =>
                (item.PrimaryLecturerId == userId || item.ClassLecturers.Any(assignment => assignment.LecturerId == userId)) &&
                (item.Status == ClassStatus.Draft || item.Status == ClassStatus.Active));
        return context.Classes.Where(_ => false);
    }

    private static string? ValidateSemesterWindow(Class targetClass, DateTime dueDate, DateTime now)
    {
        if (targetClass.Semester.StartDate is null || targetClass.Semester.EndDate is null)
            return "The class semester does not have a valid date range.";
        var today = DateOnly.FromDateTime(now);
        if (today < targetClass.Semester.StartDate.Value || today > targetClass.Semester.EndDate.Value)
            return $"Checkpoint configuration is only allowed during semester {targetClass.Semester.Code}.";
        var deadlineDate = DateOnly.FromDateTime(dueDate);
        return deadlineDate < targetClass.Semester.StartDate.Value || deadlineDate > targetClass.Semester.EndDate.Value
            ? $"Deadline must be within semester {targetClass.Semester.Code} ({targetClass.Semester.StartDate:dd/MM/yyyy} – {targetClass.Semester.EndDate:dd/MM/yyyy})."
            : null;
    }

    private async Task<string?> ValidateCheckpointOrderAsync(Guid classId, int checkpointNumber, DateTime dueDate, CancellationToken cancellationToken)
    {
        if (checkpointNumber > 1)
        {
            var previous = await context.Checkpoints.AsNoTracking().FirstOrDefaultAsync(item => item.ClassId == classId && item.CheckpointNumber == checkpointNumber - 1, cancellationToken);
            if (previous?.DueDate is null) return $"Configure the deadline for Checkpoint {checkpointNumber - 1} first.";
            if (dueDate <= previous.DueDate.Value) return $"Deadline for Checkpoint {checkpointNumber} must be later than Checkpoint {checkpointNumber - 1}.";
        }
        var next = await context.Checkpoints.AsNoTracking().FirstOrDefaultAsync(item => item.ClassId == classId && item.CheckpointNumber == checkpointNumber + 1 && item.DueDate != null, cancellationToken);
        return next?.DueDate is not null && dueDate >= next.DueDate.Value
            ? $"Deadline for Checkpoint {checkpointNumber} must be earlier than Checkpoint {checkpointNumber + 1}."
            : null;
    }
}
