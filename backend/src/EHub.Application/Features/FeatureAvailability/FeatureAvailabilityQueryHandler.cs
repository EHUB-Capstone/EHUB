using EHub.Application.Common.Interfaces.AI;
using EHub.Contracts.Features;

namespace EHub.Application.Features.FeatureAvailability;

public sealed class FeatureAvailabilityQueryHandler(IAiFeatureGate aiFeatureGate) : IFeatureAvailabilityQueryHandler
{
    public Task<FeatureAvailabilityDto> GetAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(new FeatureAvailabilityDto { AiEnabled = aiFeatureGate.IsEnabled });
}
