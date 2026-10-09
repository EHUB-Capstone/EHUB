using System.Threading;
using System.Threading.Tasks;
using EHub.Domain.Entities;

namespace EHub.Application.Common.Interfaces.Persistence;

public interface IUserRoleRepository
{
    Task AddAsync(
        UserRole userRole,
        CancellationToken cancellationToken = default);
}
