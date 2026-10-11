using EHub.Contracts.Teams;
using EHub.Domain.Entities;
using EHub.Domain.Enums;

namespace EHub.Application.Features.Teams.Common;

// Everything that shows or counts a mentor without an account goes through here, so they are labelled the same way.
internal static class TemporaryMentors
{
    public const string UiSuffix = " (no email yet)";
    public const string ExportSuffix = " (chưa có email)";

    public static string UiName(MentorImportDraft draft) => draft.FullName + UiSuffix;
    public static string ExportName(MentorImportDraft draft) => draft.FullName + ExportSuffix;

    // A temporary assignment counts while it is active, or when it ended together with its class (same rule as real ones).
    public static bool IsInEffect(TemporaryMentorAssignment assignment, DateTime? classCompletedAtUtc) =>
        (assignment.Status == MentorAssignmentStatus.Active && assignment.EndedAt == null) ||
        (classCompletedAtUtc != null && assignment.Status == MentorAssignmentStatus.Ended &&
         assignment.EndedAt != null && assignment.EndedAt >= classCompletedAtUtc);

    public static MentorAssignmentDto ToDto(TemporaryMentorAssignment assignment) => new()
    {
        AssignmentId = assignment.Id,
        TeamId = assignment.TeamId,
        TeamName = assignment.Team?.TeamName ?? string.Empty,
        ClassId = assignment.Team?.ClassId ?? Guid.Empty,
        Mentor = new MentorSummaryDto
        {
            MentorProfileId = assignment.DraftId,
            UserId = Guid.Empty,
            FullName = UiName(assignment.Draft),
            Email = assignment.Draft.Email ?? string.Empty,
            Organization = assignment.Draft.Organization,
            MentorType = assignment.Draft.Type.ToString(),
            Department = assignment.Draft.Department,
            JobTitle = assignment.Draft.JobTitle,
            ContractType = assignment.Draft.ContractType,
            IsTemporary = true
        },
        Status = assignment.Status.ToString(),
        AssignedAtUtc = assignment.AssignedAt,
        EndedAtUtc = assignment.EndedAt,
        Note = assignment.Note,
        Slot = assignment.Slot.ToString()
    };
}
