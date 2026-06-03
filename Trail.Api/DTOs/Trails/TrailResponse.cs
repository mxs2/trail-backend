namespace Trail.Api.DTOs.Trails;

public record TrailResponse(
	Guid Id,
	string Name,
	string Description,
	DateTime CreatedAt,
	int ChallengesCount,
	string? Level,
	decimal? EstimatedHours
);
