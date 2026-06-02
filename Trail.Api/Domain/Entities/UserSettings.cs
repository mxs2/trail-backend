namespace Trail.Api.Domain.Entities;

public class UserSettings
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public bool TwoFactorEnabled { get; set; }
    public bool PublicProfile { get; set; }
    public bool EmailNotifications { get; set; } = true;
    public bool StudyReminder { get; set; } = true;
    public bool AiSuggestions { get; set; } = true;
    public bool WeeklyReport { get; set; } = true;
    public string Language { get; set; } = "pt-BR";
    public string DailyStudyGoal { get; set; } = "1h";
    public bool Autoplay { get; set; } = true;
    public bool Subtitles { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public User User { get; set; } = null!;
}