namespace Trail.Api.DTOs.Auth;

/// <summary>
/// Request payload used to update the authenticated user's settings.
/// </summary>
/// <param name="TwoFactorEnabled">Enables two-factor authentication.</param>
/// <param name="PublicProfile">Makes the user profile visible to others.</param>
/// <param name="EmailNotifications">Enables email notifications.</param>
/// <param name="StudyReminder">Enables study reminders.</param>
/// <param name="AiSuggestions">Enables AI suggestions.</param>
/// <param name="WeeklyReport">Enables weekly reports.</param>
/// <param name="Language">Preferred language.</param>
/// <param name="DailyStudyGoal">Daily study goal.</param>
/// <param name="Autoplay">Enables autoplay.</param>
/// <param name="Subtitles">Enables subtitles.</param>
public record UpdateSettingsRequest(
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