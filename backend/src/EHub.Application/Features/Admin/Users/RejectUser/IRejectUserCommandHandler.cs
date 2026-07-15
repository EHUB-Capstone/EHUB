using System;
using System.Threading;
using System.Threading.Tasks;
using EHub.Shared.Results;

namespace EHub.Application.Features.Admin.Users.RejectUser;

public interface IRejectUserCommandHandler
{
    Task<Result> HandleAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
}
