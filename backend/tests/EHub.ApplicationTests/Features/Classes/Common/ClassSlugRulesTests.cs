using EHub.Application.Features.Classes.Common;
using FluentAssertions;

namespace EHub.ApplicationTests.Features.Classes.Common;

public sealed class ClassSlugRulesTests
{
    [Fact]
    public void BuildBaseSlug_WhenGivenSemesterCourseAndClassIndex_ReturnsFriendlySlug()
    {
        var slug = ClassSlugRules.BuildBaseSlug("SU2026", "EXE101", 8);

        slug.Should().Be("su2026-exe101-8");
    }

    [Fact]
    public void BuildBaseSlug_WhenValuesContainDiacriticsAndUnsafeCharacters_NormalizesSlug()
    {
        var slug = ClassSlugRules.BuildBaseSlug("SỨ 2026", "Đề Án 101", 8);

        slug.Should().Be("su-2026-de-an-101-8");
    }

    [Fact]
    public void MakeUnique_WhenSlugAlreadyExists_AppendsNextAvailableSuffix()
    {
        var slug = ClassSlugRules.MakeUnique(
            "su2026-exe101-8",
            new[] { "su2026-exe101-8", "su2026-exe101-8-2" });

        slug.Should().Be("su2026-exe101-8-3");
    }

    [Fact]
    public void TryNormalizeRouteSlug_WhenSlugHasUppercase_ReturnsLowercaseSlug()
    {
        var isValid = ClassSlugRules.TryNormalizeRouteSlug("SU2026-EXE101-8", out var slug);

        isValid.Should().BeTrue();
        slug.Should().Be("su2026-exe101-8");
    }

    [Theory]
    [InlineData("")]
    [InlineData("su2026_exe101_8")]
    [InlineData("su2026 exe101 8")]
    [InlineData("-su2026-exe101-8")]
    [InlineData("su2026-exe101-8-")]
    public void TryNormalizeRouteSlug_WhenSlugIsUnsafe_ReturnsFalse(string value)
    {
        var isValid = ClassSlugRules.TryNormalizeRouteSlug(value, out var slug);

        isValid.Should().BeFalse();
        slug.Should().BeEmpty();
    }
}
