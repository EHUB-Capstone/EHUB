using System;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using EHub.Application.Common.Interfaces.Identity;
using EHub.Application.Common.Interfaces.Services;
using EHub.Application.Common.Models.Identity;

namespace EHub.Infrastructure.Identity;

public class RefreshTokenService : IRefreshTokenService
{
    private readonly JwtOptions _jwtOptions;
    private readonly IDateTimeProvider _dateTimeProvider;

    public RefreshTokenService(IOptions<JwtOptions> jwtOptions, IDateTimeProvider dateTimeProvider)
    {
        _jwtOptions = jwtOptions.Value;
        _dateTimeProvider = dateTimeProvider;
    }

    public RefreshTokenResult GenerateRefreshToken()
    {
        var rawToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        var tokenHash = Hash(rawToken);
        var expiresAt = _dateTimeProvider.UtcNow.AddDays(_jwtOptions.RefreshTokenExpirationDays);

        return new RefreshTokenResult
        {
            RawToken = rawToken,
            TokenHash = tokenHash,
            ExpiresAt = expiresAt
        };
    }

    public string Hash(string rawToken)
    {
        using var sha256 = SHA256.Create();
        var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(rawToken));
        return Convert.ToBase64String(hashedBytes);
    }

    public bool Verify(string rawToken, string tokenHash)
    {
        var computedHash = Hash(rawToken);
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(computedHash),
            Encoding.UTF8.GetBytes(tokenHash));
    }
}
