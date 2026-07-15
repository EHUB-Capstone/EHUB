using Xunit;
using EHub.Application.Validators.Auth;
using EHub.Contracts.Auth;

namespace EHub.UnitTests.Validators.Auth;

public class GoogleLoginRequestValidatorTests
{
    private readonly GoogleLoginRequestValidator _validator = new();

    [Fact]
    public void Should_Have_Error_When_IdToken_Is_Empty()
    {
        var request = new GoogleLoginRequest
        {
            IdToken = ""
        };

        var result = _validator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, x => x.PropertyName == nameof(GoogleLoginRequest.IdToken));
    }

    [Fact]
    public void Should_Not_Have_Error_When_IdToken_Is_Not_Empty()
    {
        var request = new GoogleLoginRequest
        {
            IdToken = "google_id_token_here"
        };

        var result = _validator.Validate(request);

        Assert.True(result.IsValid);
    }
}
