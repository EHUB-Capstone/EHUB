namespace EHub.Contracts.Subjects;

public sealed class CreateSubjectRequest
{
    public string SubjectCode { get; init; } = string.Empty;
    public string SubjectName { get; init; } = string.Empty;
    public string Status { get; init; } = "active";
}

public sealed class UpdateSubjectRequest
{
    public string SubjectName { get; init; } = string.Empty;
    public string Status { get; init; } = "active";
}

public sealed class SetCurrentSemesterRequest
{
    public string Semester { get; init; } = string.Empty;
    public int Year { get; init; }
}

public sealed class ChangeSemesterLifecycleRequest
{
    public string RowVersion { get; init; } = string.Empty;
    public string Reason { get; init; } = string.Empty;
}

public sealed class SaveRoadmapItemRequest
{
    public string Title { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string TaskType { get; init; } = "COURSE_TEMPLATE";
    public string CourseCode { get; init; } = string.Empty;
    public int WeekNumber { get; init; }
    public string Priority { get; init; } = "MEDIUM";
    public decimal? EstimatedHours { get; init; }
    public IReadOnlyCollection<string> Tags { get; init; } = Array.Empty<string>();
}

public sealed class SaveRubricRequest
{
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public int? CheckpointNumber { get; init; }
    public decimal TotalWeight { get; init; } = 100;
    public string Status { get; init; } = "DRAFT";
}

public sealed class SaveRubricCriterionRequest
{
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public decimal MaxScore { get; init; } = 10;
    public decimal Weight { get; init; }
    public int DisplayOrder { get; init; }
}

public sealed class SaveSubjectCheckpointsRequest
{
    public IReadOnlyCollection<SubjectCheckpointRequest> Checkpoints { get; init; } = Array.Empty<SubjectCheckpointRequest>();
}

public sealed class SubjectCheckpointRequest
{
    public int Number { get; init; }
    public string Title { get; init; } = string.Empty;
    public string? ShortDescription { get; init; }
    public IReadOnlyCollection<string> Requirements { get; init; } = Array.Empty<string>();
    public IReadOnlyCollection<SubjectCriterionRequest> Rubrics { get; init; } = Array.Empty<SubjectCriterionRequest>();
}

public sealed class SubjectCriterionRequest
{
    public string Key { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
    public string? Description { get; init; }
    public decimal Weight { get; init; }
    public object[] Levels { get; init; } = Array.Empty<object>();
}
