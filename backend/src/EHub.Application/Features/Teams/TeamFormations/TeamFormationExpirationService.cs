using EHub.Application.Common.Exceptions;
using EHub.Application.Common.Interfaces.Persistence;
using EHub.Application.Common.Interfaces.Services;

namespace EHub.Application.Features.Teams.TeamFormations;

public sealed class TeamFormationExpirationService : ITeamFormationExpirationService
{
    private const int BatchSize = 200;

    private readonly IApplicationDbContext _context;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _clock;

    public TeamFormationExpirationService(IApplicationDbContext context, IUnitOfWork unitOfWork, IDateTimeProvider clock)
    {
        _context = context;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<int> ExpireDueAsync(CancellationToken cancellationToken = default)
    {
        var total = 0;
        while (true)
        {
            int expired;
            try
            {
                expired = await _unitOfWork.ExecuteInSerializableTransactionAsync(async ct =>
                {
                    var count = await TeamFormationExpiry.ApplyAsync(
                        _context, _clock.UtcNow, classId: null, formationId: null, BatchSize, ct);
                    if (count > 0) await _context.SaveChangesAsync(ct);
                    return count;
                }, cancellationToken);
            }
            catch (SerializableTransactionConflictException)
            {
                // A concurrent command changed the same rows; the next tick retries.
                return total;
            }

            total += expired;
            if (expired < BatchSize) return total;
        }
    }
}
