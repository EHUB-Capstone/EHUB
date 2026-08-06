using EHub.Contracts.Classes;

namespace EHub.Application.Common.Interfaces.Services;

public interface IClassChatMembershipSynchronizer
{
    Task<ChatMembershipSyncResponse> SynchronizeAsync(
        Guid classId,
        Guid? requestedByUserId = null,
        CancellationToken cancellationToken = default);
}
