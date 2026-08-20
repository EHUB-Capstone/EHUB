using EHub.Application.Features.Classes.Common;
using EHub.Domain.Entities;
using EHub.Domain.Enums;
using FluentAssertions;

namespace EHub.ApplicationTests.Features.Classes.Common;

public sealed class ClassCreationSemesterPolicyTests
{
    [Fact]
    public void SelectAvailable_ReturnsCurrentAndImmediateNextSemesterOnly()
    {
        var today = new DateOnly(2026, 8, 20);
        var current = Semester(SemesterTerm.Summer, 2026, SemesterStatus.Active, 5, 1, 8, 31);
        var next = Semester(SemesterTerm.Fall, 2026, SemesterStatus.Planned, 9, 1, 12, 31);
        var later = Semester(SemesterTerm.Spring, 2027, SemesterStatus.Planned, 1, 1, 4, 30);

        var result = ClassCreationSemesterPolicy.SelectAvailable([later, next, current], today);

        result.Should().Equal(current, next);
    }

    [Fact]
    public void SelectAvailable_HidesExpiredActiveAndPlannedSemesters()
    {
        var today = new DateOnly(2026, 8, 20);
        var expiredActive = Semester(SemesterTerm.Spring, 2026, SemesterStatus.Active, 1, 1, 4, 30);
        var expiredPlanned = Semester(SemesterTerm.Summer, 2026, SemesterStatus.Planned, 5, 1, 8, 1);
        var next = Semester(SemesterTerm.Fall, 2026, SemesterStatus.Planned, 9, 1, 12, 31);

        var result = ClassCreationSemesterPolicy.SelectAvailable([expiredActive, expiredPlanned, next], today);

        result.Should().ContainSingle().Which.Should().BeSameAs(next);
    }

    [Fact]
    public void SelectAvailable_HidesPlannedSemesterThatHasAlreadyStarted()
    {
        var today = new DateOnly(2026, 8, 20);
        var stalePlanned = Semester(SemesterTerm.Summer, 2026, SemesterStatus.Planned, 5, 1, 8, 31);
        var next = Semester(SemesterTerm.Fall, 2026, SemesterStatus.Planned, 9, 1, 12, 31);

        var result = ClassCreationSemesterPolicy.SelectAvailable([stalePlanned, next], today);

        result.Should().ContainSingle().Which.Should().BeSameAs(next);
    }

    [Fact]
    public void SelectAvailable_WhenNoActiveSemester_ReturnsNearestPlannedSemesterOnly()
    {
        var today = new DateOnly(2026, 8, 20);
        var nearest = Semester(SemesterTerm.Fall, 2026, SemesterStatus.Planned, 9, 1, 12, 31);
        var later = Semester(SemesterTerm.Spring, 2027, SemesterStatus.Planned, 1, 1, 4, 30);

        var result = ClassCreationSemesterPolicy.SelectAvailable([later, nearest], today);

        result.Should().ContainSingle().Which.Should().BeSameAs(nearest);
    }

    private static Semester Semester(
        SemesterTerm term,
        int year,
        SemesterStatus status,
        int startMonth,
        int startDay,
        int endMonth,
        int endDay) => new()
        {
            Code = $"{term}{year}",
            Name = $"{term} {year}",
            Term = term,
            Year = year,
            Status = status,
            StartDate = new DateOnly(year, startMonth, startDay),
            EndDate = new DateOnly(year, endMonth, endDay)
        };
}
