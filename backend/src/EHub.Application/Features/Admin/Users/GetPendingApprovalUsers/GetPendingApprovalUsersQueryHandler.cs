using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EHub.Application.Common.Interfaces.Persistence;
using EHub.Contracts.Admin.Users;
using EHub.Shared.Results;

namespace EHub.Application.Features.Admin.Users.GetPendingApprovalUsers;

public sealed class GetPendingApprovalUsersQueryHandler 
    : IGetPendingApprovalUsersQueryHandler
{
    private readonly IUserRepository _userRepository;

    public GetPendingApprovalUsersQueryHandler(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<Result<IReadOnlyCollection<PendingApprovalUserResponse>>> HandleAsync(
        CancellationToken cancellationToken = default)
    {
        var users = await _userRepository.GetPendingApprovalUsersAsync(cancellationToken);

        var response = users
            .Select(user => new PendingApprovalUserResponse
            {
                Id = user.Id,
                FullName = user.FullName,
                Email = user.Email,
                Roles = user.UserRoles
                    .Select(userRole => userRole.Role.Name)
                    .ToArray(),
                Status = user.Status.ToString(),
                CreatedAt = user.CreatedAt
            })
            .ToArray();

        return Result.Success<IReadOnlyCollection<PendingApprovalUserResponse>>(response);
    }
}
