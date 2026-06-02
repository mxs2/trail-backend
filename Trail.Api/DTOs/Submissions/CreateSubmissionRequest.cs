using System.ComponentModel.DataAnnotations;

namespace Trail.Api.DTOs.Submissions;

public record CreateSubmissionRequest(
    [Required] Guid ChallengeId,
    [Required][Url][MaxLength(2048)] string DeliveryUrl
);