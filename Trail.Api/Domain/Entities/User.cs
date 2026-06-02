using Trail.Api.Domain.Enums;

namespace Trail.Api.Domain.Entities;

public class User
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public UserRole Role { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Submission> Submissions { get; set; } = [];
    public ICollection<Submission> Reviews { get; set; } = [];
    public ICollection<TrailEnrollment> TrailEnrollments { get; set; } = [];
    public UserSettings? Settings { get; set; }
    public ICollection<UserActivity> Activities { get; set; } = [];
    public ICollection<RefreshToken> RefreshTokens { get; set; } = [];
}
