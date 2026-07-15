using EHub.Shared.Constants;
using FluentAssertions;

namespace EHub.UnitTests.Common;

public sealed class MajorValidationTests
{
    [Fact]
    public void IsValid_ShouldReturnTrue_WhenMajorCodeIsBitSe()
    {
        var result = MajorCodes.IsValid("BIT_SE");
        result.Should().BeTrue();
    }

    [Fact]
    public void IsValid_ShouldReturnTrue_WhenMajorCodeIsBitAi()
    {
        var result = MajorCodes.IsValid("BIT_AI");
        result.Should().BeTrue();
    }

    [Fact]
    public void IsValid_ShouldReturnTrue_WhenMajorCodeIsBbaMkt()
    {
        var result = MajorCodes.IsValid("BBA_MKT");
        result.Should().BeTrue();
    }

    [Fact]
    public void IsValid_ShouldReturnFalse_WhenMajorCodeContainsLowercaseLInsteadOfI()
    {
        // "BIT_Al" has lowercase 'l' instead of uppercase 'I'
        var result = MajorCodes.IsValid("BIT_Al");
        result.Should().BeFalse();
    }

    [Fact]
    public void IsValid_ShouldReturnFalse_WhenMajorCodeIsEmptyOrNull()
    {
        MajorCodes.IsValid("").Should().BeFalse();
        MajorCodes.IsValid(null).Should().BeFalse();
    }
}
