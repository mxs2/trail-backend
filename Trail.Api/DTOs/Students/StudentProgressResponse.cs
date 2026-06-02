namespace Trail.Api.DTOs.Students;

public record StudentProgressResponse(
    Guid StudentId,
    string StudentName,
    int TotalChallenges,
    int CompletedChallenges,
    int PendingChallenges,
    decimal CompletionRate,
    IReadOnlyList<TrailProgressItem> Trails
);