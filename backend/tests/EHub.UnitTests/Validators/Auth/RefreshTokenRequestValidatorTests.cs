using Xunit;
using EHub.Application.Validators.Auth;
using EHub.Contracts.Auth;

namespace EHub.UnitTests.Validators.Auth;

public class RefreshTokenRequestValidatorTests
{
    private readonly RefreshTokenRequestValidator _validator = new();

    [Fact]
    public void Should_Have_Error_When_RefreshToken_Is_Empty()
    {
        var request = new RefreshTokenRequest
        {
            RefreshToken = ""
        };

        var result = _validator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, x => x.PropertyName == nameof(RefreshTokenRequest.RefreshToken));
    }

    [Fact]
    public void Should_Not_Have_Error_When_RefreshToken_Is_Not_Empty()
    {
        var request = new RefreshTokenRequest
        {
            RefreshToken = "raw_refresh_token_here"
        };

        var result = _validator.Validate(request);

        Assert.True(result.IsValid);
    }
}
