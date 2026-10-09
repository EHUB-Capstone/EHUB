using Xunit;
using EHub.Application.Validators.Auth;
using EHub.Contracts.Auth;

namespace EHub.UnitTests.Validators.Auth;

public class EmailPasswordLoginRequestValidatorTests
{
    private readonly EmailPasswordLoginRequestValidator _validator = new();

    [Fact]
    public void Should_Have_Error_When_Email_Is_Empty()
    {
        var request = new EmailPasswordLoginRequest
        {
            Email = "",
            Password = "Password123"
        };

        var result = _validator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, x => x.PropertyName == nameof(EmailPasswordLoginRequest.Email));
    }

    [Fact]
    public void Should_Have_Error_When_Email_Is_Invalid()
    {
        var request = new EmailPasswordLoginRequest
        {
            Email = "invalid-email",
            Password = "Password123"
        };

        var result = _validator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, x => x.PropertyName == nameof(EmailPasswordLoginRequest.Email));
    }

    [Fact]
    public void Should_Have_Error_When_Password_Is_Empty()
    {
        var request = new EmailPasswordLoginRequest
        {
            Email = "student@fpt.edu.vn",
            Password = ""
        };

        var result = _validator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, x => x.PropertyName == nameof(EmailPasswordLoginRequest.Password));
    }

    [Theory]
    [InlineData("12345", false)]
    [InlineData("123456", true)]
    public void Should_Enforce_Six_Character_Minimum_Password(string password, bool expectedValid)
    {
        var request = new EmailPasswordLoginRequest
        {
            Email = "student@fpt.edu.vn",
            Password = password
        };

        var result = _validator.Validate(request);

        Assert.Equal(expectedValid, result.IsValid);
    }

    [Fact]
    public void Should_Not_Have_Error_When_Request_Is_Valid()
    {
        var request = new EmailPasswordLoginRequest
        {
            Email = "student@fpt.edu.vn",
            Password = "Password123"
        };

        var result = _validator.Validate(request);

        Assert.True(result.IsValid);
    }
}
