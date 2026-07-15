using System.Threading;
using System.Threading.Tasks;
using EHub.Application.Common.Interfaces.Identity;
using EHub.Application.Common.Models.Identity;
using EHub.Shared.Results;
using EHub.Shared.Errors;

namespace EHub.IntegrationTests.Common;

public class FakeGoogleAuthService : IGoogleAuthService
{
    public Task<Result<GoogleUserInfo>> VerifyIdTokenAsync(
        string idToken,
        CancellationToken cancellationToken = default)
    {
        if (idToken == "invalid-google-token")
        {
            return Task.FromResult(Result.Failure<GoogleUserInfo>(
                new Error("AUTH_INVALID_GOOGLE_TOKEN", "Google token is invalid.")));
        }

        if (idToken == "unverified-google-email-token")
        {
            return Task.FromResult(Result.Success(new GoogleUserInfo
            {
                Subject = "google-sub-unverified",
                Email = "unverified@example.com",
                FullName = "Unverified User",
                EmailVerified = false
            }));
        }

        if (idToken == "valid-google-token-unregistered")
        {
            return Task.FromResult(Result.Success(new GoogleUserInfo
            {
                Subject = "google-sub-unregistered",
                Email = "unregistered@example.com",
                FullName = "Unregistered Google User",
                EmailVerified = true
            }));
        }

        // Default valid token for test:
        // We use the idToken value as the email, which makes it dynamic
        return Task.FromResult(Result.Success(new GoogleUserInfo
        {
            Subject = $"google-sub-{idToken.GetHashCode()}",
            Email = idToken,
            FullName = "Google Test User",
            EmailVerified = true
        }));
    }
}
