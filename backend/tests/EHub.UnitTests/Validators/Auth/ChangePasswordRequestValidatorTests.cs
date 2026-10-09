using EHub.Application.Validators.Auth;
using EHub.Contracts.Auth;

namespace EHub.UnitTests.Validators.Auth;

public class ChangePasswordRequestValidatorTests
{
    private readonly ChangePasswordRequestValidator _validator = new();

    [Fact]
    public void Should_Have_Error_When_CurrentPassword_Is_Empty()
    {
        var result = _validator.Validate(new ChangePasswordRequest
        {
            NewPassword = "NewPassword123",
            ConfirmPassword = "NewPassword123"
        });

        Assert.Contains(result.Errors, error => error.PropertyName == nameof(ChangePasswordRequest.CurrentPassword));
    }

    [Fact]
    public void Should_Have_Error_When_NewPassword_Is_Short()
    {
        var result = _validator.Validate(new ChangePasswordRequest
        {
            CurrentPassword = "OldPassword123",
            NewPassword = "short",
            ConfirmPassword = "short"
        });

        Assert.Contains(result.Errors, error => error.PropertyName == nameof(ChangePasswordRequest.NewPassword));
    }

    [Fact]
    public void Should_Have_Error_When_ConfirmPassword_Does_Not_Match()
    {
        var result = _validator.Validate(new ChangePasswordRequest
        {
            CurrentPassword = "OldPassword123",
            NewPassword = "NewPassword123",
            ConfirmPassword = "DifferentPassword123"
        });

        Assert.Contains(result.Errors, error => error.PropertyName == nameof(ChangePasswordRequest.ConfirmPassword));
    }

    [Fact]
    public void Should_Not_Have_Error_When_Request_Is_Valid()
    {
        var result = _validator.Validate(new ChangePasswordRequest
        {
            CurrentPassword = "OldPassword123",
            NewPassword = "NewPassword123",
            ConfirmPassword = "NewPassword123"
        });

        Assert.True(result.IsValid);
    }
}
