namespace Trail.Api.DTOs.Common;

public record UserSummaryResponse(
    Guid Id,
    string Name,
    string Email,
    string Role,
    string AvatarInitials,
    int Level,
    DateTime JoinedAt
);