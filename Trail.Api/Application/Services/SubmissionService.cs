using Microsoft.EntityFrameworkCore;
using Trail.Api.Domain.Entities;
using Trail.Api.Domain.Enums;
using Trail.Api.DTOs.Metrics;
using Trail.Api.DTOs.Students;
using Trail.Api.DTOs.Submissions;
using Trail.Api.Infrastructure.Data;

namespace Trail.Api.Application.Services;

public class SubmissionService(AppDbContext db)
{
    public async Task<SubmissionResponse?> CreateAsync(Guid studentId, CreateSubmissionRequest request, CancellationToken ct = default)
    {
        var studentExists = await db.Users.AnyAsync(u => u.Id == studentId && u.Role == UserRole.Student, ct);
        if (!studentExists)
            return null;

        var challenge = await db.Challenges
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == request.ChallengeId, ct);

        if (challenge is null)
            return null;

        var submission = new Submission
        {
            Id = Guid.NewGuid(),
            StudentId = studentId,
            ChallengeId = request.ChallengeId,
            DeliveryUrl = request.DeliveryUrl,
            SubmittedAt = DateTime.UtcNow,
            Status = SubmissionStatus.Submitted
        };

        db.Submissions.Add(submission);
        await db.SaveChangesAsync(ct);

        return await GetByIdAsync(submission.Id, ct);
    }

    public async Task<IReadOnlyList<SubmissionResponse>> ListPendingAsync(CancellationToken ct = default)
        => await db.Submissions
            .AsNoTracking()
            .Where(s => s.Status == SubmissionStatus.Submitted)
            .OrderByDescending(s => s.SubmittedAt)
            .Select(s => new SubmissionResponse(
                s.Id,
                s.StudentId,
                s.Student.Name,
                s.ChallengeId,
                s.Challenge.Title,
                s.DeliveryUrl,
                s.SubmittedAt,
                s.Status.ToString(),
                s.ReviewerId,
                s.Reviewer != null ? s.Reviewer.Name : null,
                s.Score,
                s.Feedback,
                s.ReviewedAt))
            .ToListAsync(ct);

    public async Task<SubmissionResponse?> ReviewAsync(Guid submissionId, Guid reviewerId, ReviewSubmissionRequest request, CancellationToken ct = default)
    {
        var submission = await db.Submissions.FirstOrDefaultAsync(s => s.Id == submissionId, ct);
        if (submission is null)
            return null;

        var reviewerExists = await db.Users.AnyAsync(u => u.Id == reviewerId && u.Role == UserRole.Mentor, ct);
        if (!reviewerExists)
            return null;

        submission.ReviewerId = reviewerId;
        submission.Score = request.Score;
        submission.Feedback = request.Feedback;
        submission.ReviewedAt = DateTime.UtcNow;
        submission.Status = SubmissionStatus.Reviewed;

        await db.SaveChangesAsync(ct);
        return await GetByIdAsync(submissionId, ct);
    }

    public async Task<StudentProgressResponse?> GetStudentProgressAsync(Guid studentId, CancellationToken ct = default)
    {
        var student = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == studentId && u.Role == UserRole.Student, ct);
        if (student is null)
            return null;

        var trails = await db.Trails
            .AsNoTracking()
            .Where(t => t.Enrollments.Any(e => e.UserId == studentId) || t.Challenges.Any(c => c.Submissions.Any(s => s.StudentId == studentId)))
            .Select(t => new
            {
                t.Id,
                t.Name,
                TotalChallenges = t.Challenges.Count,
                CompletedChallenges = t.Challenges.Count(c => c.Submissions.Any(s => s.StudentId == studentId && s.Status == SubmissionStatus.Reviewed)),
                PendingChallenges = t.Challenges.Count(c => c.Submissions.Any(s => s.StudentId == studentId && s.Status == SubmissionStatus.Submitted) && !c.Submissions.Any(s => s.StudentId == studentId && s.Status == SubmissionStatus.Reviewed)),
                LastSubmissionAt = t.Challenges
                    .SelectMany(c => c.Submissions)
                    .Where(s => s.StudentId == studentId)
                    .Select(s => (DateTime?)s.SubmittedAt)
                    .OrderByDescending(s => s)
                    .FirstOrDefault()
            })
            .OrderBy(t => t.Name)
            .ToListAsync(ct);

        var totalChallenges = trails.Sum(t => t.TotalChallenges);
        var completedChallenges = trails.Sum(t => t.CompletedChallenges);
        var pendingChallenges = trails.Sum(t => t.PendingChallenges);

        var completionRate = totalChallenges == 0
            ? 0m
            : Math.Round((decimal)completedChallenges / totalChallenges * 100m, 2);

        return new StudentProgressResponse(
            student.Id,
            student.Name,
            totalChallenges,
            completedChallenges,
            pendingChallenges,
            completionRate,
            trails.Select(t => new TrailProgressItem(
                    t.Id,
                    t.Name,
                    t.TotalChallenges,
                    t.CompletedChallenges,
                    t.PendingChallenges,
                    t.TotalChallenges == 0 ? 0m : Math.Round((decimal)t.CompletedChallenges / t.TotalChallenges * 100m, 2),
                    t.LastSubmissionAt))
                .ToList());
    }

    public async Task<MetricsOverviewResponse> GetMetricsOverviewAsync(CancellationToken ct = default)
    {
        var totalStudents = await db.Users.AsNoTracking().CountAsync(u => u.Role == UserRole.Student, ct);
        var totalTrails = await db.Trails.AsNoTracking().CountAsync(ct);
        var totalChallenges = await db.Challenges.AsNoTracking().CountAsync(ct);
        var totalSubmissions = await db.Submissions.AsNoTracking().CountAsync(ct);
        var reviewedSubmissions = await db.Submissions.AsNoTracking().CountAsync(s => s.Status == SubmissionStatus.Reviewed, ct);

        var reviewedChallenges = await db.Submissions
            .AsNoTracking()
            .Where(s => s.Status == SubmissionStatus.Reviewed)
            .Select(s => s.ChallengeId)
            .Distinct()
            .CountAsync(ct);

        var averageScore = await db.Submissions
            .AsNoTracking()
            .Where(s => s.Score.HasValue)
            .Select(s => (decimal?)s.Score)
            .AverageAsync(ct);

        var leadTimeHours = await db.Submissions
            .AsNoTracking()
            .Where(s => s.ReviewedAt.HasValue)
            .Select(s => (decimal?)EF.Functions.DateDiffMinute(s.SubmittedAt, s.ReviewedAt!.Value) / 60m)
            .AverageAsync(ct);

        var completionRate = totalChallenges == 0
            ? 0m
            : Math.Round((decimal)reviewedChallenges / totalChallenges * 100m, 2);

        var coverageRate = totalSubmissions == 0
            ? 0m
            : Math.Round((decimal)reviewedSubmissions / totalSubmissions * 100m, 2);

        return new MetricsOverviewResponse(
            totalStudents,
            totalTrails,
            totalChallenges,
            totalSubmissions,
            reviewedSubmissions,
            totalSubmissions - reviewedSubmissions,
            completionRate,
            coverageRate,
            averageScore is null ? null : Math.Round(averageScore.Value, 2),
            leadTimeHours is null ? null : Math.Round(leadTimeHours.Value, 2));
    }

    private async Task<SubmissionResponse?> GetByIdAsync(Guid submissionId, CancellationToken ct = default)
        => await db.Submissions
            .AsNoTracking()
            .Where(s => s.Id == submissionId)
            .Select(s => new SubmissionResponse(
                s.Id,
                s.StudentId,
                s.Student.Name,
                s.ChallengeId,
                s.Challenge.Title,
                s.DeliveryUrl,
                s.SubmittedAt,
                s.Status.ToString(),
                s.ReviewerId,
                s.Reviewer != null ? s.Reviewer.Name : null,
                s.Score,
                s.Feedback,
                s.ReviewedAt))
            .FirstOrDefaultAsync(ct);
}