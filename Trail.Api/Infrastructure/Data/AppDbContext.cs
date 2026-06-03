using Microsoft.EntityFrameworkCore;
using Trail.Api.Domain.Entities;
using TrailEntity = Trail.Api.Domain.Entities.Trail;

namespace Trail.Api.Infrastructure.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<TrailEntity> Trails => Set<TrailEntity>();
    public DbSet<Challenge> Challenges => Set<Challenge>();
    public DbSet<Submission> Submissions => Set<Submission>();
    public DbSet<TrailEnrollment> TrailEnrollments => Set<TrailEnrollment>();
    public DbSet<UserSettings> UserSettings => Set<UserSettings>();
    public DbSet<UserActivity> UserActivities => Set<UserActivity>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>(e =>
        {
            e.HasKey(u => u.Id);
            e.Property(u => u.Email).IsRequired().HasMaxLength(256);
            e.Property(u => u.Name).IsRequired().HasMaxLength(256);
            e.HasIndex(u => u.Email).IsUnique();
            e.Property(u => u.Role).HasConversion<string>();

            e.HasOne(u => u.Settings)
             .WithOne(s => s.User)
             .HasForeignKey<UserSettings>(s => s.UserId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TrailEntity>(e =>
        {
            e.HasKey(t => t.Id);
            e.Property(t => t.Name).IsRequired().HasMaxLength(256);

            e.HasMany(t => t.Enrollments)
             .WithOne(e => e.Trail)
             .HasForeignKey(e => e.TrailId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Challenge>(e =>
        {
            e.HasKey(c => c.Id);
            e.Property(c => c.Title).IsRequired().HasMaxLength(256);
            e.HasOne(c => c.Trail)
             .WithMany(t => t.Challenges)
             .HasForeignKey(c => c.TrailId);
        });

        modelBuilder.Entity<Submission>(e =>
        {
            e.HasKey(s => s.Id);
            e.Property(s => s.DeliveryUrl).IsRequired().HasMaxLength(2048);
            e.Property(s => s.Feedback).HasMaxLength(4000);
            e.Property(s => s.Status).HasConversion<string>();

            e.HasOne(s => s.Student)
             .WithMany(u => u.Submissions)
             .HasForeignKey(s => s.StudentId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(s => s.Reviewer)
             .WithMany(u => u.Reviews)
             .HasForeignKey(s => s.ReviewerId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(s => s.Challenge)
             .WithMany(c => c.Submissions)
             .HasForeignKey(s => s.ChallengeId);
        });

        modelBuilder.Entity<TrailEnrollment>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.UserId, x.TrailId }).IsUnique();

            e.HasOne(x => x.User)
             .WithMany(u => u.TrailEnrollments)
             .HasForeignKey(x => x.UserId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(x => x.Trail)
             .WithMany(t => t.Enrollments)
             .HasForeignKey(x => x.TrailId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<UserSettings>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.UserId).IsUnique();
            e.Property(x => x.Language).IsRequired().HasMaxLength(10);
            e.Property(x => x.DailyStudyGoal).IsRequired().HasMaxLength(10);

            e.HasOne(x => x.User)
             .WithOne(u => u.Settings)
             .HasForeignKey<UserSettings>(x => x.UserId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<UserActivity>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.ActivityType).IsRequired().HasMaxLength(64);
            e.Property(x => x.TargetType).HasMaxLength(64);
            e.Property(x => x.TargetId).HasMaxLength(128);

            e.HasIndex(x => new { x.UserId, x.OccurredAt });

            e.HasOne(x => x.User)
             .WithMany(u => u.Activities)
             .HasForeignKey(x => x.UserId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<RefreshToken>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.TokenHash).IsRequired().HasMaxLength(128);
            e.Property(x => x.ReplacedByTokenHash).HasMaxLength(128);
            e.HasIndex(x => x.TokenHash).IsUnique();

            e.HasOne(x => x.User)
             .WithMany(u => u.RefreshTokens)
             .HasForeignKey(x => x.UserId)
             .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
