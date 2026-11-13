using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using EHub.Application.Common.Interfaces.Identity;
using EHub.Application.Common.Interfaces.Persistence;
using EHub.Application.Common.Interfaces.Services;
using EHub.Contracts.Mentors;
using EHub.Domain.Entities;
using EHub.Domain.Enums;
using EHub.Shared.Constants;
using EHub.Shared.Errors;
using EHub.Shared.Results;
using Microsoft.EntityFrameworkCore;

namespace EHub.Application.Features.Admin.Mentors.AllocationJobs;

public sealed class MentorAllocationJobHandler(IApplicationDbContext db, ICurrentUserService user, IDateTimeProvider clock)
    : IMentorAllocationJobHandler
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<Result<MentorAllocationJobResponse>> StartAsync(PreviewMentorAllocationRequest request, CancellationToken ct)
    {
        if (!IsAdmin) return Fail(ErrorCodes.CommonUnauthorizedError, "An authenticated administrator is required.");
        if (!string.Equals(request.Strategy, MentorAllocationStrategies.Ai, StringComparison.OrdinalIgnoreCase) ||
            request.SemesterId == Guid.Empty || request.ClassIds is null || request.Edits is null)
            return Fail(ErrorCodes.MentorAllocationInvalid, "The AI allocation scope is invalid.");
        if (!await db.Semesters.AnyAsync(item => item.Id == request.SemesterId, ct))
            return Fail(ErrorCodes.SemesterNotFound, "The selected semester was not found.");
        var classIds = request.ClassIds.Distinct().ToArray();
        if (classIds.Length > 0 && await db.Classes.CountAsync(item => classIds.Contains(item.Id) &&
                item.SemesterId == request.SemesterId && item.Status == ClassStatus.Active, ct) != classIds.Length)
            return Fail(ErrorCodes.MentorAllocationInvalid, "Selected classes must be active and belong to the selected semester.");
        var json = JsonSerializer.Serialize(new PreviewMentorAllocationRequest
        {
            SemesterId = request.SemesterId, ClassIds = classIds.Order().ToArray(), Edits = request.Edits,
            Strategy = MentorAllocationStrategies.Ai, Seed = request.Seed, IncludeTemporaryMentors = request.IncludeTemporaryMentors
        }, Json);
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json)));
        var now = clock.UtcNow;
        var active = db.MentorAllocationJobs.Where(item => item.AdminUserId == user.UserId && item.RequestHash == hash &&
            (item.Status == MentorAllocationJobStatus.Queued || item.Status == MentorAllocationJobStatus.Processing));
        await active.Where(item => item.ExpiresAtUtc <= now).ExecuteUpdateAsync(update => update
            .SetProperty(item => item.Status, MentorAllocationJobStatus.Failed)
            .SetProperty(item => item.ErrorMessage, "The allocation task expired."), ct);
        var existing = await active.AsNoTracking().FirstOrDefaultAsync(ct);
        if (existing is not null) return Result.Success(ToResponse(existing));
        var job = new MentorAllocationJob
        {
            AdminUserId = user.UserId!.Value, SemesterId = request.SemesterId, RequestHash = hash, RequestJson = json,
            Status = MentorAllocationJobStatus.Queued, CreatedAtUtc = now, ExpiresAtUtc = now.AddDays(1)
        };
        db.MentorAllocationJobs.Add(job);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException)
        {
            db.ClearChanges();
            existing = await active.AsNoTracking().FirstOrDefaultAsync(ct);
            if (existing is null) throw;
            return Result.Success(ToResponse(existing));
        }
        return Result.Success(ToResponse(job));
    }

    public async Task<Result<MentorAllocationJobResponse>> GetAsync(Guid id, CancellationToken ct)
    {
        if (!IsAdmin) return Fail(ErrorCodes.CommonUnauthorizedError, "An authenticated administrator is required.");
        var now = clock.UtcNow;
        var job = await db.MentorAllocationJobs.AsNoTracking().FirstOrDefaultAsync(item =>
            item.Id == id && item.AdminUserId == user.UserId && item.ExpiresAtUtc > now, ct);
        return job is null ? Fail(ErrorCodes.CommonNotFoundError, "The allocation task was not found.") : Result.Success(ToResponse(job));
    }

    private bool IsAdmin => user.IsAuthenticated && user.UserId is { } id && id != Guid.Empty &&
        user.Roles.Any(role => role.Equals(SystemRoles.Admin, StringComparison.OrdinalIgnoreCase));
    private static Result<MentorAllocationJobResponse> Fail(string code, string message) =>
        Result.Failure<MentorAllocationJobResponse>(new Error(code, message));
    private static MentorAllocationJobResponse ToResponse(MentorAllocationJob job) => new()
    {
        Id = job.Id, Status = job.Status.ToString(), CompletedBatches = job.CompletedBatches,
        TotalBatches = job.TotalBatches, Message = job.ErrorMessage,
        Preview = job.Status == MentorAllocationJobStatus.Ready && job.ResultJson is not null
            ? JsonSerializer.Deserialize<MentorAllocationPreviewResponse>(job.ResultJson, Json) : null
    };
}
