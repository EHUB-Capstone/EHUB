namespace EHub.Application.Features.Checkpoints.LecturerManagement;

public static class CheckpointDeadlineEvents
{
    public const string ScheduleChanged = "Checkpoint.ScheduleChanged.v1";
    public const string DeadlineReminder = "Checkpoint.DeadlineReminder.v1";
    public const string DeadlineOverdue = "Checkpoint.DeadlineOverdue.v1";
    public const string DeadlineExtensionRequested = "Checkpoint.DeadlineExtensionRequested.v1";
    public static readonly TimeSpan ReminderLeadTime = TimeSpan.FromHours(24);
}
