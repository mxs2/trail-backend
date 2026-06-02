using System.ComponentModel.DataAnnotations;

namespace Trail.Api.DTOs.Submissions;

public record ReviewSubmissionRequest(
    [Required][Range(0, 100)] int Score,
    [MaxLength(4000)] string? Feedback
);