using System.Threading;
using System.Threading.Tasks;
using EHub.Application.Common.Interfaces.Persistence;
using EHub.Domain.Entities;

namespace EHub.Infrastructure.Persistence.Repositories;

public class UserRoleRepository : IUserRoleRepository
{
    private readonly AppDbContext _context;

    public UserRoleRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(
        UserRole userRole,
        CancellationToken cancellationToken = default)
    {
        await _context.UserRoles.AddAsync(userRole, cancellationToken);
    }
}
