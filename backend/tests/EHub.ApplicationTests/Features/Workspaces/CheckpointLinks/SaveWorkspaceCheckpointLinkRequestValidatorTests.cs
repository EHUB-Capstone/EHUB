using EHub.Application.Features.Workspaces.CheckpointLinks;
using EHub.Contracts.Workspaces;
using FluentAssertions;

namespace EHub.ApplicationTests.Features.Workspaces.CheckpointLinks;

public sealed class SaveWorkspaceCheckpointLinkRequestValidatorTests
{
    private readonly SaveWorkspaceCheckpointLinkRequestValidator _validator = new();

    [Theory]
    [InlineData("https://drive.google.com/file/d/example")]
    [InlineData("github.com/example/project")]
    [InlineData("https://demo.example.com:8443/path?mode=review")]
    public async Task ValidateAsync_PublicHttpsUrl_IsValid(string url)
    {
        var result = await _validator.ValidateAsync(new SaveWorkspaceCheckpointLinkRequest
        {
            Name = "Project resource",
            Url = url
        });

        result.IsValid.Should().BeTrue();
        CheckpointLinkUrl.TryNormalize(url, out var normalized).Should().BeTrue();
        normalized.Should().StartWith("https://");
    }

    [Theory]
    [InlineData("http://example.com")]
    [InlineData("https://localhost/demo")]
    [InlineData("https://127.0.0.1/demo")]
    [InlineData("https://192.168.1.10/demo")]
    [InlineData("https://10.0.0.1/demo")]
    [InlineData("https://user:password@example.com/demo")]
    [InlineData("https://internal")]
    [InlineData("javascript:alert(1)")]
    public async Task ValidateAsync_NonPublicOrUnsafeUrl_IsInvalid(string url)
    {
        var result = await _validator.ValidateAsync(new SaveWorkspaceCheckpointLinkRequest
        {
            Name = "Project resource",
            Url = url
        });

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task ValidateAsync_NameLongerThanOneHundredCharacters_IsInvalid()
    {
        var result = await _validator.ValidateAsync(new SaveWorkspaceCheckpointLinkRequest
        {
            Name = new string('a', 101),
            Url = "https://example.com"
        });

        result.IsValid.Should().BeFalse();
    }
}
