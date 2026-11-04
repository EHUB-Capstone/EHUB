using EHub.Domain.Enums;

namespace EHub.Application.Features.ProjectData.ExportProjectData;

/// <summary>One competition a project entered, as the report shows it.</summary>
internal sealed record ExportCompetition(string Competition, string Name, int Year, CompetitionStage Stage);

/// <summary>One project (group) with everything the report says about it.</summary>
internal sealed record ExportProject(
    Guid ProjectId,
    string SemesterCode,
    int SemesterYear,
    SemesterTerm Term,
    string SubjectCode,
    Guid ClassId,
    string ClassCode,
    string ProjectName,
    string? Description,
    string Groups,
    IReadOnlyList<string> Industries,
    string Lecturer,
    string Mentor,
    string AcademicMentor,
    int MemberCount,
    IReadOnlyList<string> Majors,
    bool IsPotential,
    bool IsInternallyFunded,
    bool IsExternallyFunded,
    bool IsAwarded,
    bool IsPitching,
    bool IsDemo,
    string? AchievementNote,
    DateTime? AchievementsUpdatedAtUtc,
    string? AchievementsUpdatedBy,
    decimal? Score,
    IReadOnlyList<ExportCompetition> Competitions);

/// <summary>One competition entry of a project, with what the competition sheets show about the project.</summary>
internal sealed record ProjectCompetitionExportRow(
    string Competition,
    string Name,
    int Year,
    CompetitionStage Stage,
    string ProjectName,
    string SemesterCode,
    string SubjectCode,
    string ClassCode,
    string Groups,
    string Lecturer,
    string Mentor,
    string AcademicMentor);

internal sealed record ProjectDataExportModel(
    DateTime ExportedAtUtc,
    string ExportedBy,
    string Scope,
    IReadOnlyList<string> FilterLines,
    IReadOnlyList<ExportProject> Projects,
    IReadOnlyList<ProjectCompetitionExportRow> CompetitionRows);
