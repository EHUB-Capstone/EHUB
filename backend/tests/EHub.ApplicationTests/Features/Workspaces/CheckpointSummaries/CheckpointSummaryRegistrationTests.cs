using EHub.Application.Features.Workspaces.CheckpointSummaries;
using EHub.Infrastructure;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace EHub.ApplicationTests.Features.Workspaces.CheckpointSummaries;

public sealed class CheckpointSummaryRegistrationTests
{
    private static readonly string[] SummaryWorkers = ["CheckpointSummaryGenerationService", "CheckpointSummaryPreviewService"];

    private static IServiceCollection Register(string? provider)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Summarization:Provider"] = provider
        }).Build();
        var services = new ServiceCollection();
        services.AddInfrastructure(configuration);
        return services;
    }

    private static string[] Workers(IServiceCollection services) =>
        services.Where(item => item.ServiceType == typeof(IHostedService))
            .Select(item => item.ImplementationType?.Name ?? string.Empty)
            .Where(SummaryWorkers.Contains)
            .ToArray();

    [Theory]
    [InlineData(null)]
    [InlineData("Disabled")]
    [InlineData("disabled")]
    public void SwitchedOff_StartsNoBackgroundWork(string? provider)
    {
        var services = Register(provider);

        // Nothing runs in the background, and the settings tell uploads and the screen that the feature is off.
        Workers(services).Should().BeEmpty();
        services.Single(item => item.ServiceType == typeof(CheckpointSummarySettings))
            .ImplementationInstance.Should().BeOfType<CheckpointSummarySettings>().Which.Enabled.Should().BeFalse();
    }

    [Fact]
    public void SwitchedOn_StartsTheSummaryAndQuickViewWorkers()
    {
        var services = Register("Ollama");

        Workers(services).Should().BeEquivalentTo(SummaryWorkers);
        services.Single(item => item.ServiceType == typeof(CheckpointSummarySettings))
            .ImplementationInstance.Should().BeOfType<CheckpointSummarySettings>().Which.Enabled.Should().BeTrue();
    }

    [Fact]
    public void UnknownProvider_IsRefusedAtStartup()
    {
        var register = () => Register("OpenAI");

        register.Should().Throw<InvalidOperationException>().WithMessage("Summarization:Provider must be Disabled or Ollama.");
    }
}
