using System.Collections.Generic;
using System.Linq;
using EHub.Application.Features.Classes.GetClasses;
using EHub.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace EHub.ApplicationTests.Features.Classes.GetClasses;

public sealed class ClassListOrderingTests
{
    [Fact]
    public void Apply_CodeSort_OrdersClassIndexesBeforePagination()
    {
        var course = new Course { Code = "PRN393" };
        var insertionOrder = new[] { 1, 10, 11, 12, 2, 3, 4, 5, 6, 7, 8, 9 };
        var classes = insertionOrder.Select(index => new Class
        {
            Course = course,
            CourseId = course.Id,
            ClassCode = $"PRN393_{index}",
            ClassIndex = index
        }).AsQueryable();

        var firstPage = ClassListOrdering.Apply(classes, "code").Take(6).Select(item => item.ClassIndex);
        var secondPage = ClassListOrdering.Apply(classes, "code").Skip(6).Take(6).Select(item => item.ClassIndex);

        firstPage.Concat(secondPage).Should().Equal(Enumerable.Range(1, 12));
    }

    [Fact]
    public void Apply_CodeSort_UsesSubjectCodeBeforeClassIndex()
    {
        var classes = new List<Class>
        {
            CreateClass("SWP391", 1),
            CreateClass("PRN393", 2),
            CreateClass("PRN393", 1),
            CreateClass("SWP391", 2)
        }.AsQueryable();

        var orderedCodes = ClassListOrdering.Apply(classes, "code").Select(item => item.ClassCode);

        orderedCodes.Should().Equal("PRN393_1", "PRN393_2", "SWP391_1", "SWP391_2");
    }

    private static Class CreateClass(string subjectCode, int classIndex)
    {
        var course = new Course { Code = subjectCode };
        return new Class
        {
            Course = course,
            CourseId = course.Id,
            ClassCode = $"{subjectCode}_{classIndex}",
            ClassIndex = classIndex
        };
    }
}
