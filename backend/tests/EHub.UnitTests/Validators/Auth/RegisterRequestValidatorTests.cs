using Xunit;
using EHub.Application.Validators.Auth;
using EHub.Contracts.Auth;
using EHub.Shared.Constants;
using System.Linq;

namespace EHub.UnitTests.Validators.Auth;

public class RegisterRequestValidatorTests
{
    private readonly RegisterRequestValidator _validator = new();

    [Fact]
    public void Should_Have_Error_When_FullName_Is_Empty()
    {
        var request = new RegisterRequest
        {
            FullName = "",
            Email = "student@fpt.edu.vn",
            Password = "Password123",
            ConfirmPassword = "Password123",
            Role = SystemRoles.Student,
            MajorCode = MajorCodes.BIT_SE
        };

        var result = _validator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, x => x.PropertyName == nameof(RegisterRequest.FullName));
    }

    [Fact]
    public void Should_Have_Error_When_Email_Is_Empty()
    {
        var request = new RegisterRequest
        {
            FullName = "Nguyen Van A",
            Email = "",
            Password = "Password123",
            ConfirmPassword = "Password123",
            Role = SystemRoles.Student,
            MajorCode = MajorCodes.BIT_SE
        };

        var result = _validator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, x => x.PropertyName == nameof(RegisterRequest.Email));
    }

    [Fact]
    public void Should_Have_Error_When_Email_Is_Invalid()
    {
        var request = new RegisterRequest
        {
            FullName = "Nguyen Van A",
            Email = "invalid-email",
            Password = "Password123",
            ConfirmPassword = "Password123",
            Role = SystemRoles.Student,
            MajorCode = MajorCodes.BIT_SE
        };

        var result = _validator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, x => x.PropertyName == nameof(RegisterRequest.Email));
    }

    [Fact]
    public void Should_Have_Error_When_Password_Is_Too_Short()
    {
        var request = new RegisterRequest
        {
            FullName = "Nguyen Van A",
            Email = "student@fpt.edu.vn",
            Password = "Pass",
            ConfirmPassword = "Pass",
            Role = SystemRoles.Student,
            MajorCode = MajorCodes.BIT_SE
        };

        var result = _validator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, x => x.PropertyName == nameof(RegisterRequest.Password));
    }

    [Fact]
    public void Should_Have_Error_When_ConfirmPassword_Does_Not_Match()
    {
        var request = new RegisterRequest
        {
            FullName = "Nguyen Van A",
            Email = "student@fpt.edu.vn",
            Password = "Password123",
            ConfirmPassword = "DifferentPassword",
            Role = SystemRoles.Student,
            MajorCode = MajorCodes.BIT_SE
        };

        var result = _validator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, x => x.PropertyName == nameof(RegisterRequest.ConfirmPassword));
    }

    [Fact]
    public void Should_Have_Error_When_Role_Is_Invalid()
    {
        var request = new RegisterRequest
        {
            FullName = "Nguyen Van A",
            Email = "student@fpt.edu.vn",
            Password = "Password123",
            ConfirmPassword = "Password123",
            Role = "InvalidRole",
            MajorCode = MajorCodes.BIT_SE
        };

        var result = _validator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, x => x.PropertyName == nameof(RegisterRequest.Role));
    }

    [Fact]
    public void Should_Have_Error_When_Role_Is_Admin()
    {
        var request = new RegisterRequest
        {
            FullName = "Nguyen Van A",
            Email = "student@fpt.edu.vn",
            Password = "Password123",
            ConfirmPassword = "Password123",
            Role = SystemRoles.Admin,
            MajorCode = MajorCodes.BIT_SE
        };

        var result = _validator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, x => x.PropertyName == nameof(RegisterRequest.Role));
    }

    [Fact]
    public void Should_Have_Error_When_Student_MajorCode_Is_Empty()
    {
        var request = new RegisterRequest
        {
            FullName = "Nguyen Van A",
            Email = "student@fpt.edu.vn",
            Password = "Password123",
            ConfirmPassword = "Password123",
            Role = SystemRoles.Student,
            MajorCode = ""
        };

        var result = _validator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, x => x.PropertyName == nameof(RegisterRequest.MajorCode));
    }

    [Fact]
    public void Should_Have_Error_When_Student_MajorCode_Is_Invalid()
    {
        var request = new RegisterRequest
        {
            FullName = "Nguyen Van A",
            Email = "student@fpt.edu.vn",
            Password = "Password123",
            ConfirmPassword = "Password123",
            Role = SystemRoles.Student,
            MajorCode = "INVALID_MAJOR_CODE"
        };

        var result = _validator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, x => x.PropertyName == nameof(RegisterRequest.MajorCode));
    }

    [Fact]
    public void Should_Not_Have_Error_When_Student_Request_Is_Valid()
    {
        var request = new RegisterRequest
        {
            FullName = "Nguyen Van A",
            Email = "student@fpt.edu.vn",
            Password = "Password123",
            ConfirmPassword = "Password123",
            Role = SystemRoles.Student,
            MajorCode = MajorCodes.BIT_SE
        };

        var result = _validator.Validate(request);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Should_Not_Have_Error_When_Lecturer_Request_Is_Valid()
    {
        var request = new RegisterRequest
        {
            FullName = "Tran Van B",
            Email = "lecturer@fpt.edu.vn",
            Password = "Password123",
            ConfirmPassword = "Password123",
            Role = SystemRoles.Lecturer,
            MajorCode = null
        };

        var result = _validator.Validate(request);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Should_Not_Have_Error_When_Mentor_Request_Is_Valid()
    {
        var request = new RegisterRequest
        {
            FullName = "Le Van C",
            Email = "mentor@fpt.edu.vn",
            Password = "Password123",
            ConfirmPassword = "Password123",
            Role = SystemRoles.Mentor,
            MajorCode = null
        };

        var result = _validator.Validate(request);

        Assert.True(result.IsValid);
    }
}
