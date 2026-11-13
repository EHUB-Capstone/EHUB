using EHub.Infrastructure.Mentoring.Gemini;
using FluentAssertions;

namespace EHub.ApplicationTests.Features.Mentoring;

public sealed class GeminiMentorDefaultConfigurationTests
{
    [Fact]
    public void DefaultsUseTheMeasuredLiteModelsWithoutRetiringExplicitConfigurations()
    {
        var options = new GeminiMentorOptions { ApiKey = "SYNTHETIC_CONFIGURATION_TEST_KEY", DataTransferEnabled = true };
        options.Models.Should().Equal("gemini-3.5-flash-lite", "gemini-3.1-flash-lite");
        options.IsValid().Should().BeTrue();

        foreach (var explicitlyConfigured in new[] { "gemini-3.8-flash", "gemini-2.5-flash-lite" })
        {
            options.Model = explicitlyConfigured;
            options.IsValid().Should().BeTrue("existing explicit model choices remain supported");
        }
    }

    [Theory]
    [InlineData("docker-compose.gemini.yml", "${GEMINI_MODEL:-{0}}", "${GEMINI_FALLBACK_MODELS:-{1}}")]
    [InlineData(".env.staging.example", "GEMINI_MODEL={0}", "GEMINI_FALLBACK_MODELS={1}")]
    [InlineData(".env.production.example", "GEMINI_MODEL={0}", "GEMINI_FALLBACK_MODELS={1}")]
    [InlineData("backend/.env.example", "Mentoring__Gemini__Model={0}", "Mentoring__Gemini__FallbackModels={1}")]
    [InlineData("scripts/mentoring/configure-gemini.ps1", "$Model = '{0}'", "'Mentoring:Gemini:FallbackModels' = '{1}'")]
    public void DeploymentExamplesAndLocalSetupUseTheSameDefaults(string file, string modelPattern, string fallbackPattern)
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "docker-compose.gemini.yml")))
            root = root.Parent;
        root.Should().NotBeNull("the test runs from the repository checkout");

        // Only explicitly named tracked examples/setup files are read, never runtime .env or secrets.
        var source = File.ReadAllText(Path.Combine(root!.FullName, file));
        var defaults = new GeminiMentorOptions();
        source.Should().Contain(modelPattern.Replace("{0}", defaults.Model));
        source.Should().Contain(fallbackPattern.Replace("{1}", defaults.FallbackModels));
    }
}
