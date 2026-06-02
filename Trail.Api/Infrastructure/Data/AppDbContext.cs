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
        });

        modelBuilder.Entity<TrailEntity>(e =>
        {
            e.HasKey(t => t.Id);
            e.Property(t => t.Name).IsRequired().HasMaxLength(256);
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
    }
}
