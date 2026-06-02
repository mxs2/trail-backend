namespace Trail.Api.DTOs.Auth;

/// <summary>
/// Response returned after successful authentication.
/// </summary>
/// <param name="Token">JWT access token to be used in Authorization header.</param>
/// <param name="RefreshToken">Long-lived refresh token to obtain new access tokens.</param>
/// <param name="Role">User role.</param>
/// <param name="Name">User display name.</param>
public record LoginResponse(string Token, string RefreshToken, string Role, string Name);
