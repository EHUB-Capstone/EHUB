using EHub.Application.Common.FeatureFlags;
using EHub.Application.Common.Interfaces.AI;
using Microsoft.Extensions.Configuration;

namespace EHub.Infrastructure.Services;

public sealed class ConfigurationAiFeatureGate(IConfiguration configuration) : IAiFeatureGate
{
    public bool IsEnabled => FeatureFlagValue.IsEnabled(configuration["Features:AI:Enabled"]);
}
