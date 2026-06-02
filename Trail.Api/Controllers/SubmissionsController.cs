using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Trail.Api.Application.Services;
using Trail.Api.DTOs.Submissions;

namespace Trail.Api.Controllers;

[ApiController]
[Route("submissions")]
public class SubmissionsController(SubmissionService submissionService) : ControllerBase
{
    [HttpPost]
    [Authorize(Roles = "Student")]
    [ProducesResponseType(typeof(SubmissionResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SubmissionResponse>> Create(CreateSubmissionRequest request, CancellationToken ct)
    {
        var studentId = GetUserId();
        if (studentId is null)
            return Unauthorized();

        var result = await submissionService.CreateAsync(studentId.Value, request, ct);
        if (result is null)
            return NotFound();

        return Created($"/submissions/{result.Id}", result);
    }

    [HttpGet]
    [Authorize(Roles = "Mentor,Manager")]
    [ProducesResponseType(typeof(IReadOnlyList<SubmissionResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<SubmissionResponse>>> ListPending(CancellationToken ct)
        => Ok(await submissionService.ListPendingAsync(ct));

    [HttpPut("{id:guid}/review")]
    [Authorize(Roles = "Mentor,Manager")]
    [ProducesResponseType(typeof(SubmissionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SubmissionResponse>> Review(Guid id, ReviewSubmissionRequest request, CancellationToken ct)
    {
        var reviewerId = GetUserId();
        if (reviewerId is null)
            return Unauthorized();

        var result = await submissionService.ReviewAsync(id, reviewerId.Value, request, ct);
        if (result is null)
            return NotFound();

        return Ok(result);
    }

    private Guid? GetUserId()
    {
        var value = User.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out var userId) ? userId : null;
    }
}