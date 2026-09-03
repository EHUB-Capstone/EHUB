using System.Text.Json.Serialization;

namespace EHub.Contracts.Workspaces;

public sealed class WeeklyTaskQuery
{
    public string CourseCode { get; init; } = string.Empty;
    public int WeekNumber { get; init; } = 1;
    public Guid? ClassId { get; init; }
    public Guid? TeamId { get; init; }
    public string? Status { get; init; }
    public Guid? AssigneeStudentId { get; init; }
}

public sealed class SaveWeeklyTaskRequest
{
    public string Title { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string TaskType { get; init; } = "TEAM_TASK";
    public string Scope { get; init; } = "TEAM";
    public int WeekNumber { get; init; }
    public string CourseCode { get; init; } = string.Empty;
    public Guid? ClassId { get; init; }
    public Guid? TeamId { get; init; }
    public Guid? AssigneeStudentId { get; init; }
    public string Status { get; init; } = "TODO";
    public string Priority { get; init; } = "MEDIUM";
    public DateTime? StartDate { get; init; }
    public DateTime? DueDate { get; init; }
    public IReadOnlyCollection<WeeklyTaskAttachmentDto> Attachments { get; init; } = Array.Empty<WeeklyTaskAttachmentDto>();
    public IReadOnlyCollection<WeeklyTaskChecklistItemDto> Checklist { get; init; } = Array.Empty<WeeklyTaskChecklistItemDto>();
    public IReadOnlyCollection<string> Tags { get; init; } = Array.Empty<string>();
    public bool IsMandatory { get; init; }
    public bool VisibleToStudents { get; init; } = true;
    public decimal? EstimatedHours { get; init; }
}

public sealed class UpdateWeeklyTaskStatusRequest
{
    public string Status { get; init; } = string.Empty;
    public IReadOnlyCollection<WeeklyTaskChecklistItemDto>? Checklist { get; init; }
}

public sealed class WeeklyTaskAttachmentDto
{
    public string Name { get; init; } = string.Empty;
    public string Url { get; init; } = string.Empty;
}

public sealed class WeeklyTaskChecklistItemDto
{
    public string Text { get; init; } = string.Empty;
    public bool IsCompleted { get; init; }
}

public sealed class WeeklyTaskCreatorDto
{
    [JsonPropertyName("_id")]
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Avatar { get; init; }
}

public sealed class WeeklyTaskAssigneeDto
{
    [JsonPropertyName("_id")]
    public Guid Id { get; init; }
    public string FullName { get; init; } = string.Empty;
    public string RollNumber { get; init; } = string.Empty;
}

public sealed class WeeklyTaskDto
{
    [JsonPropertyName("_id")]
    public Guid Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string TaskType { get; init; } = string.Empty;
    public string Scope { get; init; } = string.Empty;
    public int WeekNumber { get; init; }
    public string CourseCode { get; init; } = string.Empty;
    public Guid? ClassId { get; init; }
    public Guid? TeamId { get; init; }
    public WeeklyTaskAssigneeDto? AssigneeStudentId { get; init; }
    public string Status { get; init; } = string.Empty;
    public string Priority { get; init; } = string.Empty;
    public DateTime? StartDate { get; init; }
    public DateTime? DueDate { get; init; }
    public string AttachmentsJson { get; init; } = "[]";
    public string ChecklistJson { get; init; } = "[]";
    public IReadOnlyCollection<WeeklyTaskAttachmentDto> Attachments { get; init; } = Array.Empty<WeeklyTaskAttachmentDto>();
    public IReadOnlyCollection<WeeklyTaskChecklistItemDto> Checklist { get; init; } = Array.Empty<WeeklyTaskChecklistItemDto>();
    public IReadOnlyCollection<string> Tags { get; init; } = Array.Empty<string>();
    public bool IsTemplate { get; init; }
    public bool IsMandatory { get; init; }
    public bool VisibleToStudents { get; init; }
    public int CompletionPercentage { get; init; }
    public decimal? EstimatedHours { get; init; }
    public WeeklyTaskCreatorDto CreatedBy { get; init; } = new();
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
}

public sealed class WeeklyTaskBoardDto
{
    public IReadOnlyCollection<WeeklyTaskDto> CourseTasks { get; init; } = Array.Empty<WeeklyTaskDto>();
    public IReadOnlyCollection<WeeklyTaskDto> ClassTasks { get; init; } = Array.Empty<WeeklyTaskDto>();
    public IReadOnlyCollection<WeeklyTaskDto> TeamTasks { get; init; } = Array.Empty<WeeklyTaskDto>();
}

public sealed class SaveShortcutRequest
{
    public string Name { get; init; } = string.Empty;
    public string Url { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string ShortcutType { get; init; } = "OTHER";
}

public sealed class ShortcutCreatorDto
{
    [JsonPropertyName("_id")]
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Avatar { get; init; }
}

public sealed class ProjectShortcutDto
{
    [JsonPropertyName("_id")]
    public Guid Id { get; init; }
    public Guid TeamId { get; init; }
    public Guid ProjectId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Url { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string ShortcutType { get; init; } = string.Empty;
    public ShortcutCreatorDto CreatedBy { get; init; } = new();
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
}
