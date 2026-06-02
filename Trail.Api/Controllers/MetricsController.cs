using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Trail.Api.Application.Services;
using Trail.Api.DTOs.Metrics;

namespace Trail.Api.Controllers;

[ApiController]
[Route("metrics")]
[Authorize(Roles = "Mentor,Manager")]
public class MetricsController(SubmissionService submissionService) : ControllerBase
{
    [HttpGet("overview")]
    [ProducesResponseType(typeof(MetricsOverviewResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<MetricsOverviewResponse>> GetOverview(CancellationToken ct)
        => Ok(await submissionService.GetMetricsOverviewAsync(ct));
}