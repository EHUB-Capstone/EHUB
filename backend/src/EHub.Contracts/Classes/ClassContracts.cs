using System;
using System.Collections.Generic;

namespace EHub.Contracts.Classes;

public sealed class GetClassesRequest
{
    public Guid? SemesterId { get; init; }
    public Guid? CourseId { get; init; }
    public string? SemesterCode { get; init; }
    public string? SubjectCode { get; init; }
    public int? Year { get; init; }
    public string? Status { get; init; }
    public string? Search { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 10;
    public string Sort { get; init; } = "code";
}

public sealed class ClassResponse
{
    public Guid Id { get; init; }
    public string ClassCode { get; init; } = string.Empty;
    public int ClassIndex { get; init; }
    public Guid CourseId { get; init; }
    public string SubjectCode { get; init; } = string.Empty;
    public string SubjectName { get; init; } = string.Empty;
    public Guid SemesterId { get; init; }
    public string SemesterCode { get; init; } = string.Empty;
    public int Year { get; init; }
    public Guid? PrimaryLecturerId { get; init; }
    public string? PrimaryLecturerName { get; init; }
    public string? PrimaryLecturerEmail { get; init; }
    public string? Room { get; init; }
    public string? ScheduleJson { get; init; }
    public bool IsEnrollmentMajorLocked { get; init; }
    public string Status { get; init; } = string.Empty;
    public int StudentCount { get; init; }
    public int TeamCount { get; init; }
    public DateTime CreatedAtUtc { get; init; }
}

public sealed class ClassListResponse
{
    public IReadOnlyCollection<ClassResponse> Items { get; init; } = Array.Empty<ClassResponse>();
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalPages { get; init; }
}
