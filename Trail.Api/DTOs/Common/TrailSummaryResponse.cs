namespace Trail.Api.DTOs.Common;

public record TrailSummaryResponse(
    Guid Id,
    string Title,
    string Subtitle,
    string Color,
    decimal Progress,
    decimal HoursTotal,
    decimal HoursDone,
    int LessonsTotal,
    int LessonsDone,
    string Level,
    string? NextLesson,
    string? AiNote
);