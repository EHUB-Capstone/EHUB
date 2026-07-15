using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EHub.Application.Common.Interfaces.Identity;
using EHub.Application.Common.Interfaces.Persistence;
using EHub.Domain.Enums;
using EHub.Shared.Constants;
using EHub.Shared.Results;

namespace EHub.Application.Features.Admin.Users.RejectUser;

public sealed class RejectUserCommandHandler : IRejectUserCommandHandler
{
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;

    public RejectUserCommandHandler(
        IUserRepository userRepository,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService)
    {
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
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
            return Result.Failure(AdminUserErrors.UserNotPendingApproval);
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

        user.Status = UserStatus.Rejected;

        if (_currentUserService.UserId is not null)
        {
            user.UpdatedBy = _currentUserService.UserId.Value;
        }

        _userRepository.Update(user);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
