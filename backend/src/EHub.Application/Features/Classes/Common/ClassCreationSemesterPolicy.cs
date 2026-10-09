using EHub.Domain.Entities;
using EHub.Domain.Enums;

namespace EHub.Application.Features.Classes.Common;

/// <summary>
/// Defines the only semesters in which administrators may create classes:
/// the non-expired active semester and its immediate non-expired planned successor.
/// </summary>
public static class ClassCreationSemesterPolicy
{
    public static IReadOnlyList<Semester> SelectAvailable(
        IEnumerable<Semester> semesters,
        DateOnly today)
    {
        var candidates = semesters
            .Where(item => IsEligible(item, today))
            .OrderBy(GetSortDate)
            .ThenBy(item => item.Year)
            .ThenBy(item => item.Term)
            .ToArray();

        var active = candidates
            .Where(item => item.Status == SemesterStatus.Active)
            .OrderBy(GetSortDate)
            .LastOrDefault();

        if (active != null)
        {
            var next = candidates.FirstOrDefault(item =>
                item.Status == SemesterStatus.Planned &&
                CompareAcademicOrder(item, active) > 0);

            return next == null ? new[] { active } : new[] { active, next };
        }

        var nearestPlanned = candidates.FirstOrDefault(item => item.Status == SemesterStatus.Planned);
        return nearestPlanned == null ? Array.Empty<Semester>() : new[] { nearestPlanned };
    }

    private static bool IsEligible(Semester semester, DateOnly today)
    {
        if (semester.Status == SemesterStatus.Active)
        {
            return !semester.EndDate.HasValue || semester.EndDate.Value >= today;
        }

        if (semester.Status == SemesterStatus.Planned)
        {
            // A planned semester that has already started is stale lifecycle data,
            // not the next semester administrators should use for class creation.
            return GetSortDate(semester) > today &&
                   (!semester.EndDate.HasValue || semester.EndDate.Value >= today);
        }

        return false;
    }

    private static DateOnly GetSortDate(Semester semester) =>
        semester.StartDate ?? new DateOnly(semester.Year, semester.Term switch
        {
            SemesterTerm.Spring => 1,
            SemesterTerm.Summer => 5,
            SemesterTerm.Fall => 9,
            _ => 1
        }, 1);

    private static int CompareAcademicOrder(Semester left, Semester right)
    {
        var yearComparison = left.Year.CompareTo(right.Year);
        return yearComparison != 0 ? yearComparison : left.Term.CompareTo(right.Term);
    }
}
