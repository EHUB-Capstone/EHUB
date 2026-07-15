using System;
using System.Threading;
using System.Threading.Tasks;
using EHub.Shared.Results;

namespace EHub.Application.Features.Admin.Users.ApproveUser;

public interface IApproveUserCommandHandler
{
    Task<Result> HandleAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
}
