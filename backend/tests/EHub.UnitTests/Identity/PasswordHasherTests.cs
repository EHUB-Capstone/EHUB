using EHub.Infrastructure.Identity;
using FluentAssertions;

namespace EHub.UnitTests.Identity;

public sealed class PasswordHasherTests
{
    private readonly BCryptPasswordHasher _passwordHasher = new();

    [Fact]
    public void Hash_ShouldNotReturnRawPassword()
    {
        var password = "Password123";

        var hash = _passwordHasher.Hash(password);

        hash.Should().NotBe(password);
        hash.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Verify_ShouldReturnTrue_WhenPasswordIsCorrect()
    {
        var password = "Password123";
        var hash = _passwordHasher.Hash(password);

        var result = _passwordHasher.Verify(password, hash);

        result.Should().BeTrue();
    }

    [Fact]
    public void Verify_ShouldReturnFalse_WhenPasswordIsWrong()
    {
        var hash = _passwordHasher.Hash("Password123");

        var result = _passwordHasher.Verify("WrongPassword123", hash);

        result.Should().BeFalse();
    }

    [Fact]
    public void Hash_ShouldGenerateDifferentHashes_ForSamePassword()
    {
        var password = "Password123";

        var hash1 = _passwordHasher.Hash(password);
        var hash2 = _passwordHasher.Hash(password);

        hash1.Should().NotBe(hash2);
        _passwordHasher.Verify(password, hash1).Should().BeTrue();
        _passwordHasher.Verify(password, hash2).Should().BeTrue();
    }
}
