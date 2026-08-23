using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EHub.Application.Common.Interfaces.Identity;
using EHub.Application.Common.Interfaces.Persistence;
using EHub.Domain.Enums;
using EHub.Shared.Constants;
using EHub.Shared.Results;
using Microsoft.Extensions.Logging;

namespace EHub.Application.Features.Admin.Users.ApproveUser;

public sealed class ApproveUserCommandHandler : IApproveUserCommandHandler
{
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;

    private readonly ILogger<ApproveUserCommandHandler> _logger;

    public ApproveUserCommandHandler(
        IUserRepository userRepository,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        ILogger<ApproveUserCommandHandler> _logger)
    {
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        this._logger = _logger;
    }

    public async Task<Result> HandleAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdWithRolesAsync(
            userId,
            cancellationToken);

        if (user is null)
        {
            return Result.Failure(AdminUserErrors.UserNotFound);
        }

        if (user.Status != UserStatus.PendingApproval)
        {
            _logger.LogWarning(
                "Admin approval failed. Target user {TargetUserId} is not pending approval.",
                user.Id);
            return Result.Failure(AdminUserErrors.UserNotPendingApproval);
        }

        if (!user.IsEmailVerified)
        {
            return Result.Failure(AdminUserErrors.EmailNotVerified);
        }

        var roles = user.UserRoles
            .Select(userRole => userRole.Role.Name)
            .ToArray();

        var isApprovalTarget =
            roles.Contains(SystemRoles.Lecturer) ||
            roles.Contains(SystemRoles.Mentor);

        if (!isApprovalTarget)
        {
            return Result.Failure(AdminUserErrors.InvalidTargetRole);
        }

        user.Status = UserStatus.Active;

        if (_currentUserService.UserId is not null)
        {
            user.UpdatedBy = _currentUserService.UserId.Value;
        }

        _userRepository.Update(user);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Admin {AdminId} approved user {TargetUserId}.",
            _currentUserService.UserId,
            user.Id);

        return Result.Success();
    }
}
