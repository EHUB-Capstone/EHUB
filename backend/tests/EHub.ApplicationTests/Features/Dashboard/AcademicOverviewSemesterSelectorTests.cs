using EHub.Application.Features.Dashboard.GetAcademicOverview;
using EHub.Domain.Enums;
using FluentAssertions;

namespace EHub.ApplicationTests.Features.Dashboard;

public sealed class AcademicOverviewSemesterSelectorTests
{
    private static SemesterCandidate Semester(
        string code, int year, SemesterTerm term, SemesterStatus status, DateOnly? start = null) =>
        new(Guid.NewGuid(), code, code, year, term, status, start);

    private static IReadOnlySet<Guid> Teaching(params SemesterCandidate[] semesters) =>
        semesters.Select(item => item.Id).ToHashSet();

    [Fact]
    public void Choose_UsesTheRequestedSemesterWhenItIsOpen()
    {
        var active = Semester("FA2026", 2026, SemesterTerm.Fall, SemesterStatus.Active);
        var requested = Semester("SP2026", 2026, SemesterTerm.Spring, SemesterStatus.Completed);

        var chosen = AcademicOverviewSemesterSelector.Choose(requested.Id, [active, requested], Teaching());

        chosen.Should().Be(requested);
    }

    [Fact]
    public void Choose_RejectsAnArchivedOrUnknownRequestedSemester()
    {
        var archived = Semester("SP2025", 2025, SemesterTerm.Spring, SemesterStatus.Archived);
        var active = Semester("FA2026", 2026, SemesterTerm.Fall, SemesterStatus.Active);

        AcademicOverviewSemesterSelector.Choose(archived.Id, [archived, active], Teaching()).Should().BeNull();
        AcademicOverviewSemesterSelector.Choose(Guid.NewGuid(), [archived, active], Teaching()).Should().BeNull();
    }

    [Fact]
    public void Choose_PrefersTheActiveSemesterWhenNothingIsRequested()
    {
        var active = Semester("FA2026", 2026, SemesterTerm.Fall, SemesterStatus.Active);
        var newer = Semester("SP2027", 2027, SemesterTerm.Spring, SemesterStatus.Planned);

        AcademicOverviewSemesterSelector.Choose(null, [newer, active], Teaching(newer)).Should().Be(active);
    }

    [Fact]
    public void Choose_WithoutAnActiveSemesterFallsBackToTheLatestSemesterTheLecturerTeachesIn()
    {
        var older = Semester("SP2026", 2026, SemesterTerm.Spring, SemesterStatus.Completed);
        var closing = Semester("FA2026", 2026, SemesterTerm.Fall, SemesterStatus.Closing);
        var plannedWithoutClasses = Semester("SP2027", 2027, SemesterTerm.Spring, SemesterStatus.Planned);

        var chosen = AcademicOverviewSemesterSelector.Choose(
            null, [older, plannedWithoutClasses, closing], Teaching(older, closing));

        chosen.Should().Be(closing, "the lecturer teaches nothing in the planned semester");
    }

    [Fact]
    public void Choose_WithoutAnActiveSemesterAndWithoutClassesUsesTheLatestSemester()
    {
        var older = Semester("SP2026", 2026, SemesterTerm.Spring, SemesterStatus.Completed);
        var latest = Semester("SP2027", 2027, SemesterTerm.Spring, SemesterStatus.Planned);

        AcademicOverviewSemesterSelector.Choose(null, [older, latest], Teaching()).Should().Be(latest);
    }

    [Fact]
    public void Choose_IgnoresArchivedSemestersEvenWhenTheyAreTheOnlyOnesTaught()
    {
        var archived = Semester("FA2025", 2025, SemesterTerm.Fall, SemesterStatus.Archived);
        var open = Semester("SP2026", 2026, SemesterTerm.Spring, SemesterStatus.Completed);

        AcademicOverviewSemesterSelector.Choose(null, [archived, open], Teaching(archived)).Should().Be(open);
    }

    [Fact]
    public void Choose_OrdersByConfiguredStartDateAndOtherwiseByYearAndTerm()
    {
        var spring = Semester("SP2026", 2026, SemesterTerm.Spring, SemesterStatus.Completed);
        var fall = Semester("FA2026", 2026, SemesterTerm.Fall, SemesterStatus.Completed);
        var summerWithDate = Semester("SU2026", 2026, SemesterTerm.Summer, SemesterStatus.Completed, new DateOnly(2026, 10, 1));

        AcademicOverviewSemesterSelector.Choose(null, [spring, fall], Teaching()).Should().Be(fall);
        AcademicOverviewSemesterSelector.Choose(null, [spring, fall, summerWithDate], Teaching()).Should().Be(summerWithDate,
            "a configured start date overrides the default placement of the term");
    }

    [Fact]
    public void Choose_ReturnsNothingWhenThereAreNoOpenSemesters()
    {
        var archived = Semester("FA2025", 2025, SemesterTerm.Fall, SemesterStatus.Archived);

        AcademicOverviewSemesterSelector.Choose(null, [archived], Teaching()).Should().BeNull();
        AcademicOverviewSemesterSelector.Choose(null, [], Teaching()).Should().BeNull();
    }
}
