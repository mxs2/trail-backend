using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Trail.Api.Application.Services;
using Trail.Api.DTOs.Auth;

namespace Trail.Api.Controllers;

[ApiController]
[Route("auth")]
public class AuthController(AuthService authService) : ControllerBase
{
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

    private string? GetClaimValue(params string[] claimTypes)
        => claimTypes.Select(User.FindFirstValue).FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
}
