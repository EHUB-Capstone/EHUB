using System.Text.Json.Serialization;
using EHub.Contracts.Subjects;

namespace EHub.Contracts.Workspaces;

public sealed class WorkspaceCheckpointOverviewResponse
{
    public string SubjectCode { get; init; } = string.Empty;
    public IReadOnlyCollection<SubjectCheckpointResponse> Checkpoints { get; init; } =
        Array.Empty<SubjectCheckpointResponse>();
    public IReadOnlyCollection<WorkspaceCheckpointSubmissionResponse> Submissions { get; init; } =
        Array.Empty<WorkspaceCheckpointSubmissionResponse>();
    public IReadOnlyCollection<WorkspaceCheckpointFeedbackResponse> Feedbacks { get; init; } =
        Array.Empty<WorkspaceCheckpointFeedbackResponse>();
}

public sealed class WorkspaceCheckpointSubmissionResponse
{
    public int CheckpointNumber { get; init; }
    public string Status { get; init; } = string.Empty;
    public IReadOnlyCollection<WorkspaceCheckpointFileResponse> Files { get; init; } =
        Array.Empty<WorkspaceCheckpointFileResponse>();
    public IReadOnlyCollection<WorkspaceCheckpointRequirementContentResponse> RequirementContents { get; init; } =
        Array.Empty<WorkspaceCheckpointRequirementContentResponse>();
}

public sealed class WorkspaceCheckpointRequirementContentResponse
{
    public int Index { get; init; }
    public string Content { get; init; } = string.Empty;
}

public sealed class WorkspaceCheckpointFileResponse
{
    [JsonPropertyName("_id")]
    public Guid Id { get; init; }
    public string OriginalName { get; init; } = string.Empty;
    public string FileType { get; init; } = string.Empty;
    public long FileSize { get; init; }
    public DateTime UploadedAt { get; init; }
    public WorkspaceCheckpointUserResponse? UploadedBy { get; init; }
}

public sealed class WorkspaceCheckpointFeedbackResponse
{
    [JsonPropertyName("_id")]
    public Guid Id { get; init; }
    public int CheckpointNumber { get; init; }
    public string Comment { get; init; } = string.Empty;
    public Guid? ParentFeedbackId { get; init; }
    public DateTime CreatedAt { get; init; }
    public WorkspaceCheckpointUserResponse? User { get; init; }
}

public sealed class WorkspaceCheckpointUserResponse
{
    [JsonPropertyName("_id")]
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Role { get; init; } = string.Empty;
}
