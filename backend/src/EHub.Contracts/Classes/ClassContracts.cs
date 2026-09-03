using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace EHub.Contracts.Classes;

public sealed class GetClassesRequest
{
    public Guid? SemesterId { get; init; }
    public Guid? CourseId { get; init; }
    public string? SemesterCode { get; init; }
    public string? SubjectCode { get; init; }
    public int? Year { get; init; }
    public string? Status { get; init; }
    public string? AssignmentStatus { get; init; }
    public string? Search { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 10;
    public string Sort { get; init; } = "code";
}

public sealed class ClassResponse
{
    public Guid Id { get; init; }
    public string Slug { get; init; } = string.Empty;
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
    // Default room. A schedule slot may override it with its own Room value.
    public string? Room { get; init; }
    public IReadOnlyCollection<ClassScheduleSlotDto> Schedules { get; init; } = Array.Empty<ClassScheduleSlotDto>();
    public bool IsEnrollmentMajorLocked { get; init; }
    public string Status { get; init; } = string.Empty;
    public string? StatusBeforeArchive { get; init; }
    public int StudentCount { get; init; }
    public int TeamCount { get; init; }
    public IReadOnlyCollection<ClassMentorSummaryDto> Mentors { get; init; } = Array.Empty<ClassMentorSummaryDto>();
    public DateTime CreatedAtUtc { get; init; }
    public DateTime? CompletedAtUtc { get; init; }
    public string? CompletionReason { get; init; }
    public string RowVersion { get; init; } = string.Empty;
}

public sealed class ClassMentorSummaryDto
{
    public Guid MentorProfileId { get; init; }
    public Guid UserId { get; init; }
    public string FullName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
}

public sealed class ClassListResponse
{
    public IReadOnlyCollection<ClassResponse> Items { get; init; } = Array.Empty<ClassResponse>();
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalPages { get; init; }
}

public sealed class CreateClassRequest
{
    public Guid CourseId { get; init; }
    public Guid SemesterId { get; init; }
    public int ClassIndex { get; init; }
    public Guid? PrimaryLecturerId { get; init; }
    public string? Room { get; init; }
}

public sealed class CreateBulkClassesRequest
{
    public Guid CourseId { get; init; }
    public Guid SemesterId { get; init; }
    public string? SubjectCode { get; init; }
    public string? Semester { get; init; }
    public int? Year { get; init; }
    public int StartClassIndex { get; init; } = 1;
    public int Quantity { get; init; } = 1;
    public IReadOnlyCollection<int>? ClassIndices { get; init; }
    // Backward-compatible shortcut: assign one lecturer to the whole batch.
    public Guid? PrimaryLecturerId { get; init; }
    public IReadOnlyCollection<BulkClassLecturerAssignmentRequest>? LecturerAssignments { get; init; }
}

public sealed class BulkClassLecturerAssignmentRequest
{
    public Guid LecturerId { get; init; }
    public IReadOnlyCollection<int> ClassIndices { get; init; } = Array.Empty<int>();
}

public sealed class BulkClassPreviewItem
{
    public string ClassCode { get; init; } = string.Empty;
    public int ClassIndex { get; init; }
    public string SubjectCode { get; init; } = string.Empty;
    public string SemesterCode { get; init; } = string.Empty;
    public Guid? PrimaryLecturerId { get; init; }
    public string? PrimaryLecturerName { get; init; }
    public bool IsValid { get; init; }
    public string? ErrorMessage { get; init; }
}

public sealed class BulkClassPreviewResponse
{
    public IReadOnlyCollection<BulkClassPreviewItem> Items { get; init; } = Array.Empty<BulkClassPreviewItem>();
    public int TotalCount { get; init; }
    public int ValidCount { get; init; }
    public int InvalidCount { get; init; }
}

public sealed class UpdateClassRequest
{
    private string? _room;

    public string? Room
    {
        get => _room;
        init
        {
            _room = value;
            IsRoomSpecified = true;
        }
    }

    [JsonIgnore]
    public bool IsRoomSpecified { get; private set; }

    public string RowVersion { get; init; } = string.Empty;
}

public sealed class ClassScheduleSlotDto
{
    public DayOfWeek DayOfWeek { get; init; }
    public int SlotNumber { get; init; }
    // Null means use the class default Room.
    public string? Room { get; init; }
}

public sealed class UpdateClassScheduleRequest
{
    // Null/missing is invalid; an explicit empty array intentionally clears the schedule.
    public IReadOnlyCollection<ClassScheduleSlotDto>? Schedules { get; init; }
    public string RowVersion { get; init; } = string.Empty;
}

public sealed class UpdateTeachingAssignmentRequest
{
    private Guid? _primaryLecturerId;

    public Guid? PrimaryLecturerId
    {
        get => _primaryLecturerId;
        init
        {
            _primaryLecturerId = value;
            IsPrimaryLecturerIdSpecified = true;
        }
    }

    [JsonIgnore]
    public bool IsPrimaryLecturerIdSpecified { get; private set; }

    public string RowVersion { get; init; } = string.Empty;
}

public sealed class ClassStudentDto
{
    public Guid StudentId { get; init; }
    public string RollNumber { get; init; } = string.Empty;
    public string FullName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string? MajorCode { get; init; }
    public string? ProfileMajorCode { get; init; }
    public string MajorVerificationStatus { get; init; } = "Unverified";
    public string? MemberCode { get; init; }
    public string EnrollmentStatus { get; init; } = string.Empty;
    public Guid? TeamId { get; init; }
    public string? TeamName { get; init; }
    public bool IsTeamLeader { get; init; }
    public DateTime JoinedAtUtc { get; init; }
}

public sealed class GetClassRosterRequest
{
    public string? Search { get; init; }
    public string? MajorCode { get; init; }
    public string? Status { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}

public sealed class ClassRosterListResponse
{
    public IReadOnlyCollection<ClassStudentDto> Items { get; init; } = Array.Empty<ClassStudentDto>();
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalPages { get; init; }
}

public sealed class AddStudentToClassRequest
{
    public string StudentCode { get; init; } = string.Empty;
    public string FullName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    // Optional for an existing student profile. When omitted, the enrollment
    // uses the major already stored on that profile.
    public string? MajorCode { get; init; }
}

/// <summary>
/// Assigns existing student profiles to a class. Each identifier may be either
/// the student-profile id or its linked user id, which keeps the API compatible
/// with staff directory pickers while persisting only student-profile ids.
/// </summary>
public sealed class AssignStudentsToClassRequest
{
    public IReadOnlyCollection<Guid> StudentIds { get; init; } = Array.Empty<Guid>();
}

public sealed class AssignStudentsToTeamRequest
{
    public IReadOnlyCollection<Guid> StudentIds { get; init; } = Array.Empty<Guid>();
}

public sealed class ClassStudentAssignmentResponse
{
    public Guid ClassId { get; init; }
    public IReadOnlyCollection<Guid> AssignedStudentIds { get; init; } = Array.Empty<Guid>();
}

public sealed class TeamStudentAssignmentResponse
{
    public Guid ClassId { get; init; }
    public Guid TeamId { get; init; }
    public IReadOnlyCollection<Guid> AssignedStudentIds { get; init; } = Array.Empty<Guid>();
}

public sealed class UpdateClassStudentRequest
{
    public string MajorCode { get; init; } = string.Empty;
    public string Reason { get; init; } = string.Empty;
}

public sealed class ExportClassRosterRequest
{
    public string Scope { get; init; } = "Active";
    public string? Search { get; init; }
    public string? MajorCode { get; init; }
    public string? Status { get; init; }
}

public sealed class EnrollmentMajorLockResponse
{
    public Guid ClassId { get; init; }
    public bool IsLocked { get; init; }
}

public sealed class ChangeClassLifecycleRequest
{
    public string RowVersion { get; init; } = string.Empty;
    public string Reason { get; init; } = string.Empty;
}

public sealed class ClassLifecycleResponse
{
    public Guid ClassId { get; init; }
    public string Status { get; init; } = string.Empty;
    public DateTime? CompletedAtUtc { get; init; }
    public DateTime? ArchivedAtUtc { get; init; }
    public string RowVersion { get; init; } = string.Empty;
}

public sealed class ClassCompletionPreviewResponse
{
    public Guid ClassId { get; init; }
    public string ClassCode { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public int ActiveEnrollmentCount { get; init; }
    public int DroppedEnrollmentCount { get; init; }
    public int ActiveMentorAssignmentCount { get; init; }
    public int OpenTeamProposalCount { get; init; }
    public int OpenProjectDirectionCount { get; init; }
    public int ProcessingImportSessionCount { get; init; }
    public int ScheduledMentoringSessionCount { get; init; }
    public IReadOnlyCollection<string> Blockers { get; init; } = Array.Empty<string>();
    public IReadOnlyCollection<string> Warnings { get; init; } = Array.Empty<string>();
    public string RowVersion { get; init; } = string.Empty;
}

public sealed class ClassAuditLogDto
{
    public Guid Id { get; init; }
    public string Action { get; init; } = string.Empty;
    public Guid PerformedByUserId { get; init; }
    public string PerformedByName { get; init; } = string.Empty;
    public DateTime OccurredAtUtc { get; init; }
    public string? DetailsJson { get; init; }
}

public sealed class ClassAuditLogListResponse
{
    public IReadOnlyCollection<ClassAuditLogDto> Items { get; init; } = Array.Empty<ClassAuditLogDto>();
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalPages { get; init; }
}

public sealed class ChatMembershipSyncResponse
{
    public Guid ClassId { get; init; }
    public int GroupsCreated { get; init; }
    public int MembershipsAdded { get; init; }
    public int MembershipsReactivated { get; init; }
    public int MembershipsEnded { get; init; }
    public bool IsReadOnly { get; init; }
}

public sealed class MajorVerificationRowDto
{
    public int? RowNumber { get; init; }
    public Guid? StudentId { get; init; }
    public string RollNumber { get; init; } = string.Empty;
    public string FullName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string? MajorInFile { get; init; }
    public string? MajorInDb { get; init; }
    public string Status { get; init; } = string.Empty;
    public string? Message { get; init; }
}

public sealed class VerifyClassMajorsResponse
{
    public IReadOnlyCollection<MajorVerificationRowDto> Matched { get; init; } = Array.Empty<MajorVerificationRowDto>();
    public IReadOnlyCollection<MajorVerificationRowDto> Mismatched { get; init; } = Array.Empty<MajorVerificationRowDto>();
    public IReadOnlyCollection<MajorVerificationRowDto> Missing { get; init; } = Array.Empty<MajorVerificationRowDto>();
    public IReadOnlyCollection<MajorVerificationRowDto> NotFound { get; init; } = Array.Empty<MajorVerificationRowDto>();
}

public sealed class SynchronizeProfileMajorsResponse
{
    public int MismatchCount { get; init; }
    public int SynchronizedCount { get; init; }
}

// ─── GIAI ĐOẠN 5: EXCEL IMPORT & EXPORT CONTRACTS ──────────────────────────────

public sealed class ImportStudentRowPreviewDto
{
    public int RowNumber { get; init; }
    public string StudentCode { get; init; } = string.Empty;
    public string FullName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string MajorCode { get; init; } = string.Empty;
    public string? RegisteredMajorCode { get; init; }
    public string MajorComparisonStatus { get; init; } = "NotCompared";
    public string? MajorWarningMessage { get; init; }
    public bool NeedsMajorSync { get; init; }
    public bool IsValid { get; init; }
    public string Status { get; init; } = "Valid";
    public string? ErrorMessage { get; init; }
}

public sealed class ImportStudentsPreviewResponse
{
    public Guid SessionId { get; init; }
    public int TotalRows { get; init; }
    public int ValidRowsCount { get; init; }
    public int ErrorRowsCount { get; init; }
    public int MajorMismatchCount { get; init; }
    public IReadOnlyCollection<ImportStudentRowPreviewDto> Rows { get; init; } = Array.Empty<ImportStudentRowPreviewDto>();
}

public sealed class CommitImportStudentsRequest
{
    public Guid SessionId { get; init; }
    public bool SynchronizeProfileMajors { get; init; }
}

public sealed class ImportStudentsCommitResponse
{
    public int InsertedCount { get; init; }
    public int UpdatedCount { get; init; }
    public int SkippedCount { get; init; }
    public int ErrorCount { get; init; }
    public int SynchronizedMajorCount { get; init; }
    public IReadOnlyCollection<ImportStudentCommitErrorDto> Errors { get; init; } = Array.Empty<ImportStudentCommitErrorDto>();
}

public sealed class ImportStudentCommitErrorDto
{
    public int RowNumber { get; init; }
    public string StudentCode { get; init; } = string.Empty;
    public string ErrorCode { get; init; } = string.Empty;
    public string ErrorMessage { get; init; } = string.Empty;
}
