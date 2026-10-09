using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using EHub.Application.Common.Interfaces.Persistence;
using EHub.Domain.Entities;

namespace EHub.Infrastructure.Persistence.Repositories;

public class RoleRepository : IRoleRepository
{
    private readonly AppDbContext _context;

    public RoleRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<Role?> GetByNameAsync(
        string name,
        CancellationToken cancellationToken = default)
    {
        return await _context.Roles
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Name == name, cancellationToken);
    }

    public async Task<IReadOnlyCollection<Role>> GetByNamesAsync(
        IReadOnlyCollection<string> names,
        CancellationToken cancellationToken = default)
    {
        var roles = await _context.Roles
            .AsNoTracking()
            .Where(r => names.Contains(r.Name))
            .ToListAsync(cancellationToken);

        return roles;
    }
}
