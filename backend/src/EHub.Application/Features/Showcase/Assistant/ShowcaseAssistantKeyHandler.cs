using EHub.Application.Common.Interfaces.Persistence;
using EHub.Application.Common.Interfaces.Services;
using EHub.Contracts.Showcase;
using EHub.Domain.Entities;
using EHub.Shared.Errors;
using EHub.Shared.Results;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EHub.Application.Features.Showcase.Assistant;

public sealed class ShowcaseAssistantKeyHandler(
    IApplicationDbContext context,
    IShowcaseAssistantKeyProtector protector,
    IValidator<SaveShowcaseAssistantKeyRequest> validator,
    IDateTimeProvider time,
    ILogger<ShowcaseAssistantKeyHandler> logger) : IShowcaseAssistantKeyHandler, IStoredShowcaseAssistantKey
{
    public async Task<Result<ShowcaseAssistantKeyStatusDto>> GetStatusAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty) return Unauthorized();
        var stored = await context.ShowcaseAssistantUserKeys.AsNoTracking().Where(item => item.UserId == userId)
            .Select(item => new { item.Last4, item.UpdatedAtUtc, item.Ciphertext }).FirstOrDefaultAsync(cancellationToken);
        // A key the server can no longer open (its encryption key was retired or replaced, or the row was altered or copied from
        // someone else) would only fail in the chat, so it is reported as not there and the user is simply asked again.
        return Result.Success(stored is not null && protector.CanOpen(userId, stored.Ciphertext)
            ? Status(stored.Last4, stored.UpdatedAtUtc) : Status(null, null));
    }

    public async Task<Result<ShowcaseAssistantKeyStatusDto>> SaveAsync(
        SaveShowcaseAssistantKeyRequest request, Guid userId, CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty) return Unauthorized();
        if (!protector.IsAvailable)
            return Result.Failure<ShowcaseAssistantKeyStatusDto>(ErrorCodes.ShowcaseAssistantKeyStorageUnavailable,
                "Máy chủ chưa bật việc lưu key. Bạn vẫn có thể giữ key trong bộ nhớ của tab này.");
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
            return Result.Failure<ShowcaseAssistantKeyStatusDto>(ErrorCodes.ShowcaseAssistantKeyInvalid, validation.Errors[0].ErrorMessage);

        var key = request.ApiKey.Trim();
        var protectedKey = protector.Protect(userId, key);
        var last4 = key[^4..];
        // Two requests may race to create or replace the row; the second one retries as an update of what the first one wrote.
        for (var attempt = 0; ; attempt++)
        {
            var now = time.UtcNow;
            var row = await context.ShowcaseAssistantUserKeys.FirstOrDefaultAsync(item => item.UserId == userId, cancellationToken);
            if (row is null)
                context.ShowcaseAssistantUserKeys.Add(new ShowcaseAssistantUserKey
                {
                    UserId = userId, Ciphertext = protectedKey, Last4 = last4, CreatedAtUtc = now, UpdatedAtUtc = now
                });
            else
            {
                row.Ciphertext = protectedKey;
                row.Last4 = last4;
                row.UpdatedAtUtc = now;
            }
            try
            {
                await context.SaveChangesAsync(cancellationToken);
                return Result.Success(Status(last4, now));
            }
            catch (Exception error) when (attempt == 0 && error is DbUpdateException or DbUpdateConcurrencyException)
            {
                context.ClearChanges();
            }
        }
    }

    public async Task<Result<ShowcaseAssistantKeyStatusDto>> DeleteAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty) return Unauthorized();
        var row = await context.ShowcaseAssistantUserKeys.FirstOrDefaultAsync(item => item.UserId == userId, cancellationToken);
        if (row is not null)
        {
            context.ShowcaseAssistantUserKeys.Remove(row);
            await context.SaveChangesAsync(cancellationToken);
        }
        return Result.Success(Status(null, null));
    }

    public async Task<string?> RevealAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty || !protector.IsAvailable) return null;
        var protectedKey = await context.ShowcaseAssistantUserKeys.AsNoTracking().Where(item => item.UserId == userId)
            .Select(item => item.Ciphertext).FirstOrDefaultAsync(cancellationToken);
        if (protectedKey is null) return null;
        if (protector.TryUnprotect(userId, protectedKey, out var key)) return key;
        // Never log the blob or anything derived from it.
        logger.LogWarning("A stored Showcase assistant key could not be opened with the configured encryption keys");
        return null;
    }

    private ShowcaseAssistantKeyStatusDto Status(string? last4, DateTime? updatedAt) => new()
    {
        StorageAvailable = protector.IsAvailable, HasKey = last4 is not null, Last4 = last4,
        UpdatedAt = last4 is null ? null : DateTime.SpecifyKind(updatedAt!.Value, DateTimeKind.Utc)
    };

    private static Result<ShowcaseAssistantKeyStatusDto> Unauthorized() =>
        Result.Failure<ShowcaseAssistantKeyStatusDto>(ErrorCodes.CommonUnauthorizedError, "Bạn cần đăng nhập để quản lý key.");
}
