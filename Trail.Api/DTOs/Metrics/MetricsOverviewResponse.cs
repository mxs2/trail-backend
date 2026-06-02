namespace Trail.Api.DTOs.Metrics;

public record MetricsOverviewResponse(
    int TotalStudents,
    int TotalTrails,
    int TotalChallenges,
    int TotalSubmissions,
    int ReviewedSubmissions,
    int PendingSubmissions,
    decimal CompletionRate,
    decimal CoverageRate,
    decimal? AverageScore,
    decimal? AverageLeadTimeHours
);