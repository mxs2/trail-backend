namespace Trail.Api.Domain.Entities;

public class TrailEnrollment
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid TrailId { get; set; }
    public DateTime EnrolledAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }

    public User User { get; set; } = null!;
    public Trail Trail { get; set; } = null!;
}