using EHub.Application.Validators.Auth;
using EHub.Contracts.Auth;

namespace EHub.UnitTests.Validators.Auth;

public sealed class RegistrationOtpRequestValidatorTests
{
    [Theory]
    [InlineData("")]
    [InlineData("12345")]
    [InlineData("1234567")]
    [InlineData("12A456")]
    public void VerifyValidator_RejectsInvalidCodeFormat(string otp)
    {
        var validator = new VerifyRegistrationOtpRequestValidator();

        var result = validator.Validate(new VerifyRegistrationOtpRequest
        {
            RegistrationId = Guid.NewGuid(),
            Otp = otp
        });

        Assert.False(result.IsValid);
    }

    [Fact]
    public void VerifyValidator_AcceptsSixDigitCode()
    {
        var validator = new VerifyRegistrationOtpRequestValidator();

        var result = validator.Validate(new VerifyRegistrationOtpRequest
        {
            RegistrationId = Guid.NewGuid(),
            Otp = "012345"
        });

        Assert.True(result.IsValid);
    }

    [Fact]
    public void ResendValidator_RejectsEmptyRegistrationId()
    {
        var validator = new ResendRegistrationOtpRequestValidator();

        var result = validator.Validate(new ResendRegistrationOtpRequest
        {
            RegistrationId = Guid.Empty
        });

        Assert.False(result.IsValid);
    }
}
