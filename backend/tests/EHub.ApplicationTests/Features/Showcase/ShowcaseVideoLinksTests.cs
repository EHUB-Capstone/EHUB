using EHub.Application.Features.Showcase;
using FluentAssertions;

namespace EHub.ApplicationTests.Features.Showcase;

public sealed class ShowcaseVideoLinksTests
{
    [Theory]
    [InlineData("https://youtu.be/dQw4w9WgXcQ", "https://www.youtube.com/watch?v=dQw4w9WgXcQ")]
    [InlineData("https://www.youtube.com/watch?v=dQw4w9WgXcQ&t=42s", "https://www.youtube.com/watch?v=dQw4w9WgXcQ")]
    [InlineData("https://m.youtube.com/watch?v=dQw4w9WgXcQ", "https://www.youtube.com/watch?v=dQw4w9WgXcQ")]
    [InlineData("https://youtube.com/shorts/dQw4w9WgXcQ", "https://www.youtube.com/watch?v=dQw4w9WgXcQ")]
    [InlineData("https://www.youtube.com/embed/dQw4w9WgXcQ", "https://www.youtube.com/watch?v=dQw4w9WgXcQ")]
    [InlineData("https://vimeo.com/123456789", "https://vimeo.com/123456789")]
    [InlineData("  https://VIMEO.com/123456789  ", "https://vimeo.com/123456789")]
    public void Normalize_ReducesSupportedLinksToACanonicalWatchUrl(string input, string expected) =>
        ShowcaseVideoLinks.Normalize(input).Should().Be(expected);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not a url")]
    [InlineData("http://youtu.be/dQw4w9WgXcQ")]
    [InlineData("https://user:pass@youtu.be/dQw4w9WgXcQ")]
    [InlineData("https://youtu.be.evil.example/dQw4w9WgXcQ")]
    [InlineData("https://evil.example/youtube.com/watch?v=dQw4w9WgXcQ")]
    [InlineData("https://youtu.be:8443/dQw4w9WgXcQ")]
    [InlineData("https://www.youtube.com/watch?v=short")]
    [InlineData("https://www.youtube.com/watch?v=dQw4w9WgXcQ%22onload%3Dx")]
    [InlineData("https://www.youtube.com/playlist?list=PL12345678901")]
    [InlineData("https://vimeo.com/channels/staffpicks")]
    [InlineData("https://example.test/video.mp4")]
    [InlineData("javascript:alert(1)")]
    public void Normalize_RejectsEverythingElse(string? input) => ShowcaseVideoLinks.Normalize(input).Should().BeNull();
}
