using EHub.Application.Features.FeatureAvailability;
using EHub.Contracts.Common;
using EHub.Contracts.Features;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EHub.Api.Controllers;

[ApiController]
[Route("api/features")]
[Authorize]
public sealed class FeaturesController : ControllerBase
{
    [HttpGet]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<ActionResult<ApiResponse<FeatureAvailabilityDto>>> Get(
        [FromServices] IFeatureAvailabilityQueryHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.GetAsync(cancellationToken);
        return Ok(ApiResponse<FeatureAvailabilityDto>.SuccessResponse(result, "Feature availability retrieved."));
    }
}
