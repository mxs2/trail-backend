using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Trail.Api.Domain.Entities;
using Trail.Api.Domain.Enums;
using TrailEntity = Trail.Api.Domain.Entities.Trail;

namespace Trail.Api.Infrastructure.Data;

public static class DbSeeder
{
    public static async Task SeedAsync(AppDbContext db, ILogger logger)
    {
        await SeedUsersAsync(db, logger);
        await SeedTrailsAsync(db, logger);
        await SeedEnrollmentsAndActivityAsync(db, logger);
        await SeedSubmissionsAsync(db, logger);
    }

    private static async Task SeedUsersAsync(AppDbContext db, ILogger logger)
    {
        var hasher = new PasswordHasher<User>();

        var seeds = new List<(string Name, string Email, UserRole Role)>
        {
            ("Admin Manager",    "manager@trail.com", UserRole.Manager),
            ("Mentor Avanade",   "mentor@trail.com",  UserRole.Mentor),
            ("Estudante Teste",  "student@trail.com", UserRole.Student),
        };

        var inserted = 0;

        foreach (var (name, email, role) in seeds)
        {
            if (await db.Users.AnyAsync(u => u.Email == email)) continue;

            var user = new User
            {
                Id = Guid.NewGuid(),
                Name = name,
                Email = email,
                Role = role
            };
            user.PasswordHash = hasher.HashPassword(user, "Senha@123");
            user.Settings = new UserSettings
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                TwoFactorEnabled = false,
                PublicProfile = false,
                EmailNotifications = true,
                StudyReminder = true,
                AiSuggestions = true,
                WeeklyReport = true,
                Language = "pt-BR",
                DailyStudyGoal = "1h",
                Autoplay = true,
                Subtitles = false,
                UpdatedAt = DateTime.UtcNow
            };
            db.Users.Add(user);
            inserted++;
        }

        if (inserted > 0)
        {
            await db.SaveChangesAsync();
            logger.LogInformation("User seed completed: {Count} user(s) inserted.", inserted);
        }
        else
        {
            logger.LogInformation("User seed skipped: all seed users already exist.");
        }
    }

    private static async Task SeedTrailsAsync(AppDbContext db, ILogger logger)
    {
        if (await db.Trails.AnyAsync())
        {
            logger.LogInformation("Trail seed skipped: trails already exist.");
            return;
        }

        var fundamentos = new TrailEntity
        {
            Id = Guid.NewGuid(),
            Name = "Fundamentos de .NET",
            Description = "Trilha introdutória cobrindo C#, ASP.NET Core e Entity Framework.",
            Challenges =
            [
                new Challenge { Id = Guid.NewGuid(), Title = "Hello World em C#", Description = "Crie um console app que imprima 'Hello, Trail!'.", Order = 1 },
                new Challenge { Id = Guid.NewGuid(), Title = "API REST mínima",   Description = "Implemente um endpoint GET /ping retornando JSON.",     Order = 2 },
                new Challenge { Id = Guid.NewGuid(), Title = "CRUD com EF Core",  Description = "Modele uma entidade e exponha endpoints CRUD.",          Order = 3 },
            ]
        };

        var frontend = new TrailEntity
        {
            Id = Guid.NewGuid(),
            Name = "Frontend com Next.js",
            Description = "Trilha de frontend cobrindo React, Next.js e integração com APIs.",
            Challenges =
            [
                new Challenge { Id = Guid.NewGuid(), Title = "Setup do projeto Next.js", Description = "Inicialize um projeto Next.js com TypeScript.",      Order = 1 },
                new Challenge { Id = Guid.NewGuid(), Title = "Página de listagem",       Description = "Liste itens consumindo uma API pública.",            Order = 2 },
            ]
        };

        db.Trails.AddRange(fundamentos, frontend);
        await db.SaveChangesAsync();
        logger.LogInformation("Trail seed completed: 2 trails and 5 challenges inserted.");
    }

    private static async Task SeedEnrollmentsAndActivityAsync(AppDbContext db, ILogger logger)
    {
        var student = await db.Users.FirstOrDefaultAsync(u => u.Email == "student@trail.com");
        var trail = await db.Trails.FirstOrDefaultAsync();

        if (student is null || trail is null)
        {
            logger.LogWarning("Base seed skipped: enrollment/activity prerequisites not found.");
            return;
        }

        if (!await db.TrailEnrollments.AnyAsync(e => e.UserId == student.Id && e.TrailId == trail.Id))
        {
            db.TrailEnrollments.Add(new TrailEnrollment
            {
                Id = Guid.NewGuid(),
                UserId = student.Id,
                TrailId = trail.Id,
                EnrolledAt = DateTime.UtcNow.AddDays(-7)
            });
        }

        if (!await db.UserActivities.AnyAsync(a => a.UserId == student.Id))
        {
            db.UserActivities.AddRange(
                new UserActivity
                {
                    Id = Guid.NewGuid(),
                    UserId = student.Id,
                    ActivityType = "study",
                    TargetType = "trail",
                    TargetId = trail.Id.ToString(),
                    Minutes = 45,
                    OccurredAt = DateTime.UtcNow.AddDays(-1)
                },
                new UserActivity
                {
                    Id = Guid.NewGuid(),
                    UserId = student.Id,
                    ActivityType = "study",
                    TargetType = "trail",
                    TargetId = trail.Id.ToString(),
                    Minutes = 30,
                    OccurredAt = DateTime.UtcNow.AddDays(-2)
                });
        }

        await db.SaveChangesAsync();
        logger.LogInformation("Base seed completed: enrollment and activity records inserted.");
    }

    private static async Task SeedSubmissionsAsync(AppDbContext db, ILogger logger)
    {
        if (await db.Submissions.AnyAsync())
        {
            logger.LogInformation("Submission seed skipped: submissions already exist.");
            return;
        }

        var student = await db.Users.FirstOrDefaultAsync(u => u.Email == "student@trail.com");
        var mentor = await db.Users.FirstOrDefaultAsync(u => u.Email == "mentor@trail.com");
        var firstChallenge = await db.Challenges.OrderBy(c => c.Order).FirstOrDefaultAsync();
        var reviewedChallenge = await db.Challenges.OrderBy(c => c.Order).Skip(1).FirstOrDefaultAsync();

        if (student is null || mentor is null || firstChallenge is null || reviewedChallenge is null)
        {
            logger.LogWarning("Submission seed skipped: required seed entities not found.");
            return;
        }

        var submissions = new[]
        {
            new Submission
            {
                Id = Guid.NewGuid(),
                StudentId = student.Id,
                ChallengeId = firstChallenge.Id,
                DeliveryUrl = "https://github.com/trail/student-submission-1",
                SubmittedAt = DateTime.UtcNow.AddDays(-2),
                Status = SubmissionStatus.Submitted
            },
            new Submission
            {
                Id = Guid.NewGuid(),
                StudentId = student.Id,
                ChallengeId = reviewedChallenge.Id,
                DeliveryUrl = "https://github.com/trail/student-submission-2",
                SubmittedAt = DateTime.UtcNow.AddDays(-4),
                Status = SubmissionStatus.Reviewed,
                ReviewerId = mentor.Id,
                Score = 92,
                Feedback = "Boa entrega.",
                ReviewedAt = DateTime.UtcNow.AddDays(-3)
            }
        };

        db.Submissions.AddRange(submissions);
        await db.SaveChangesAsync();
        logger.LogInformation("Submission seed completed: 2 submissions inserted.");
    }
}
