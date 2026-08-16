using System.Net;
using EHub.Api.Extensions;
using FluentAssertions;

namespace EHub.IntegrationTests.Common;

[Collection("Sequential")]
public sealed class DeploymentReadinessTests
{
    private readonly HttpClient _client;

    public DeploymentReadinessTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Liveness_And_Readiness_Endpoints_Should_Be_Healthy()
    {
        var liveResponse = await _client.GetAsync("/health/live");
        var readyResponse = await _client.GetAsync("/health/ready");

        liveResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        readyResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("--initialize-database", true)]
    [InlineData("--INITIALIZE-DATABASE", true)]
    [InlineData("--other-command", false)]
    public void Initialization_Argument_Should_Be_Explicit_And_Case_Insensitive(
        string argument,
        bool expected)
    {
        DatabaseInitializationExtensions
            .IsInitializationRequested([argument])
            .Should()
            .Be(expected);
    }
}
