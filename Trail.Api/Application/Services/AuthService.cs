using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Trail.Api.Domain.Entities;
using Trail.Api.DTOs.Auth;
using Trail.Api.DTOs.Common;
using Trail.Api.Infrastructure.Data;
using Trail.Api.Configuration;

namespace Trail.Api.Application.Services;

public class AuthService(AppDbContext db, ITokenService tokenService, IOptions<RefreshOptions> refreshOptions)
{
    private readonly PasswordHasher<User> _hasher = new();
    private readonly RefreshOptions _refresh = refreshOptions.Value;

    private static string ComputeHash(string input)
    {
        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes);
    }

    private async Task<string> CreateAndStoreRefreshTokenAsync(User user)
    {
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(64));
        var hash = ComputeHash(token);

        var refresh = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = hash,
            ExpiresAt = DateTime.UtcNow.AddDays(_refresh.ExpirationDays),
            CreatedAt = DateTime.UtcNow
        };

        db.RefreshTokens.Add(refresh);
        await db.SaveChangesAsync();

        return token;
    }

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

        var refreshToken = await CreateAndStoreRefreshTokenAsync(user);

        return new LoginResponse(tokenService.GenerateToken(user), refreshToken, user.Role.ToString(), user.Name);
    }

    public async Task<LoginResponse?> LoginAsync(LoginRequest request)
    {
        var user = await db.Users.Include(u => u.Settings).FirstOrDefaultAsync(u => u.Email == request.Email);
        if (user is null) return null;

        var result = _hasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
        if (result == PasswordVerificationResult.Failed) return null;

        var refreshToken = await CreateAndStoreRefreshTokenAsync(user);

        return new LoginResponse(tokenService.GenerateToken(user), refreshToken, user.Role.ToString(), user.Name);
    }

    public async Task<LoginResponse?> RefreshAsync(RefreshRequest request)
    {
        var hash = ComputeHash(request.RefreshToken);
        var existing = await db.RefreshTokens.Include(r => r.User).FirstOrDefaultAsync(r => r.TokenHash == hash);
        if (existing is null) return null;
        if (existing.RevokedAt != null) return null;
        if (existing.ExpiresAt < DateTime.UtcNow) return null;

        // rotate
        existing.RevokedAt = DateTime.UtcNow;
        var newToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(64));
        var newHash = ComputeHash(newToken);
        existing.ReplacedByTokenHash = newHash;

        var newRefresh = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = existing.UserId,
            TokenHash = newHash,
            ExpiresAt = DateTime.UtcNow.AddDays(_refresh.ExpirationDays),
            CreatedAt = DateTime.UtcNow
        };

        db.RefreshTokens.Add(newRefresh);
        await db.SaveChangesAsync();

        return new LoginResponse(tokenService.GenerateToken(existing.User), newToken, existing.User.Role.ToString(), existing.User.Name);
    }

    public async Task<bool> LogoutAsync(LogoutRequest request)
    {
        var hash = ComputeHash(request.RefreshToken);
        var existing = await db.RefreshTokens.FirstOrDefaultAsync(r => r.TokenHash == hash);
        if (existing is null) return false;
        if (existing.RevokedAt != null) return false;

        existing.RevokedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return true;
    }

    public async Task<UserSummaryResponse?> GetProfileAsync(Guid userId)
    {
        var u = await db.Users.FirstOrDefaultAsync(x => x.Id == userId);
        if (u is null) return null;

        return new UserSummaryResponse(u.Id, u.Name, u.Email, u.Role.ToString(),
            (u.Name.Length > 0 ? u.Name[..1].ToUpper() : ""), 1, u.CreatedAt);
    }

    public async Task<UserSettingsResponse?> GetSettingsAsync(Guid userId)
    {
        var s = await db.UserSettings.FirstOrDefaultAsync(x => x.UserId == userId);
        if (s is null) return null;

        return new UserSettingsResponse(
            s.TwoFactorEnabled,
            s.PublicProfile,
            s.EmailNotifications,
            s.StudyReminder,
            s.AiSuggestions,
            s.WeeklyReport,
            s.Language,
            s.DailyStudyGoal,
            s.Autoplay,
            s.Subtitles
        );
    }

    public async Task<bool> UpdateSettingsAsync(Guid userId, UpdateSettingsRequest request)
    {
        var s = await db.UserSettings.FirstOrDefaultAsync(x => x.UserId == userId);
        if (s is null) return false;

        s.TwoFactorEnabled = request.TwoFactorEnabled;
        s.PublicProfile = request.PublicProfile;
        s.EmailNotifications = request.EmailNotifications;
        s.StudyReminder = request.StudyReminder;
        s.AiSuggestions = request.AiSuggestions;
        s.WeeklyReport = request.WeeklyReport;
        s.Language = request.Language;
        s.DailyStudyGoal = request.DailyStudyGoal;
        s.Autoplay = request.Autoplay;
        s.Subtitles = request.Subtitles;
        s.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> UpdateProfileAsync(Guid userId, UpdateProfileRequest request)
    {
        var u = await db.Users.FirstOrDefaultAsync(x => x.Id == userId);
        if (u is null) return false;

        u.Name = request.Name;
        await db.SaveChangesAsync();
        return true;
    }

    public async Task<IEnumerable<WeeklyActivityResponse>> GetWeeklyActivityAsync(Guid userId)
    {
        var since = DateTime.UtcNow.Date.AddDays(-6);
        var items = await db.UserActivities
            .Where(a => a.UserId == userId && a.OccurredAt >= since)
            .ToListAsync();

        var grouped = items.GroupBy(a => a.OccurredAt.Date)
            .Select(g => new WeeklyActivityResponse(g.Key.ToString("ddd"), g.Sum(x => x.Minutes)))
            .ToList();

        // ensure 7 days
        var result = new List<WeeklyActivityResponse>();
        for (int i = 0; i < 7; i++)
        {
            var day = since.AddDays(i);
            var found = grouped.FirstOrDefault(g => g.Day == day.ToString("ddd"));
            result.Add(found ?? new WeeklyActivityResponse(day.ToString("ddd"), 0));
        }

        return result;
    }
}
