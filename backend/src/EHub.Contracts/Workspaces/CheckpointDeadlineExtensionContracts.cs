namespace EHub.Contracts.Workspaces;

public sealed class CreateCheckpointDeadlineExtensionRequest
{
    public string Reason { get; init; } = string.Empty;
}

public sealed class CheckpointDeadlineExtensionRequestResponse
{
    public Guid Id { get; init; }
    public Guid TeamId { get; init; }
    public string TeamName { get; init; } = string.Empty;
    public Guid ClassId { get; init; }
    public string ClassCode { get; init; } = string.Empty;
    public Guid CheckpointId { get; init; }
    public int CheckpointNumber { get; init; }
    public string CheckpointTitle { get; init; } = string.Empty;
    public DateTime DeadlineUtc { get; init; }
    public DateTime RequestedAtUtc { get; init; }
    public string Reason { get; init; } = string.Empty;
}
