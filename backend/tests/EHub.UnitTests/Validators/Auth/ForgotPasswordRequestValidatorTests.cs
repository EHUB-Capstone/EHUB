using Xunit;
using EHub.Application.Validators.Auth;
using EHub.Contracts.Auth;

namespace EHub.UnitTests.Validators.Auth;

public class ForgotPasswordRequestValidatorTests
{
    private readonly ForgotPasswordRequestValidator _validator = new();

    [Fact]
    public void Should_Have_Error_When_Email_Is_Empty()
    {
        var request = new ForgotPasswordRequest { Email = "" };

        var result = _validator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, x => x.PropertyName == nameof(ForgotPasswordRequest.Email));
    }

    [Fact]
    public void Should_Have_Error_When_Email_Is_Invalid()
    {
        var request = new ForgotPasswordRequest { Email = "invalid-email" };

        var result = _validator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, x => x.PropertyName == nameof(ForgotPasswordRequest.Email));
    }

    [Fact]
    public void Should_Not_Have_Error_When_Request_Is_Valid()
    {
        var request = new ForgotPasswordRequest { Email = "student@example.com" };

        var result = _validator.Validate(request);

        Assert.True(result.IsValid);
    }
}
