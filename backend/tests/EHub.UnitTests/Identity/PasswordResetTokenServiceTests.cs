using Xunit;
using EHub.Infrastructure.Services.Auth;

namespace EHub.UnitTests.Identity;

public class PasswordResetTokenServiceTests
{
    private readonly PasswordResetTokenService _service = new();

    [Fact]
    public void GenerateRawToken_Should_Return_NonEmpty_String()
    {
        var rawToken = _service.GenerateRawToken();

        Assert.False(string.IsNullOrWhiteSpace(rawToken));
        Assert.True(rawToken.Length > 20);
    }

    [Fact]
    public void HashToken_Should_Be_Deterministic()
    {
        var rawToken = "my-secure-token-123456";

        var hash1 = _service.HashToken(rawToken);
        var hash2 = _service.HashToken(rawToken);

        Assert.Equal(hash1, hash2);
    }

    [Fact]
    public void HashToken_Should_Not_Return_RawToken()
    {
        var rawToken = "my-secure-token-123456";

        var hash = _service.HashToken(rawToken);

        Assert.NotEqual(rawToken, hash);
    }
}
