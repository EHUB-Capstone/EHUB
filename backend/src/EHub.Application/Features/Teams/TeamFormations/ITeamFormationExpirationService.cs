namespace EHub.Application.Features.Teams.TeamFormations;

public interface ITeamFormationExpirationService
{
    /// <summary>Expires every overdue pending invitation. Returns the number of invitations expired.</summary>
    Task<int> ExpireDueAsync(CancellationToken cancellationToken = default);
}
