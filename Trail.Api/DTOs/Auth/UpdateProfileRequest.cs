namespace Trail.Api.DTOs.Auth;

/// <summary>
/// Request payload used to update the authenticated user's profile.
/// </summary>
/// <param name="Name">New display name.</param>
public record UpdateProfileRequest(string Name);