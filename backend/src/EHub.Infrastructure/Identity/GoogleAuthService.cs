using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Google.Apis.Auth;
using EHub.Application.Common.Interfaces.Identity;
using EHub.Application.Common.Models.Identity;
using EHub.Application.Features.Auth;
using EHub.Shared.Results;

namespace EHub.Infrastructure.Identity;

public class GoogleAuthService : IGoogleAuthService
{
    private readonly GoogleOptions _googleOptions;

    public GoogleAuthService(IOptions<GoogleOptions> googleOptions)
    {
        _googleOptions = googleOptions.Value;
    }

    public async Task<Result<GoogleUserInfo>> VerifyIdTokenAsync(
        string idToken,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var settings = new GoogleJsonWebSignature.ValidationSettings
            {
                Audience = new[] { _googleOptions.ClientId }
            };

            var payload = await GoogleJsonWebSignature.ValidateAsync(idToken, settings);
            if (payload == null)
            {
                return Result.Failure<GoogleUserInfo>(AuthErrors.InvalidGoogleToken);
            }

            if (!payload.EmailVerified)
            {
                return Result.Failure<GoogleUserInfo>(AuthErrors.GoogleEmailNotVerified);
            }

            var userInfo = new GoogleUserInfo
            {
                Subject = payload.Subject,
                Email = payload.Email,
                FullName = payload.Name,
                EmailVerified = payload.EmailVerified
            };

            return Result.Success(userInfo);
        }
        catch (Exception)
        {
            return Result.Failure<GoogleUserInfo>(AuthErrors.InvalidGoogleToken);
        }
    }
}
