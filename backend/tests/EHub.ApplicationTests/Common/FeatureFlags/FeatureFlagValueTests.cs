using EHub.Application.Common.FeatureFlags;
using FluentAssertions;

namespace EHub.ApplicationTests.Common.FeatureFlags;

public sealed class FeatureFlagValueTests
{
    [Theory]
    [InlineData("ON")]
    [InlineData("on")]
    [InlineData(" true ")]
    [InlineData("1")]
    [InlineData("YES")]
    public void IsEnabled_ReturnsTrue_ForSupportedEnabledValues(string value)
    {
        FeatureFlagValue.IsEnabled(value).Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("OFF")]
    [InlineData("false")]
    [InlineData("unexpected")]
    public void IsEnabled_FailsClosed_ForDisabledMissingOrUnknownValues(string? value)
    {
        FeatureFlagValue.IsEnabled(value).Should().BeFalse();
    }
}
