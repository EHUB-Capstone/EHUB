namespace EHub.Domain.Enums;

public enum CheckpointSummaryStatus
{
    Pending,
    Processing,
    Ready,
    Failed,
    // A reviewer stopped a queued or running summary. Stored as text, so adding it needs no migration.
    Cancelled
}
