using System;
using System.Security.Cryptography;
using System.Text;
using EHub.Application.Common.Interfaces.Services;
using Microsoft.AspNetCore.WebUtilities;

namespace EHub.Infrastructure.Services.Auth;

public sealed class PasswordResetTokenService : IPasswordResetTokenService
{
    public string GenerateRawToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(64);
        return WebEncoders.Base64UrlEncode(bytes);
    }

    public string HashToken(string rawToken)
    {
        var tokenBytes = Encoding.UTF8.GetBytes(rawToken);
        var hashBytes = SHA256.HashData(tokenBytes);
        return Convert.ToHexString(hashBytes);
    }
}
