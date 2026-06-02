namespace Trail.Api.DTOs.Auth;

/// <summary>
/// Request payload used to exchange a refresh token for a new access token.
/// </summary>
/// <param name="RefreshToken">Refresh token previously issued by the API.</param>
public record RefreshRequest(string RefreshToken);

/// <summary>
/// Authentication response returned when refreshing a token.
/// </summary>
/// <param name="Token">New JWT access token.</param>
/// <param name="RefreshToken">New refresh token.</param>
/// <param name="Role">User role.</param>
/// <param name="Name">User display name.</param>
public record RefreshResponse(string Token, string RefreshToken, string Role, string Name);