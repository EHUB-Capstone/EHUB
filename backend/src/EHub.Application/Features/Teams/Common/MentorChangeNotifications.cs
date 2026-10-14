using EHub.Application.Common.Interfaces.Persistence;
using EHub.Application.Features.Classes.Common;

namespace EHub.Application.Features.Teams.Common;

// One event for every time a team loses a mentor (replaced or removed). The notification dispatcher turns it into
// a message for the lecturer of the class and for the former mentor when that mentor has an account.
internal static class MentorChangeNotifications
{
    public const string EventType = "Team.MentorChanged.v1";

    public static void Enqueue(
        IApplicationDbContext context,
        Guid classId,
        Guid teamId,
        string teamCode,
        Guid? previousMentorUserId,
        string previousMentorName,
        string? newMentorName,
        string reason,
        Guid actorUserId,
        DateTime now)
    {
        ClassOutbox.Enqueue(context, EventType, classId, new
        {
            TeamId = teamId,
            TeamCode = teamCode,
            PreviousMentorUserId = previousMentorUserId,
            PreviousMentorName = Clean(previousMentorName),
            NewMentorName = newMentorName is null ? null : Clean(newMentorName),
            Reason = reason,
            ActorUserId = actorUserId
        }, now);
    }

    // Mentors without an account carry a display suffix in the allocation screens; it does not belong in a message.
    private static string Clean(string name) => name.Replace(TemporaryMentors.UiSuffix, string.Empty).Trim();
}
