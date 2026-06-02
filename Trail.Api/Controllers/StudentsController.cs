using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Trail.Api.Application.Services;
using Trail.Api.DTOs.Students;

namespace Trail.Api.Controllers;

[ApiController]
[Route("students")]
[Authorize]
public class StudentsController(SubmissionService submissionService) : ControllerBase
{
    [HttpGet("{id:guid}/progress")]
    [ProducesResponseType(typeof(StudentProgressResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<StudentProgressResponse>> GetProgress(Guid id, CancellationToken ct)
    {
        var result = await submissionService.GetStudentProgressAsync(id, ct);
        if (result is null)
            return NotFound();

        return Ok(result);
    }
}