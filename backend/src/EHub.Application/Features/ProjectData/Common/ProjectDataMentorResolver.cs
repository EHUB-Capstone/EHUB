using System.Linq.Expressions;
using EHub.Domain.Entities;
using EHub.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace EHub.Application.Features.ProjectData.Common;

internal sealed record ProjectDataMentorRow(
    Guid TeamId,
    MentorType Slot,
    Guid AssignmentId,
    Guid MentorProfileId,
    Guid UserId,
    string FullName,
    DateTime AssignedAtUtc,
    DateTime? EndedAtUtc,
    MentorAssignmentStatus Status);

/// <summary>
/// Single definition of which mentor assignment is shown for a team slot, shared by the list
/// projection, search and filters so they always agree.
/// </summary>
internal static class ProjectDataMentorResolver
{
    /// <summary>
    /// An assignment is displayed when it is currently active, or when it was closed by class
    /// completion (EndedAt equals the class completion time). Assignments ended earlier, pending
    /// or cancelled ones never show; a mentor removed mid-semester must not appear as the final mentor.
    /// </summary>
    internal static readonly Expression<Func<MentorAssignment, bool>> IsDisplayed = assignment =>
        assignment.Status == MentorAssignmentStatus.Active && assignment.EndedAt == null ||
        assignment.Status == MentorAssignmentStatus.Ended &&
        assignment.EndedAt != null &&
        assignment.EndedAt == assignment.Team.Class.CompletedAtUtc &&
        (assignment.Team.Class.Status == ClassStatus.Completed ||
         assignment.Team.Class.Status == ClassStatus.Archived &&
         assignment.Team.Class.StatusBeforeArchive == ClassStatus.Completed);

    /// <summary>True when the displayed Enterprise or Academic mentor of the project's team is this user.</summary>
    internal static Expression<Func<Project, bool>> HasDisplayedMentor(Guid userId) =>
        project =>
            project.Team.MentorAssignments.AsQueryable()
                .Where(IsDisplayed)
                .Where(assignment => assignment.Slot == MentorType.Enterprise)
                .OrderByDescending(assignment => assignment.AssignedAt)
                .ThenBy(assignment => assignment.Id)
                .Select(assignment => (Guid?)assignment.MentorProfile.UserId)
                .FirstOrDefault() == userId ||
            project.Team.MentorAssignments.AsQueryable()
                .Where(IsDisplayed)
                .Where(assignment => assignment.Slot == MentorType.Academic)
                .OrderByDescending(assignment => assignment.AssignedAt)
                .ThenBy(assignment => assignment.Id)
                .Select(assignment => (Guid?)assignment.MentorProfile.UserId)
                .FirstOrDefault() == userId;

    /// <summary>True when the displayed Enterprise or Academic mentor's name contains the (lower-cased) term.</summary>
    internal static Expression<Func<Project, bool>> HasDisplayedMentorNamed(string term) =>
        project =>
            project.Team.MentorAssignments.AsQueryable()
                .Where(IsDisplayed)
                .Where(assignment => assignment.Slot == MentorType.Enterprise)
                .OrderByDescending(assignment => assignment.AssignedAt)
                .ThenBy(assignment => assignment.Id)
                .Select(assignment => assignment.MentorProfile.User.FullName)
                .FirstOrDefault()!.ToLower().Contains(term) ||
            project.Team.MentorAssignments.AsQueryable()
                .Where(IsDisplayed)
                .Where(assignment => assignment.Slot == MentorType.Academic)
                .OrderByDescending(assignment => assignment.AssignedAt)
                .ThenBy(assignment => assignment.Id)
                .Select(assignment => assignment.MentorProfile.User.FullName)
                .FirstOrDefault()!.ToLower().Contains(term);

    /// <summary>Loads the displayed mentor of each slot for the given teams (one row per team and slot).</summary>
    internal static async Task<IReadOnlyDictionary<(Guid TeamId, MentorType Slot), ProjectDataMentorRow>> LoadAsync(
        IQueryable<MentorAssignment> assignments,
        CancellationToken cancellationToken)
    {
        var rows = await assignments
            .Where(IsDisplayed)
            .Select(assignment => new ProjectDataMentorRow(
                assignment.TeamId,
                assignment.Slot,
                assignment.Id,
                assignment.MentorProfileId,
                assignment.MentorProfile.UserId,
                assignment.MentorProfile.User.FullName,
                assignment.AssignedAt,
                assignment.EndedAt,
                assignment.Status))
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(row => (row.TeamId, row.Slot))
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderByDescending(row => row.AssignedAtUtc)
                    .ThenBy(row => row.AssignmentId)
                    .First());
    }
}
