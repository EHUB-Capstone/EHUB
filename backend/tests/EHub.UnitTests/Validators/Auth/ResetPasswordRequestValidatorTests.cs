using Xunit;
using EHub.Application.Validators.Auth;
using EHub.Contracts.Auth;

namespace EHub.UnitTests.Validators.Auth;

public class ResetPasswordRequestValidatorTests
{
    private readonly ResetPasswordRequestValidator _validator = new();

    [Fact]
    public void Should_Have_Error_When_Token_Is_Empty()
    {
        var request = new ResetPasswordRequest
        {
            Token = "",
            NewPassword = "Password123",
            ConfirmPassword = "Password123"
        };

        var result = _validator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, x => x.PropertyName == nameof(ResetPasswordRequest.Token));
    }

    [Fact]
    public void Should_Have_Error_When_NewPassword_Is_Short()
    {
        var request = new ResetPasswordRequest
        {
            Token = "valid-token",
            NewPassword = "short",
            ConfirmPassword = "short"
        };

        var result = _validator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, x => x.PropertyName == nameof(ResetPasswordRequest.NewPassword));
    }

    [Theory]
    [InlineData("12345", false)]
    [InlineData("123456", true)]
    public void Should_Enforce_Six_Character_Minimum_NewPassword(string password, bool expectedValid)
    {
        var request = new ResetPasswordRequest
        {
            Token = "valid-token",
            NewPassword = password,
            ConfirmPassword = password
        };

        var result = _validator.Validate(request);

        Assert.Equal(expectedValid, result.IsValid);
    }

    [Fact]
    public void Should_Have_Error_When_ConfirmPassword_Does_Not_Match()
    {
        var request = new ResetPasswordRequest
        {
            Token = "valid-token",
            NewPassword = "Password123",
            ConfirmPassword = "DifferentPassword123"
        };

        var result = _validator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, x => x.PropertyName == nameof(ResetPasswordRequest.ConfirmPassword));
    }

    [Fact]
    public void Should_Not_Have_Error_When_Request_Is_Valid()
    {
        var request = new ResetPasswordRequest
        {
            Token = "valid-token",
            NewPassword = "Password123",
            ConfirmPassword = "Password123"
        };

        var result = _validator.Validate(request);

        Assert.True(result.IsValid);
    }
}
