using System.Text.Json;
using EHub.Application.Common.Exceptions;
using EHub.Application.Common.Interfaces.Identity;
using EHub.Application.Common.Interfaces.Persistence;
using EHub.Contracts.Subjects;
using EHub.Domain.Entities;
using EHub.Domain.Enums;
using EHub.Shared.Errors;
using EHub.Shared.Results;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EHub.Application.Features.Subjects.ManageSemester;

public sealed class CurrentSemesterHandler : ICurrentSemesterHandler
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<CurrentSemesterHandler> _logger;

    public CurrentSemesterHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser,
        IUnitOfWork unitOfWork,
        ILogger<CurrentSemesterHandler> logger)
    {
        _context = context;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result<CurrentSemesterResponse>> GetAsync(CancellationToken token = default)
    {
        var now = DateTime.UtcNow;
        var semester = await _context.Semesters.AsNoTracking()
            .Where(item => item.Status == SemesterStatus.Active)
            .OrderByDescending(item => item.Year)
            .ThenByDescending(item => item.Term)
            .FirstOrDefaultAsync(token);

        return Result.Success(new CurrentSemesterResponse
        {
            CurrentSemester = semester == null ? null : ToResponse(semester),
            AvailableYears = await GetAvailableYearsAsync(now, token),
            IsDecember = now.Month == 12,
        });
    }

    public async Task<Result<SemesterListResponse>> GetAllAsync(CancellationToken token = default)
    {
        var semesters = await _context.Semesters.AsNoTracking()
            .OrderByDescending(item => item.Year)
            .ThenByDescending(item => item.Term)
            .ToListAsync(token);
        return Result.Success(new SemesterListResponse { Semesters = semesters.Select(ToResponse).ToArray() });
    }

    public async Task<Result<CurrentSemesterResponse>> SetAsync(
        SetCurrentSemesterRequest request,
        CancellationToken token = default)
    {
        if (!IsAdmin())
            return Failure<CurrentSemesterResponse>(ErrorCodes.ClassAccessDenied, "Only an administrator can activate a semester.");
        if (!TryParseTerm(request.Semester, out var term))
            return Failure<CurrentSemesterResponse>(ErrorCodes.ClassValidationError, "Semester must be SP, SU, or FA.");

        var now = DateTime.UtcNow;
        if (request.Year < 2000 || request.Year > now.Year + 1)
            return Failure<CurrentSemesterResponse>(ErrorCodes.ClassValidationError, "Semester year is outside the supported range.");
        if (request.Year > now.Year && !(now.Month == 12 && request.Year == now.Year + 1))
            return Failure<CurrentSemesterResponse>(ErrorCodes.SemesterActivationBlocked, "Next-year activation is only available in December.");

        try
        {
            return await _unitOfWork.ExecuteInSerializableTransactionAsync(async cancellationToken =>
            {
                var existingActive = await _context.Semesters
                    .FirstOrDefaultAsync(item => item.Status == SemesterStatus.Active, cancellationToken);
                var semester = await _context.Semesters.FirstOrDefaultAsync(
                    item => item.Term == term && item.Year == request.Year,
                    cancellationToken);

                if (semester?.Status == SemesterStatus.Active)
                    return await BuildCurrentResponseAsync(semester, now, cancellationToken);
                if (existingActive != null)
                    return Failure<CurrentSemesterResponse>(
                        ErrorCodes.SemesterActivationBlocked,
                        $"Complete {ToTermCode(existingActive.Term)} {existingActive.Year} before activating another semester.");
                if (semester?.Status is SemesterStatus.Completed or SemesterStatus.Archived)
                    return Failure<CurrentSemesterResponse>(
                        ErrorCodes.SemesterInvalidState,
                        "A completed or archived semester must be explicitly reopened before activation.");

                if (semester == null)
                {
                    var (startDate, endDate) = GetDefaultDates(term, request.Year);
                    semester = new Semester
                    {
                        Code = $"{ToTermCode(term)}{request.Year}",
                        Name = $"{GetTermName(term)} {request.Year}",
                        Term = term,
                        Year = request.Year,
                        StartDate = startDate,
                        EndDate = endDate,
                        Status = SemesterStatus.Planned,
                        CreatedBy = _currentUser.UserId,
                    };
                    await _context.Semesters.AddAsync(semester, cancellationToken);
                }

                semester.Status = SemesterStatus.Active;
                semester.UpdatedBy = _currentUser.UserId;
                AddAuditAndOutbox(semester, "SEMESTER_ACTIVATED", "Semester.Activated.v1", null);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                return await BuildCurrentResponseAsync(semester, now, cancellationToken);
            }, token);
        }
        catch (DbUpdateException exception)
        {
            _logger.LogWarning(exception, "Could not activate semester {Term} {Year}", request.Semester, request.Year);
            return Failure<CurrentSemesterResponse>(ErrorCodes.SemesterActivationBlocked, "The semester conflicts with current academic data. Reload and try again.");
        }
        catch (SerializableTransactionConflictException)
        {
            return Failure<CurrentSemesterResponse>(ErrorCodes.SemesterConcurrencyConflict, "Another semester operation completed first. Reload and try again.");
        }
    }

    public async Task<Result<SemesterCompletionPreviewResponse>> PreviewCompletionAsync(
        Guid semesterId,
        CancellationToken token = default)
    {
        if (!IsAdmin())
            return Failure<SemesterCompletionPreviewResponse>(ErrorCodes.ClassAccessDenied, "Only an administrator can preview semester completion.");
        var semester = await _context.Semesters.AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == semesterId, token);
        return semester == null
            ? Failure<SemesterCompletionPreviewResponse>(ErrorCodes.SemesterNotFound, "The requested semester was not found.")
            : Result.Success(await BuildPreviewAsync(semester, token));
    }

    public Task<Result<SemesterResponse>> CompleteAsync(
        Guid semesterId,
        ChangeSemesterLifecycleRequest request,
        CancellationToken token = default) =>
        ChangeLifecycleAsync(semesterId, request, reopen: false, token);

    public Task<Result<SemesterResponse>> ReopenAsync(
        Guid semesterId,
        ChangeSemesterLifecycleRequest request,
        CancellationToken token = default) =>
        ChangeLifecycleAsync(semesterId, request, reopen: true, token);

    private async Task<Result<SemesterResponse>> ChangeLifecycleAsync(
        Guid semesterId,
        ChangeSemesterLifecycleRequest request,
        bool reopen,
        CancellationToken token)
    {
        if (!IsAdmin())
            return Failure<SemesterResponse>(ErrorCodes.ClassAccessDenied, "Only an administrator can change semester lifecycle.");
        if (!uint.TryParse(request.RowVersion, out var expectedVersion))
            return Failure<SemesterResponse>(ErrorCodes.ClassValidationError, "A valid rowVersion is required.");
        var reason = request.Reason?.Trim();
        if (string.IsNullOrWhiteSpace(reason) || reason.Length is < 3 or > 500)
            return Failure<SemesterResponse>(ErrorCodes.ClassValidationError, "Reason must contain between 3 and 500 characters.");

        try
        {
            return await _unitOfWork.ExecuteInSerializableTransactionAsync(async cancellationToken =>
            {
                var semester = await _context.Semesters
                    .FirstOrDefaultAsync(item => item.Id == semesterId, cancellationToken);
                if (semester == null)
                    return Failure<SemesterResponse>(ErrorCodes.SemesterNotFound, "The requested semester was not found.");

                if ((reopen && semester.Status == SemesterStatus.Active) || (!reopen && semester.Status == SemesterStatus.Completed))
                    return Result.Success(ToResponse(semester));
                if (semester.Version != expectedVersion)
                    return Failure<SemesterResponse>(ErrorCodes.SemesterConcurrencyConflict, "The semester changed concurrently. Reload and try again.");

                if (reopen)
                {
                    if (semester.Status != SemesterStatus.Completed)
                        return Failure<SemesterResponse>(ErrorCodes.SemesterInvalidState, "Only a completed semester can be reopened.");
                    if (await _context.Semesters.AnyAsync(
                            item => item.Status == SemesterStatus.Active && item.Id != semester.Id,
                            cancellationToken))
                        return Failure<SemesterResponse>(ErrorCodes.SemesterActivationBlocked, "Complete the active semester before reopening this semester.");

                    semester.Status = SemesterStatus.Active;
                    semester.CompletedAtUtc = null;
                    semester.CompletedByUserId = null;
                    semester.CompletionReason = null;
                    semester.UpdatedBy = _currentUser.UserId;
                    AddAuditAndOutbox(semester, "SEMESTER_REOPENED", "Semester.Reopened.v1", reason);
                }
                else
                {
                    if (semester.Status != SemesterStatus.Active)
                        return Failure<SemesterResponse>(ErrorCodes.SemesterInvalidState, "Only the active semester can be completed.");

                    var preview = await BuildPreviewAsync(semester, cancellationToken);
                    if (preview.Blockers.Count > 0)
                        return Failure<SemesterResponse>(ErrorCodes.SemesterCompletionBlocked, string.Join(" ", preview.Blockers));

                    semester.Status = SemesterStatus.Completed;
                    semester.CompletedAtUtc = DateTime.UtcNow;
                    semester.CompletedByUserId = _currentUser.UserId;
                    semester.CompletionReason = reason;
                    semester.UpdatedBy = _currentUser.UserId;
                    AddAuditAndOutbox(semester, "SEMESTER_COMPLETED", "Semester.Completed.v1", reason);
                }

                await _unitOfWork.SaveChangesAsync(cancellationToken);
                return Result.Success(ToResponse(semester));
            }, token);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Failure<SemesterResponse>(ErrorCodes.SemesterConcurrencyConflict, "The semester changed concurrently. Reload and try again.");
        }
        catch (SerializableTransactionConflictException)
        {
            return Failure<SemesterResponse>(ErrorCodes.SemesterConcurrencyConflict, "Another semester operation completed first. Reload and try again.");
        }
        catch (DbUpdateException exception)
        {
            _logger.LogWarning(exception, "Semester lifecycle conflict for {SemesterId}", semesterId);
            return Failure<SemesterResponse>(ErrorCodes.SemesterCompletionBlocked, "The semester conflicts with current academic data. Reload and try again.");
        }
    }

    private async Task<SemesterCompletionPreviewResponse> BuildPreviewAsync(Semester semester, CancellationToken token)
    {
        var statusCounts = await _context.Classes.AsNoTracking()
            .Where(item => item.SemesterId == semester.Id)
            .GroupBy(item => item.Status)
            .Select(group => new { Status = group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.Status, item => item.Count, token);
        var activeEnrollments = await _context.ClassStudents.AsNoTracking()
            .CountAsync(item => item.Class.SemesterId == semester.Id && item.EnrollmentStatus == EnrollmentStatus.Active, token);
        var processingImports = await _context.ClassImportSessions.AsNoTracking()
            .CountAsync(item => item.Class.SemesterId == semester.Id &&
                item.Status == ClassImportSessionStatus.Processing &&
                item.ExpiresAtUtc > DateTime.UtcNow, token);

        var draft = statusCounts.GetValueOrDefault(ClassStatus.Draft);
        var active = statusCounts.GetValueOrDefault(ClassStatus.Active);
        var inactive = statusCounts.GetValueOrDefault(ClassStatus.Inactive);
        var unfinished = draft + active + inactive;
        var blockers = new List<string>();
        if (unfinished > 0)
            blockers.Add($"Complete or archive the remaining {unfinished} non-completed class(es).");
        if (activeEnrollments > 0)
            blockers.Add($"Resolve {activeEnrollments} active enrollment(s).");
        if (processingImports > 0)
            blockers.Add($"Wait for {processingImports} processing import session(s) to finish.");

        return new SemesterCompletionPreviewResponse
        {
            SemesterId = semester.Id,
            Semester = ToTermCode(semester.Term),
            Year = semester.Year,
            Status = semester.Status.ToString(),
            DraftClassCount = draft,
            ActiveClassCount = active,
            InactiveClassCount = inactive,
            CompletedClassCount = statusCounts.GetValueOrDefault(ClassStatus.Completed),
            ArchivedClassCount = statusCounts.GetValueOrDefault(ClassStatus.Archived),
            ActiveEnrollmentCount = activeEnrollments,
            ProcessingImportSessionCount = processingImports,
            Blockers = blockers,
            RowVersion = semester.Version.ToString(),
        };
    }

    private void AddAuditAndOutbox(Semester semester, string action, string eventType, string? reason)
    {
        var eventId = Guid.NewGuid();
        var occurredAtUtc = DateTime.UtcNow;
        _context.SemesterAuditLogs.Add(new SemesterAuditLog
        {
            SemesterId = semester.Id,
            Action = action,
            PerformedByUserId = _currentUser.UserId ?? Guid.Empty,
            OccurredAtUtc = occurredAtUtc,
            DetailsJson = JsonSerializer.Serialize(new { reason, semester.Status }),
        });
        _context.OutboxMessages.Add(new OutboxMessage
        {
            EventId = eventId,
            Type = eventType,
            AggregateType = "Semester",
            AggregateId = semester.Id,
            OccurredAtUtc = occurredAtUtc,
            AvailableAtUtc = occurredAtUtc,
            PayloadJson = JsonSerializer.Serialize(new
            {
                EventId = eventId,
                EventType = eventType,
                AggregateType = "Semester",
                AggregateId = semester.Id,
                OccurredAtUtc = occurredAtUtc,
                Data = new
                {
                    SemesterId = semester.Id,
                    Term = ToTermCode(semester.Term),
                    semester.Year,
                    Status = semester.Status.ToString(),
                    Reason = reason,
                },
            }, JsonOptions),
        });
    }

    private async Task<CurrentSemesterResponse> BuildCurrentResponseAsync(Semester semester, DateTime now, CancellationToken token) => new()
    {
        CurrentSemester = ToResponse(semester),
        AvailableYears = await GetAvailableYearsAsync(now, token),
        IsDecember = now.Month == 12,
    };

    private async Task<int[]> GetAvailableYearsAsync(DateTime now, CancellationToken token)
    {
        var years = await _context.Semesters.AsNoTracking().Select(item => item.Year).Distinct().ToListAsync(token);
        if (!years.Contains(now.Year)) years.Add(now.Year);
        if (now.Month == 12 && !years.Contains(now.Year + 1)) years.Add(now.Year + 1);
        return years.OrderByDescending(item => item).ToArray();
    }

    private static bool TryParseTerm(string? value, out SemesterTerm term)
    {
        term = value?.Trim().ToUpperInvariant() switch
        {
            "SP" => SemesterTerm.Spring,
            "SU" => SemesterTerm.Summer,
            "FA" => SemesterTerm.Fall,
            _ => default,
        };
        return value != null && term is SemesterTerm.Spring or SemesterTerm.Summer or SemesterTerm.Fall;
    }

    private static (DateOnly Start, DateOnly End) GetDefaultDates(SemesterTerm term, int year) => term switch
    {
        SemesterTerm.Spring => (new DateOnly(year, 1, 1), new DateOnly(year, 4, 30)),
        SemesterTerm.Summer => (new DateOnly(year, 5, 1), new DateOnly(year, 8, 31)),
        SemesterTerm.Fall => (new DateOnly(year, 9, 1), new DateOnly(year, 12, 31)),
        _ => throw new ArgumentOutOfRangeException(nameof(term)),
    };

    private static string GetTermName(SemesterTerm term) => term switch
    {
        SemesterTerm.Spring => "Spring",
        SemesterTerm.Summer => "Summer",
        SemesterTerm.Fall => "Fall",
        _ => throw new ArgumentOutOfRangeException(nameof(term)),
    };

    private static string ToTermCode(SemesterTerm term) => term switch
    {
        SemesterTerm.Spring => "SP",
        SemesterTerm.Summer => "SU",
        SemesterTerm.Fall => "FA",
        _ => throw new ArgumentOutOfRangeException(nameof(term)),
    };

    private static SemesterResponse ToResponse(Semester value) => new()
    {
        Id = value.Id,
        Semester = ToTermCode(value.Term),
        Year = value.Year,
        Status = value.Status.ToString(),
        StartDate = value.StartDate,
        EndDate = value.EndDate,
        CompletedAtUtc = value.CompletedAtUtc,
        CompletionReason = value.CompletionReason,
        RowVersion = value.Version.ToString(),
    };

    private static Result<T> Failure<T>(string code, string message) => Result.Failure<T>(new Error(code, message));

    private bool IsAdmin() => _currentUser.Roles.Any(role =>
        string.Equals(role, EHub.Shared.Constants.SystemRoles.Admin, StringComparison.OrdinalIgnoreCase));
}
