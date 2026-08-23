using EHub.Application.Common.Models.Identity;
using EHub.Infrastructure.Services.Auth;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace EHub.UnitTests.Identity;

public sealed class RegistrationOtpServiceTests
{
    private const string HashKey = "registration-otp-test-key-that-is-longer-than-32-bytes";

    [Fact]
    public void GenerateCode_AlwaysReturnsSixDigits()
    {
        var service = CreateService(HashKey, Environments.Development);

        for (var index = 0; index < 100; index++)
        {
            Assert.Matches("^[0-9]{6}$", service.GenerateCode());
        }
    }

    [Fact]
    public void VerifyCode_AcceptsOnlyMatchingRegistrationAndCode()
    {
        var service = CreateService(HashKey, Environments.Development);
        var registrationId = Guid.NewGuid();
        var hash = service.HashCode(registrationId, "123456");

        Assert.True(service.VerifyCode(registrationId, "123456", hash));
        Assert.False(service.VerifyCode(registrationId, "123457", hash));
        Assert.False(service.VerifyCode(Guid.NewGuid(), "123456", hash));
        Assert.False(service.VerifyCode(registrationId, "123456", "not-a-hex-hash"));
    }

    [Fact]
    public void Constructor_RejectsMissingKeyOutsideDevelopmentOrTesting()
    {
        var action = () => CreateService(string.Empty, Environments.Production);

        Assert.Throws<InvalidOperationException>(action);
    }

    private static RegistrationOtpService CreateService(string hashKey, string environmentName)
    {
        var environment = Substitute.For<IHostEnvironment>();
        environment.EnvironmentName.Returns(environmentName);

        return new RegistrationOtpService(
            Options.Create(new RegistrationOtpOptions { HashKey = hashKey }),
            environment,
            Substitute.For<ILogger<RegistrationOtpService>>());
    }
}
