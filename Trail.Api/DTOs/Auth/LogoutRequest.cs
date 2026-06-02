namespace Trail.Api.DTOs.Auth;

/// <summary>
/// Request payload used to revoke a refresh token.
/// </summary>
/// <param name="RefreshToken">Refresh token to revoke.</param>
public record LogoutRequest(string RefreshToken);