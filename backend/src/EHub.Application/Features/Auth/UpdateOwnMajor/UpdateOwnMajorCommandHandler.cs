using EHub.Application.Common.Interfaces.Identity;
using EHub.Application.Common.Interfaces.Persistence;
using EHub.Application.Common.Interfaces.Services;
using EHub.Contracts.Auth;
using EHub.Shared.Constants;
using EHub.Shared.Errors;
using EHub.Shared.Results;
using Microsoft.EntityFrameworkCore;

namespace EHub.Application.Features.Auth.UpdateOwnMajor;

public sealed class UpdateOwnMajorCommandHandler(
    ICurrentUserService currentUserService,
    IApplicationDbContext context,
    IDateTimeProvider dateTimeProvider) : IUpdateOwnMajorCommandHandler
{
    public async Task<Result<UpdateOwnMajorResponse>> HandleAsync(
        UpdateOwnMajorRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!currentUserService.IsAuthenticated || currentUserService.UserId is null)
        {
            return Result.Failure<UpdateOwnMajorResponse>(CommonErrors.Unauthorized);
        }

        if (!currentUserService.Roles.Any(role =>
                string.Equals(role, SystemRoles.Student, StringComparison.OrdinalIgnoreCase)))
        {
            return Result.Failure<UpdateOwnMajorResponse>(CommonErrors.Forbidden);
        }

        if (!MajorCodes.IsValid(request.MajorCode))
        {
            return Result.Failure<UpdateOwnMajorResponse>(
                ErrorCodes.AuthInvalidMajor,
                "Select a valid student major.");
        }

        var student = await context.Students
            .FirstOrDefaultAsync(
                candidate => candidate.UserId == currentUserService.UserId.Value,
                cancellationToken);
        if (student is null)
        {
            return Result.Failure<UpdateOwnMajorResponse>(
                ErrorCodes.CommonNotFoundError,
                "Your student profile could not be found.");
        }

        var prepared = await StudentMajorUpdatePolicy.PrepareAsync(
            context,
            student,
            currentUserService.UserId.Value,
            request.MajorCode,
            dateTimeProvider.UtcNow,
            cancellationToken);
        if (prepared.IsFailure)
        {
            return Result.Failure<UpdateOwnMajorResponse>(prepared.Error);
        }

        if (prepared.Value.HasChanges)
        {
            try
            {
                await context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                return Result.Failure<UpdateOwnMajorResponse>(
                    ErrorCodes.ClassConcurrencyConflict,
                    "Your class or team changed concurrently. Refresh and try again.");
            }
        }

        return Result.Success(new UpdateOwnMajorResponse
        {
            MajorCode = prepared.Value.MajorCode,
            UpdatedEnrollmentCount = prepared.Value.UpdatedClassIds.Count,
            UpdatedClassIds = prepared.Value.UpdatedClassIds
        });
    }
}
