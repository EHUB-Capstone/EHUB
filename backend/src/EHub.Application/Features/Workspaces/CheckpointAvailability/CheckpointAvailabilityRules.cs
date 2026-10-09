using EHub.Domain.Entities;
using EHub.Domain.Enums;

namespace EHub.Application.Features.Workspaces.CheckpointAvailability;

public sealed record CheckpointAvailabilityResult(string Status, string? Reason)
{
    public bool CanSubmit => string.Equals(Status, "Open", StringComparison.Ordinal);
}

public static class CheckpointAvailabilityRules
{
    public static CheckpointAvailabilityResult Evaluate(
        Checkpoint? schedule,
        bool previousCompleted,
        DateTime now)
    {
        if (schedule is null || schedule.OpenDate is null || schedule.DueDate is null)
        {
            return new("Closed", "Deadline has not been configured yet.");
        }

        if (schedule.Status == CheckpointStatus.Archived)
        {
            return new("Archived", "This checkpoint has been archived.");
        }

        if (now >= schedule.DueDate)
        {
            return new("Closed", "The deadline has passed.");
        }

        if (now < schedule.OpenDate)
        {
            return new("Closed", "This checkpoint has not opened yet.");
        }

        if (!previousCompleted)
        {
            return new("Locked", "Complete the previous checkpoint first.");
        }

        return new("Open", null);
    }
}
