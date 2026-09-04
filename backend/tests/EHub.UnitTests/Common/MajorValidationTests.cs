using EHub.Shared.Constants;
using FluentAssertions;

namespace EHub.UnitTests.Common;

public sealed class MajorValidationTests
{
    [Fact]
    public void All_ShouldContainExactlyTheSupportedMajors()
    {
        MajorCodes.All.Should().Equal(
            MajorCodes.BBA_HM,
            MajorCodes.BBA_IB,
            MajorCodes.BBA_MC,
            MajorCodes.BBA_MKT,
            MajorCodes.BEN,
            MajorCodes.BBA_TM,
            MajorCodes.BIT_AI,
            MajorCodes.BIT_GD,
            MajorCodes.BIT_IA,
            MajorCodes.BIT_SE);
    }

    [Theory]
    [InlineData(MajorCodes.BBA_HM)]
    [InlineData(MajorCodes.BBA_IB)]
    [InlineData(MajorCodes.BBA_MC)]
    [InlineData(MajorCodes.BBA_MKT)]
    [InlineData(MajorCodes.BEN)]
    [InlineData(MajorCodes.BBA_TM)]
    [InlineData(MajorCodes.BIT_AI)]
    [InlineData(MajorCodes.BIT_GD)]
    [InlineData(MajorCodes.BIT_IA)]
    [InlineData(MajorCodes.BIT_SE)]
    public void IsValid_ShouldReturnTrue_WhenMajorCodeIsSupported(string majorCode)
    {
        var result = MajorCodes.IsValid(majorCode);
        result.Should().BeTrue();
    }

    [Theory]
    [InlineData("BBA_FIN")]
    [InlineData("BIT_IS")]
    [InlineData("BLA_CN")]
    public void IsValid_ShouldReturnFalse_WhenMajorCodeIsNoLongerSupported(string majorCode) =>
        MajorCodes.IsValid(majorCode).Should().BeFalse();

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
