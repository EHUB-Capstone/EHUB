using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using EHub.Application.Common.Interfaces.Identity;
using EHub.Application.Common.Interfaces.Services;
using EHub.Application.Common.Models.Identity;
using EHub.Domain.Entities;
using EHub.Shared.Constants;

namespace EHub.Infrastructure.Identity;

public class JwtTokenService : IJwtTokenService
{
    private readonly JwtOptions _jwtOptions;
    private readonly IDateTimeProvider _dateTimeProvider;

    public JwtTokenService(IOptions<JwtOptions> jwtOptions, IDateTimeProvider dateTimeProvider)
    {
        _jwtOptions = jwtOptions.Value;
        _dateTimeProvider = dateTimeProvider;
    }

    public AccessTokenResult GenerateAccessToken(User user, IReadOnlyCollection<string> roles)
    {
        var expiresAt = _dateTimeProvider.UtcNow.AddMinutes(_jwtOptions.AccessTokenExpirationMinutes);

        var claims = new List<Claim>
        {
            new(ClaimNames.UserId, user.Id.ToString()),
            new(ClaimNames.Email, user.Email),
            new(ClaimNames.FullName, user.FullName),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        foreach (var role in roles)
        {
            claims.Add(new Claim(ClaimNames.Role, role));
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtOptions.Secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _jwtOptions.Issuer,
            audience: _jwtOptions.Audience,
            claims: claims,
            expires: expiresAt,
            signingCredentials: creds);

        var tokenString = new JwtSecurityTokenHandler().WriteToken(token);

        return new AccessTokenResult
        {
            Token = tokenString,
            ExpiresAt = expiresAt
        };
    }
}
