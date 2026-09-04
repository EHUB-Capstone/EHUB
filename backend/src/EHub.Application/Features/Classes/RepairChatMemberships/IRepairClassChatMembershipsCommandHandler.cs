using EHub.Contracts.Classes;
using EHub.Shared.Results;

namespace EHub.Application.Features.Classes.RepairChatMemberships;

public interface IRepairClassChatMembershipsCommandHandler
{
    Task<Result<ChatMembershipSyncResponse>> HandleAsync(
        Guid classId,
        Guid currentUserId,
        string currentUserRole,
        CancellationToken cancellationToken = default);
}
