using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace EHub.Contracts.Subjects;

public sealed class SubjectResponse
{
    [JsonPropertyName("_id")]
    public Guid Id { get; init; }
    public string SubjectCode { get; init; } = string.Empty;
    public string SubjectName { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
}

public sealed class SubjectListResponse
{
    public IReadOnlyCollection<SubjectResponse> Subjects { get; init; } = Array.Empty<SubjectResponse>();
}

public sealed class SemesterResponse
{
    public string Semester { get; init; } = string.Empty;
    public int Year { get; init; }
}

public sealed class CurrentSemesterResponse
{
    public SemesterResponse CurrentSemester { get; init; } = new();
    public IReadOnlyCollection<int> AvailableYears { get; init; } = Array.Empty<int>();
    public bool IsDecember { get; init; }
}

public sealed class TeachingAssignmentResponse
{
    [JsonPropertyName("_id")]
    public string Id { get; init; } = string.Empty;
    public string ClassCode { get; init; } = string.Empty;
    public string SubjectCode { get; init; } = string.Empty;
}

public sealed class TeachingStaffResponse
{
    [JsonPropertyName("_id")]
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string? Avatar { get; init; }
    public string Role { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public int ClassCount { get; init; }
    public IReadOnlyCollection<TeachingAssignmentResponse> Assignments { get; init; } = Array.Empty<TeachingAssignmentResponse>();
}

public sealed class TeachingStaffSummaryResponse
{
    public int Lecturers { get; init; }
    public int Mentors { get; init; }
    public int Assigned { get; init; }
    public int Unassigned { get; init; }
    public int Classes { get; init; }
}

public sealed class TeachingStaffListResponse
{
    public IReadOnlyCollection<TeachingStaffResponse> Staff { get; init; } = Array.Empty<TeachingStaffResponse>();
    public TeachingStaffSummaryResponse Summary { get; init; } = new();
}

public sealed class RoadmapItemResponse
{
    [JsonPropertyName("_id")]
    public Guid Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string TaskType { get; init; } = "COURSE_TEMPLATE";
    public string CourseCode { get; init; } = string.Empty;
    public int WeekNumber { get; init; }
    public string Priority { get; init; } = string.Empty;
    public decimal? EstimatedHours { get; init; }
    public IReadOnlyCollection<string> Tags { get; init; } = Array.Empty<string>();
}

public sealed class RubricCriterionResponse
{
    [JsonPropertyName("_id")]
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public decimal MaxScore { get; init; }
    public decimal Weight { get; init; }
    public int DisplayOrder { get; init; }
}

public sealed class CourseRubricResponse
{
    [JsonPropertyName("_id")]
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string Status { get; init; } = string.Empty;
    public decimal TotalWeight { get; init; }
    public int? CheckpointNumber { get; init; }
    public IReadOnlyCollection<RubricCriterionResponse> Criteria { get; init; } = Array.Empty<RubricCriterionResponse>();
}

public sealed class SubjectCurriculumResponse
{
    public SubjectResponse Subject { get; init; } = new();
    public IReadOnlyCollection<RoadmapItemResponse> RoadmapItems { get; init; } = Array.Empty<RoadmapItemResponse>();
    public IReadOnlyCollection<CourseRubricResponse> Rubrics { get; init; } = Array.Empty<CourseRubricResponse>();
    public IReadOnlyCollection<SubjectCheckpointResponse> Checkpoints { get; init; } = Array.Empty<SubjectCheckpointResponse>();
}

public sealed class SubjectCheckpointResponse
{
    public int Number { get; init; }
    public string Title { get; init; } = string.Empty;
    public string? ShortDescription { get; init; }
    public IReadOnlyCollection<string> Requirements { get; init; } = Array.Empty<string>();
    public IReadOnlyCollection<SubjectCriterionResponse> Rubrics { get; init; } = Array.Empty<SubjectCriterionResponse>();
}

public sealed class SubjectCriterionResponse
{
    public string Key { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
    public string? Description { get; init; }
    public decimal Weight { get; init; }
    public object[] Levels { get; init; } = Array.Empty<object>();
}
