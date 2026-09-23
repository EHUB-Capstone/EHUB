using System;
using System.Collections.Generic;

namespace EHub.Contracts.Checkpoints;

public sealed class GetLecturerCheckpointsRequest
{
    public string? Semester { get; init; }
    public int? Year { get; init; }
    public string? SubjectCode { get; init; }
    public string? Status { get; init; }
    public string? Search { get; init; }
    public Guid? ClassId { get; init; }
    public Guid? CheckpointId { get; init; }
    public int? CheckpointNumber { get; init; }
}

public sealed class SaveClassCheckpointScheduleRequest
{
    public DateTime? StartDateUtc { get; init; }
    public DateTime? EndDateUtc { get; init; }
}

public sealed class BulkSaveClassCheckpointScheduleRequest
{
    public int CheckpointNumber { get; init; }
    public string? Semester { get; init; }
    public int? Year { get; init; }
    public string? SubjectCode { get; init; }
    public string? Status { get; init; }
    public string? Search { get; init; }
    public IReadOnlyCollection<Guid> ExpectedClassIds { get; init; } = Array.Empty<Guid>();
    public DateTime? StartDateUtc { get; init; }
    public DateTime? EndDateUtc { get; init; }
}

public sealed class BulkClassCheckpointScheduleResponse
{
    public int AppliedClassCount { get; init; }
    public IReadOnlyCollection<ClassCheckpointScheduleResponse> Schedules { get; init; } =
        Array.Empty<ClassCheckpointScheduleResponse>();
}

public sealed class LecturerCheckpointOverviewResponse
{
    public DateTime ServerTimeUtc { get; init; }
    public IReadOnlyCollection<LecturerCheckpointClassResponse> Classes { get; init; } =
        Array.Empty<LecturerCheckpointClassResponse>();
    public IReadOnlyCollection<LecturerCheckpointDefinitionResponse> Checkpoints { get; init; } =
        Array.Empty<LecturerCheckpointDefinitionResponse>();
    public IReadOnlyCollection<ClassCheckpointScheduleResponse> Schedules { get; init; } =
        Array.Empty<ClassCheckpointScheduleResponse>();
    public IReadOnlyCollection<LecturerCheckpointSubmissionResponse> Submissions { get; init; } =
        Array.Empty<LecturerCheckpointSubmissionResponse>();
}

public sealed class LecturerCheckpointClassResponse
{
    public Guid Id { get; init; }
    public string ClassCode { get; init; } = string.Empty;
    public string SubjectCode { get; init; } = string.Empty;
    public string SubjectName { get; init; } = string.Empty;
    public string SemesterCode { get; init; } = string.Empty;
    public int Year { get; init; }
}

public sealed class LecturerCheckpointDefinitionResponse
{
    public Guid Id { get; init; }
    public Guid CourseId { get; init; }
    public int Number { get; init; }
    public string Title { get; init; } = string.Empty;
    public string? ShortDescription { get; init; }
}

public sealed class ClassCheckpointScheduleResponse
{
    public Guid? Id { get; init; }
    public Guid ClassId { get; init; }
    public string ClassCode { get; init; } = string.Empty;
    public Guid CheckpointId { get; init; }
    public int CheckpointNumber { get; init; }
    public string CheckpointTitle { get; init; } = string.Empty;
    public DateTime? StartDateUtc { get; init; }
    public DateTime? EndDateUtc { get; init; }
    public string Status { get; init; } = string.Empty;
    public bool CanReopen { get; init; }
    public int ReopenCount { get; init; }
}

public sealed class LecturerCheckpointSubmissionResponse
{
    public Guid ClassId { get; init; }
    public string ClassCode { get; init; } = string.Empty;
    public Guid TeamId { get; init; }
    public string TeamName { get; init; } = string.Empty;
    public Guid CheckpointId { get; init; }
    public int CheckpointNumber { get; init; }
    public string CheckpointTitle { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public DateTime? LatestSubmissionAtUtc { get; init; }
    public LecturerCheckpointFileResponse? EarliestSubmittedFile { get; init; }
}

public sealed class LecturerCheckpointFileResponse
{
    public Guid Id { get; init; }
    public string OriginalName { get; init; } = string.Empty;
    public DateTime UploadedAtUtc { get; init; }
}
