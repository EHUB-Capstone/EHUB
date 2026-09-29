namespace EHub.Contracts.Workspaces;

public sealed class SaveWorkspaceCheckpointLinkRequest
{
    public string Name { get; init; } = string.Empty;
    public string Url { get; init; } = string.Empty;
}

public sealed class CreateWorkspaceCheckpointFeedbackRequest
{
    public string Comment { get; init; } = string.Empty;
    public Guid? ParentFeedbackId { get; init; }
}
