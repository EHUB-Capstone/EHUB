namespace EHub.Contracts.Workspaces;

public sealed class CreateWorkspaceCheckpointFeedbackRequest
{
    public string Comment { get; init; } = string.Empty;
    public Guid? ParentFeedbackId { get; init; }
}
