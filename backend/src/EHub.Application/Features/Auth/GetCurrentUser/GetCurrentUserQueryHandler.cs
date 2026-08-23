using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EHub.Application.Common.Interfaces.Identity;
using EHub.Application.Common.Interfaces.Persistence;
using EHub.Application.Features.Auth;
using EHub.Contracts.Auth;
using EHub.Domain.Enums;
using EHub.Shared.Constants;
using EHub.Shared.Errors;
using EHub.Shared.Results;

namespace EHub.Application.Features.Auth.GetCurrentUser;

public sealed class GetCurrentUserQueryHandler : IGetCurrentUserQueryHandler
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IUserRepository _userRepository;
    private readonly IStudentRepository _studentRepository;

    public GetCurrentUserQueryHandler(
        ICurrentUserService currentUserService,
        IUserRepository userRepository,
        IStudentRepository studentRepository)
    {
        _currentUserService = currentUserService;
        _userRepository = userRepository;
        _studentRepository = studentRepository;
    }

    public async Task<Result<CurrentUserResponse>> HandleAsync(
        CancellationToken cancellationToken = default)
    {
        // 1. Check if user is authenticated via JWT middleware
        if (!_currentUserService.IsAuthenticated || _currentUserService.UserId is null)
        {
            return Result.Failure<CurrentUserResponse>(CommonErrors.Unauthorized);
        }

        var userId = _currentUserService.UserId.Value;

        // 2. Fetch fresh user information from database including roles
        var user = await _userRepository.GetByIdWithRolesAsync(
            userId,
            cancellationToken);

        if (user is null)
        {
            return Result.Failure<CurrentUserResponse>(CommonErrors.Unauthorized);
        }

        if (!user.IsEmailVerified)
        {
            return Result.Failure<CurrentUserResponse>(AuthErrors.EmailVerificationRequired);
        }

        // 3. Validate user status
        if (user.Status == UserStatus.PendingApproval)
        {
            return Result.Failure<CurrentUserResponse>(AuthErrors.AccountPendingApproval);
        }

        if (user.Status == UserStatus.Rejected)
        {
            return Result.Failure<CurrentUserResponse>(AuthErrors.AccountRejected);
        }

        if (user.Status == UserStatus.Blocked)
        {
            return Result.Failure<CurrentUserResponse>(AuthErrors.UserBlocked);
        }

        if (user.Status == UserStatus.Inactive)
        {
            return Result.Failure<CurrentUserResponse>(AuthErrors.UserInactive);
        }

        if (user.Status != UserStatus.Active)
        {
            return Result.Failure<CurrentUserResponse>(AuthErrors.UserInactive);
        }

        // 4. Extract roles
        var roles = user.UserRoles
            .Select(userRole => userRole.Role.Name)
            .ToArray();

        // 5. If Student, load MajorCode from Profile
        string? majorCode = null;

        if (roles.Contains(SystemRoles.Student))
        {
            var student = await _studentRepository.GetByUserIdAsync(
                user.Id,
                cancellationToken);

            majorCode = student?.MajorCode;
        }

        var response = new CurrentUserResponse
        {
            Id = user.Id,
            FullName = user.FullName,
            Email = user.Email,
            Roles = roles,
            Status = user.Status.ToString(),
            MajorCode = majorCode
        };

        return Result.Success(response);
    }
}
