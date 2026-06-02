namespace Trail.Api.DTOs.Students;

public record TrailProgressItem(
    Guid TrailId,
    string TrailName,
    int TotalChallenges,
    int CompletedChallenges,
    int PendingChallenges,
    decimal CompletionRate,
    DateTime? LastSubmissionAt
);