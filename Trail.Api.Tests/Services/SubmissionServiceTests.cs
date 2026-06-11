using Trail.Api.Application.Services;
using Trail.Api.Domain.Entities;
using Trail.Api.Domain.Enums;
using Trail.Api.DTOs.Submissions;
using Trail.Api.Infrastructure.Data;
using Trail.Api.Tests.Helpers;
using TrailEntity = Trail.Api.Domain.Entities.Trail;
using Xunit;

namespace Trail.Api.Tests.Services;

public class SubmissionServiceTests
{
    private static (User student, User mentor, Challenge challenge) SeedBaseData(AppDbContext db)
    {
        var student = new User { Id = Guid.NewGuid(), Name = "Aluno", Email = "aluno@trail.com", Role = UserRole.Student };
        var mentor = new User { Id = Guid.NewGuid(), Name = "Mentor", Email = "mentor@trail.com", Role = UserRole.Mentor };

        var trail = new TrailEntity { Id = Guid.NewGuid(), Name = "Trilha", Description = "d" };
        var challenge = new Challenge { Id = Guid.NewGuid(), TrailId = trail.Id, Title = "Desafio 1", Description = "d", Order = 1 };
        trail.Challenges.Add(challenge);

        db.Users.AddRange(student, mentor);
        db.Trails.Add(trail);
        db.SaveChanges();

        return (student, mentor, challenge);
    }

    // Teste B11 — Submeter para um desafio inexistente retorna null (404),
    // evitando submissões órfãs.
    [Fact]
    public async Task CreateAsync_WithUnknownChallenge_ReturnsNull()
    {
        using var db = TestDb.NewContext();
        var (student, _, _) = SeedBaseData(db);
        var service = new SubmissionService(db);

        var request = new CreateSubmissionRequest(Guid.NewGuid(), "https://github.com/aluno/repo");
        var result = await service.CreateAsync(student.Id, request);

        Assert.Null(result);
    }

    // Teste B12 — Apenas usuários com papel Student podem submeter; um mentor
    // tentando submeter recebe null (regra de negócio do service).
    [Fact]
    public async Task CreateAsync_WhenAuthorIsNotStudent_ReturnsNull()
    {
        using var db = TestDb.NewContext();
        var (_, mentor, challenge) = SeedBaseData(db);
        var service = new SubmissionService(db);

        var request = new CreateSubmissionRequest(challenge.Id, "https://github.com/mentor/repo");
        var result = await service.CreateAsync(mentor.Id, request);

        Assert.Null(result);
    }

    // Teste B13 — Submissão válida é criada com status "Submitted".
    [Fact]
    public async Task CreateAsync_WithValidData_CreatesSubmittedSubmission()
    {
        using var db = TestDb.NewContext();
        var (student, _, challenge) = SeedBaseData(db);
        var service = new SubmissionService(db);

        var request = new CreateSubmissionRequest(challenge.Id, "https://github.com/aluno/repo");
        var result = await service.CreateAsync(student.Id, request);

        Assert.NotNull(result);
        Assert.Equal("Submitted", result!.Status);
        Assert.Equal(challenge.Id, result.ChallengeId);
        Assert.Equal(student.Id, result.StudentId);
    }

    // Teste B14 — A revisão por um mentor muda o status para "Reviewed" e grava
    // nota, feedback, revisor e data — o coração do fluxo mentor↔aluno.
    [Fact]
    public async Task ReviewAsync_ByMentor_SetsReviewedStatusAndFields()
    {
        using var db = TestDb.NewContext();
        var (student, mentor, challenge) = SeedBaseData(db);
        var submission = new Submission
        {
            Id = Guid.NewGuid(),
            StudentId = student.Id,
            ChallengeId = challenge.Id,
            DeliveryUrl = "https://github.com/aluno/repo",
            SubmittedAt = DateTime.UtcNow,
            Status = SubmissionStatus.Submitted
        };
        db.Submissions.Add(submission);
        await db.SaveChangesAsync();

        var service = new SubmissionService(db);
        var result = await service.ReviewAsync(submission.Id, mentor.Id, new ReviewSubmissionRequest(90, "Bom trabalho"));

        Assert.NotNull(result);
        Assert.Equal("Reviewed", result!.Status);
        Assert.Equal(90, result.Score);
        Assert.Equal("Bom trabalho", result.Feedback);
        Assert.Equal(mentor.Id, result.ReviewerId);
        Assert.NotNull(result.ReviewedAt);
    }

    // Teste B15 — Um revisor que não é Mentor não pode avaliar (retorna null).
    [Fact]
    public async Task ReviewAsync_WhenReviewerIsNotMentor_ReturnsNull()
    {
        using var db = TestDb.NewContext();
        var (student, _, challenge) = SeedBaseData(db);
        var submission = new Submission
        {
            Id = Guid.NewGuid(),
            StudentId = student.Id,
            ChallengeId = challenge.Id,
            DeliveryUrl = "https://github.com/aluno/repo",
            SubmittedAt = DateTime.UtcNow,
            Status = SubmissionStatus.Submitted
        };
        db.Submissions.Add(submission);
        await db.SaveChangesAsync();

        var service = new SubmissionService(db);
        // student.Id não tem papel Mentor → não deve revisar
        var result = await service.ReviewAsync(submission.Id, student.Id, new ReviewSubmissionRequest(50, null));

        Assert.Null(result);
    }

    // Teste B16 — A fila de pendentes retorna apenas submissões "Submitted",
    // ordenadas da mais recente para a mais antiga.
    [Fact]
    public async Task ListPendingAsync_ReturnsOnlySubmittedOrderedByMostRecent()
    {
        using var db = TestDb.NewContext();
        var (student, mentor, challenge) = SeedBaseData(db);

        var older = new Submission
        {
            Id = Guid.NewGuid(), StudentId = student.Id, ChallengeId = challenge.Id,
            DeliveryUrl = "https://github.com/aluno/repo-old",
            SubmittedAt = DateTime.UtcNow.AddDays(-2), Status = SubmissionStatus.Submitted
        };
        var newer = new Submission
        {
            Id = Guid.NewGuid(), StudentId = student.Id, ChallengeId = challenge.Id,
            DeliveryUrl = "https://github.com/aluno/repo-new",
            SubmittedAt = DateTime.UtcNow, Status = SubmissionStatus.Submitted
        };
        var reviewed = new Submission
        {
            Id = Guid.NewGuid(), StudentId = student.Id, ChallengeId = challenge.Id,
            DeliveryUrl = "https://github.com/aluno/repo-done",
            SubmittedAt = DateTime.UtcNow.AddDays(-1), Status = SubmissionStatus.Reviewed,
            ReviewerId = mentor.Id, Score = 80, ReviewedAt = DateTime.UtcNow
        };
        db.Submissions.AddRange(older, newer, reviewed);
        await db.SaveChangesAsync();

        var service = new SubmissionService(db);
        var result = await service.ListPendingAsync();

        Assert.Equal(2, result.Count); // a revisada é excluída
        Assert.Equal(newer.Id, result[0].Id); // mais recente primeiro
        Assert.Equal(older.Id, result[1].Id);
    }
}
