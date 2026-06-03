namespace Trail.Api.Domain.Entities;

public class Trail
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Challenge> Challenges { get; set; } = [];
    public ICollection<TrailEnrollment> Enrollments { get; set; } = [];
}
