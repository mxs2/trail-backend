namespace Trail.Api.DTOs.Common;

public record UserSettingsResponse(
    bool TwoFactorEnabled,
    bool PublicProfile,
    bool EmailNotifications,
    bool StudyReminder,
    bool AiSuggestions,
    bool WeeklyReport,
    string Language,
    string DailyStudyGoal,
    bool Autoplay,
    bool Subtitles
);