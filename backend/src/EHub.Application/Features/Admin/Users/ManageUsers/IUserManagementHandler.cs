using EHub.Contracts.Users;
using EHub.Shared.Results;

namespace EHub.Application.Features.Admin.Users.ManageUsers;

public interface IUserManagementHandler
{
    Task<Result<ManagedUserListResponse>> GetUsersAsync(int page, int limit, string? search, string? role, string? status, CancellationToken cancellationToken = default);
    Task<Result<ManagedUserResponse>> GetUserAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result<ManagedUserResponse>> CreateUserAsync(SaveManagedUserRequest request, CancellationToken cancellationToken = default);
    Task<Result<ManagedUserResponse>> UpdateUserAsync(Guid id, SaveManagedUserRequest request, CancellationToken cancellationToken = default);
    Task<Result> DeleteUserAsync(Guid id, CancellationToken cancellationToken = default);
}
