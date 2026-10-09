using EHub.Domain.Enums;

namespace EHub.Application.Features.Dashboard.GetAcademicOverview;

internal sealed record SemesterCandidate(
    Guid Id,
    string Code,
    string Name,
    int Year,
    SemesterTerm Term,
    SemesterStatus Status,
    DateOnly? StartDate);

/// <summary>
/// Decides which semester the lecturer's academic overview describes.
/// An explicit choice wins. Otherwise the active semester is used. When no semester is active (for example between
/// two semesters) the overview falls back to the most recent semester the lecturer teaches in, and then to the most
/// recent semester at all, so the dashboard keeps working instead of failing.
/// </summary>
internal static class AcademicOverviewSemesterSelector
{
    public static SemesterCandidate? Choose(
        Guid? requestedSemesterId,
        IReadOnlyCollection<SemesterCandidate> semesters,
        IReadOnlySet<Guid> lecturerSemesterIds)
    {
        var open = semesters.Where(item => item.Status != SemesterStatus.Archived).ToArray();

        if (requestedSemesterId is { } requested)
            return open.FirstOrDefault(item => item.Id == requested);

        return open.FirstOrDefault(item => item.Status == SemesterStatus.Active)
               ?? MostRecent(open.Where(item => lecturerSemesterIds.Contains(item.Id)))
               ?? MostRecent(open);
    }

    private static SemesterCandidate? MostRecent(IEnumerable<SemesterCandidate> semesters) =>
        semesters
            .OrderByDescending(SortDate)
            .ThenByDescending(item => item.Code, StringComparer.Ordinal)
            .FirstOrDefault();

    // Semesters without configured dates are placed by the usual start month of their term.
    private static DateOnly SortDate(SemesterCandidate semester) =>
        semester.StartDate ?? new DateOnly(
            Math.Clamp(semester.Year, 1, 9998),
            semester.Term switch { SemesterTerm.Spring => 1, SemesterTerm.Summer => 5, _ => 9 },
            1);
}
