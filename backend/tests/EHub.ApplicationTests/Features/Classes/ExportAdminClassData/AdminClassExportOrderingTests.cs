using System.Collections.Generic;
using System.Linq;
using EHub.Application.Features.Classes.ExportAdminClassData;
using EHub.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace EHub.ApplicationTests.Features.Classes.ExportAdminClassData;

public sealed class AdminClassExportOrderingTests
{
    [Fact]
    public void Apply_IgnoresSelectionOrder_AndUsesNaturalCourseThenNumericClassIndex()
    {
        var classes = new List<Class>
        {
            CreateClass("EXE201", 3),
            CreateClass("EXE101", 10),
            CreateClass("EXE301", 2),
            CreateClass("EXE101", 2),
            CreateClass("EXE201", 1),
            CreateClass("EXE101", 1),
            CreateClass("EXE301", 1),
            CreateClass("EXE201", 2)
        };

        var result = AdminClassExportOrdering.Apply(classes).Select(@class => @class.ClassCode);

        result.Should().Equal(
            "EXE101-1",
            "EXE101-2",
            "EXE101-10",
            "EXE201-1",
            "EXE201-2",
            "EXE201-3",
            "EXE301-1",
            "EXE301-2");
    }

    [Fact]
    public void Apply_NaturallyOrdersNumericSegmentsInCourseCodes()
    {
        var classes = new[]
        {
            CreateClass("EXE10", 1),
            CreateClass("EXE2", 1),
            CreateClass("EXE1", 1)
        };

        AdminClassExportOrdering.Apply(classes)
            .Select(@class => @class.Course.Code)
            .Should().Equal("EXE1", "EXE2", "EXE10");
    }

    private static Class CreateClass(string courseCode, int classIndex)
    {
        var course = new Course { Code = courseCode, Name = courseCode };
        return new Class
        {
            Course = course,
            CourseId = course.Id,
            ClassIndex = classIndex,
            ClassCode = $"{courseCode}-{classIndex}",
            Slug = $"{courseCode}-{classIndex}".ToLowerInvariant()
        };
    }
}
