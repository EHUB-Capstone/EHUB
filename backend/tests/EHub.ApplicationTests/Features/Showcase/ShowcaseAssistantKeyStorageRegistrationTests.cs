using System.Security.Cryptography;
using EHub.Application.Common.Interfaces.Services;
using EHub.Infrastructure.Showcase.Assistant;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace EHub.ApplicationTests.Features.Showcase;

/// <summary>What the application does at start with each way the encryption key can be configured.</summary>
public sealed class ShowcaseAssistantKeyStorageRegistrationTests
{
    private const string Prefix = "Showcase:Assistant:KeyEncryption:";
    private static string NewKey() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

    private static IHost Build(Dictionary<string, string?> settings)
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { DisableDefaults = true });
        builder.Configuration.AddInMemoryCollection(settings);
        builder.Services.AddShowcaseAssistantKeyStorage(builder.Configuration);
        return builder.Build();
    }

    [Fact]
    public async Task ACorrectConfigurationStartsAndTurnsStorageOn()
    {
        using var host = Build(new() { [$"{Prefix}ActiveKeyId"] = "1", [$"{Prefix}Keys:1"] = NewKey() });

        await host.StartAsync();

        host.Services.GetRequiredService<IShowcaseAssistantKeyProtector>().IsAvailable.Should().BeTrue();
        await host.StopAsync();
    }

    [Fact]
    public async Task ARotationConfigurationWithTheOldKeyStillListedStarts()
    {
        using var host = Build(new() { [$"{Prefix}ActiveKeyId"] = "2", [$"{Prefix}Keys:1"] = NewKey(), [$"{Prefix}Keys:2"] = NewKey(), [$"{Prefix}Keys:3"] = "" });

        await host.StartAsync();

        host.Services.GetRequiredService<IShowcaseAssistantKeyProtector>().IsAvailable.Should().BeTrue();
        await host.StopAsync();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NoConfigurationAtAllStartsWithStorageOff_WhetherTheSettingsAreMissingOrBlank(bool blankSettings)
    {
        var settings = blankSettings
            ? new Dictionary<string, string?> { [$"{Prefix}ActiveKeyId"] = "", [$"{Prefix}Keys:1"] = "", [$"{Prefix}Keys:2"] = " " }
            : new Dictionary<string, string?>();
        using var host = Build(settings);

        await host.StartAsync();

        host.Services.GetRequiredService<IShowcaseAssistantKeyProtector>().IsAvailable.Should().BeFalse();
        await host.StopAsync();
    }

    [Theory]
    [InlineData("an active id without any key")]
    [InlineData("keys without an active id")]
    [InlineData("the active id is not among the keys")]
    [InlineData("a key that is not Base64")]
    [InlineData("a key of the wrong length")]
    [InlineData("an all-zero placeholder key")]
    [InlineData("an id outside 1 to 255")]
    public async Task AWrongOrPartialConfigurationStopsTheApplicationAtStart_WithoutRevealingAnyKey(string problem)
    {
        var good = NewKey();
        var settings = problem switch
        {
            "an active id without any key" => new Dictionary<string, string?> { [$"{Prefix}ActiveKeyId"] = "1" },
            "keys without an active id" => new() { [$"{Prefix}Keys:1"] = good },
            "the active id is not among the keys" => new() { [$"{Prefix}ActiveKeyId"] = "2", [$"{Prefix}Keys:1"] = good },
            "a key that is not Base64" => new() { [$"{Prefix}ActiveKeyId"] = "1", [$"{Prefix}Keys:1"] = "replace-with-the-real-key!!" },
            "a key of the wrong length" => new() { [$"{Prefix}ActiveKeyId"] = "1", [$"{Prefix}Keys:1"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(16)) },
            "an all-zero placeholder key" => new() { [$"{Prefix}ActiveKeyId"] = "1", [$"{Prefix}Keys:1"] = Convert.ToBase64String(new byte[32]) },
            _ => new() { [$"{Prefix}ActiveKeyId"] = "256", [$"{Prefix}Keys:256"] = good },
        };
        using var host = Build(settings);

        var start = async () => await host.StartAsync();

        var failure = (await start.Should().ThrowAsync<OptionsValidationException>(problem)).Which;
        // The message is fixed text. Whatever was configured as key material (anything longer than an id) is never repeated in it.
        foreach (var keyMaterial in settings.Values.Where(value => value is { Length: > 8 })) failure.Message.Should().NotContain(keyMaterial!);
        failure.Message.Should().Contain("Showcase:Assistant:KeyEncryption");
    }
}
