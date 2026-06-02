using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Trail.Api.Domain.Entities;
using Trail.Api.DTOs.Auth;
using Trail.Api.Infrastructure.Data;

namespace Trail.Api.Application.Services;

public class AuthService(AppDbContext db, ITokenService tokenService)
{
    private readonly PasswordHasher<User> _hasher = new();

    public async Task<LoginResponse?> RegisterAsync(RegisterRequest request)
    {
        if (await db.Users.AnyAsync(u => u.Email == request.Email))
            return null;

        var user = new User
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Email = request.Email,
            Role = request.Role
        };
        user.PasswordHash = _hasher.HashPassword(user, request.Password);

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
        await db.SaveChangesAsync();

        return new LoginResponse(tokenService.GenerateToken(user), user.Role.ToString(), user.Name);
    }

    public async Task<LoginResponse?> LoginAsync(LoginRequest request)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == request.Email);
        if (user is null) return null;

        var result = _hasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
        if (result == PasswordVerificationResult.Failed) return null;

        return new LoginResponse(tokenService.GenerateToken(user), user.Role.ToString(), user.Name);
    }
}
