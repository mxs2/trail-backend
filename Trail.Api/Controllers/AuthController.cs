using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Trail.Api.Application.Services;
using Trail.Api.DTOs.Auth;
using Trail.Api.DTOs.Common;

namespace Trail.Api.Controllers;

/// <summary>
/// Authentication and user profile endpoints.
/// </summary>
[ApiController]
[Route("auth")]
public class AuthController(AuthService authService) : ControllerBase
{
    /// <summary>
    /// Registers a new user and returns access and refresh tokens.
    /// </summary>
    /// <param name="request">Registration data.</param>
    /// <returns>Authentication payload.</returns>
    [HttpPost("register")]
    public async Task<ActionResult<LoginResponse>> Register(RegisterRequest request)
    {
        var result = await authService.RegisterAsync(request);
        if (result is null)
            return Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Conflict",
                detail: "Email já cadastrado.");

        return Ok(result);
    }

    /// <summary>
    /// Authenticates a user and returns access and refresh tokens.
    /// </summary>
    /// <param name="request">Login credentials.</param>
    /// <returns>Authentication payload.</returns>
    [HttpPost("login")]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest request)
    {
        var result = await authService.LoginAsync(request);
        if (result is null)
            return Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: "Unauthorized",
                detail: "Email ou senha inválidos.");

        return Ok(result);
    }

    /// <summary>
    /// Returns the currently authenticated user's claims.
    /// </summary>
    /// <returns>Authenticated user claims.</returns>
    [HttpGet("me")]
    [Authorize]
    public IActionResult Me()
    {
        var id = GetClaimValue(JwtRegisteredClaimNames.Sub, ClaimTypes.NameIdentifier);
        var email = GetClaimValue(JwtRegisteredClaimNames.Email, ClaimTypes.Email);
        var name = GetClaimValue("name", ClaimTypes.Name);
        var role = GetClaimValue(ClaimTypes.Role, "role");

        return Ok(new { id, email, name, role });
    }

    /// <summary>
    /// Rotates the refresh token and returns a new access token.
    /// </summary>
    /// <param name="request">Refresh token payload.</param>
    /// <returns>New authentication payload.</returns>
    [HttpPost("refresh")]
    public async Task<ActionResult<LoginResponse>> Refresh(RefreshRequest request)
    {
        var result = await authService.RefreshAsync(request);
        if (result is null)
            return Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Unauthorized", detail: "Invalid or expired refresh token.");

        return Ok(result);
    }

    /// <summary>
    /// Revokes the provided refresh token.
    /// </summary>
    /// <param name="request">Refresh token to revoke.</param>
    /// <returns>No content when the token is revoked.</returns>
    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout(LogoutRequest request)
    {
        var ok = await authService.LogoutAsync(request);
        if (!ok)
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Bad Request", detail: "Refresh token not found or already revoked.");

        return NoContent();
    }

    /// <summary>
    /// Returns the authenticated user's profile summary.
    /// </summary>
    /// <returns>User profile summary.</returns>
    [HttpGet("profile")]
    [Authorize]
    public async Task<ActionResult<UserSummaryResponse>> Profile()
    {
        var idClaim = GetClaimValue(JwtRegisteredClaimNames.Sub, ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(idClaim, out var id)) return Unauthorized();

        var profile = await authService.GetProfileAsync(id);
        if (profile is null) return NotFound();
        return Ok(profile);
    }

    /// <summary>
    /// Updates the authenticated user's profile.
    /// </summary>
    /// <param name="request">Profile data to update.</param>
    /// <returns>No content when updated.</returns>
    [HttpPut("profile")]
    [Authorize]
    public async Task<IActionResult> UpdateProfile(UpdateProfileRequest request)
    {
        var idClaim = GetClaimValue(JwtRegisteredClaimNames.Sub, ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(idClaim, out var id)) return Unauthorized();

        var ok = await authService.UpdateProfileAsync(id, request);
        if (!ok) return NotFound();
        return NoContent();
    }

    /// <summary>
    /// Returns the authenticated user's settings.
    /// </summary>
    /// <returns>User settings.</returns>
    [HttpGet("settings")]
    [Authorize]
    public async Task<ActionResult<UserSettingsResponse>> GetSettings()
    {
        var idClaim = GetClaimValue(JwtRegisteredClaimNames.Sub, ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(idClaim, out var id)) return Unauthorized();

        var s = await authService.GetSettingsAsync(id);
        if (s is null) return NotFound();
        return Ok(s);
    }

    /// <summary>
    /// Updates the authenticated user's settings.
    /// </summary>
    /// <param name="request">Settings payload.</param>
    /// <returns>No content when updated.</returns>
    [HttpPut("settings")]
    [Authorize]
    public async Task<IActionResult> UpdateSettings(UpdateSettingsRequest request)
    {
        var idClaim = GetClaimValue(JwtRegisteredClaimNames.Sub, ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(idClaim, out var id)) return Unauthorized();

        var ok = await authService.UpdateSettingsAsync(id, request);
        if (!ok) return NotFound();
        return NoContent();
    }

    /// <summary>
    /// Returns weekly activity for the authenticated user.
    /// </summary>
    /// <returns>Weekly activity series.</returns>
    [HttpGet("activity/weekly")]
    [Authorize]
    public async Task<ActionResult<IEnumerable<WeeklyActivityResponse>>> WeeklyActivity()
    {
        var idClaim = GetClaimValue(JwtRegisteredClaimNames.Sub, ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(idClaim, out var id)) return Unauthorized();

        var items = await authService.GetWeeklyActivityAsync(id);
        return Ok(items);
    }

    private string? GetClaimValue(params string[] claimTypes)
        => claimTypes.Select(User.FindFirstValue).FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
}
