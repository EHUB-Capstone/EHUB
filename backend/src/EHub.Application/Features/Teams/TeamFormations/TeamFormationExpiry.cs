using EHub.Application.Common.Interfaces.Persistence;
using EHub.Application.Features.Classes.Common;
using EHub.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace EHub.Application.Features.Teams.TeamFormations;

internal static class TeamFormationExpiry
{
    /// <summary>
    /// Marks overdue Pending invitations of Pending formations as Expired, releases their
    /// reservation and enqueues one outbox event per formation. The caller saves the context.
    /// Idempotent: already expired records are no longer selected.
    /// </summary>
    public static async Task<int> ApplyAsync(
        IApplicationDbContext context,
        DateTime now,
        Guid? classId,
        Guid? formationId,
        int batchSize,
        CancellationToken cancellationToken)
    {
        var query = context.TeamFormationInvitations.Where(item =>
            item.Status == TeamInvitationStatus.Pending &&
            item.ReservationReleasedAtUtc == null &&
            item.ExpiresAtUtc != null && item.ExpiresAtUtc <= now &&
            item.Formation.Status == TeamFormationStatus.Pending);
        if (classId.HasValue) query = query.Where(item => item.ClassId == classId.Value);
        if (formationId.HasValue) query = query.Where(item => item.FormationId == formationId.Value);

        var due = await query.OrderBy(item => item.ExpiresAtUtc).Take(batchSize).ToListAsync(cancellationToken);
        if (due.Count == 0) return 0;

        foreach (var invitation in due)
        {
            invitation.Status = TeamInvitationStatus.Expired;
            invitation.ReservationReleasedAtUtc = now;
        }

        var formationIds = due.Select(item => item.FormationId).Distinct().ToArray();
        var formations = await context.TeamFormations.AsNoTracking()
            .Where(item => formationIds.Contains(item.Id))
            .Select(item => new { item.Id, item.ClassId, item.CreatorStudentId })
            .ToListAsync(cancellationToken);
        foreach (var formation in formations)
        {
            ClassOutbox.Enqueue(context, "TeamFormation.InvitationExpired.v1", formation.ClassId, new
            {
                FormationId = formation.Id,
                CreatorStudentId = formation.CreatorStudentId,
                StudentIds = due.Where(item => item.FormationId == formation.Id)
                    .Select(item => item.StudentId).Distinct().ToArray()
            }, now);
        }

        return due.Count;
    }
}
