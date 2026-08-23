using System.Security.Cryptography;
using System.Text;
using EHub.Application.Common.Interfaces.Services;
using EHub.Application.Common.Models.Identity;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EHub.Infrastructure.Services.Auth;

public sealed class RegistrationOtpService : IRegistrationOtpService
{
    private readonly byte[] _hashKey;

    public RegistrationOtpService(
        IOptions<RegistrationOtpOptions> options,
        IHostEnvironment environment,
        ILogger<RegistrationOtpService> logger)
    {
        var configuredKey = options.Value.HashKey;
        if (string.IsNullOrWhiteSpace(configuredKey))
        {
            if (!environment.IsDevelopment() && !environment.IsEnvironment("Testing"))
            {
                throw new InvalidOperationException(
                    "RegistrationOtp:HashKey must be configured outside Development.");
            }

            _hashKey = RandomNumberGenerator.GetBytes(32);
            logger.LogWarning(
                "RegistrationOtp:HashKey is not configured. An ephemeral development key is being used; pending codes become invalid after restart.");
            return;
        }

        _hashKey = Encoding.UTF8.GetBytes(configuredKey);
        if (_hashKey.Length < 32)
        {
            throw new InvalidOperationException(
                "RegistrationOtp:HashKey must contain at least 32 bytes.");
        }
    }

    public string GenerateCode()
    {
        return RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
    }

    public string HashCode(Guid registrationId, string code)
    {
        var payload = Encoding.UTF8.GetBytes($"{registrationId:N}:{code}");
        var hash = HMACSHA256.HashData(_hashKey, payload);
        return Convert.ToHexString(hash);
    }

    public bool VerifyCode(Guid registrationId, string code, string expectedHash)
    {
        byte[] expectedBytes;
        try
        {
            expectedBytes = Convert.FromHexString(expectedHash);
        }
        catch (FormatException)
        {
            return false;
        }

        var payload = Encoding.UTF8.GetBytes($"{registrationId:N}:{code}");
        var actualBytes = HMACSHA256.HashData(_hashKey, payload);
        return CryptographicOperations.FixedTimeEquals(actualBytes, expectedBytes);
    }
}
