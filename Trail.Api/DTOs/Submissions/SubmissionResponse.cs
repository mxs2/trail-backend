namespace Trail.Api.DTOs.Submissions;

public record SubmissionResponse(
    Guid Id,
    Guid StudentId,
    string StudentName,
    Guid ChallengeId,
    string ChallengeTitle,
    string DeliveryUrl,
    DateTime SubmittedAt,
    string Status,
    Guid? ReviewerId,
    string? ReviewerName,
    int? Score,
    string? Feedback,
    DateTime? ReviewedAt
);