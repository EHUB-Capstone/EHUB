using EHub.Contracts.Features;

namespace EHub.Application.Features.FeatureAvailability;

public interface IFeatureAvailabilityQueryHandler
{
    Task<FeatureAvailabilityDto> GetAsync(CancellationToken cancellationToken = default);
}
