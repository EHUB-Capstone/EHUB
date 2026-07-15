using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EHub.Contracts.Admin.Users;
using EHub.Shared.Results;

namespace EHub.Application.Features.Admin.Users.GetPendingApprovalUsers;

public interface IGetPendingApprovalUsersQueryHandler
{
    Task<Result<IReadOnlyCollection<PendingApprovalUserResponse>>> HandleAsync(
        CancellationToken cancellationToken = default);
}
