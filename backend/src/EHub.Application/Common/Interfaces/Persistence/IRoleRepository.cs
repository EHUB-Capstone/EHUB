using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EHub.Domain.Entities;

namespace EHub.Application.Common.Interfaces.Persistence;

public interface IRoleRepository
{
    Task<Role?> GetByNameAsync(
        string name,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<Role>> GetByNamesAsync(
        IReadOnlyCollection<string> names,
        CancellationToken cancellationToken = default);
}
