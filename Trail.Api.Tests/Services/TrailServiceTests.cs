using Trail.Api.Application.Services;
using Trail.Api.Domain.Entities;
using Trail.Api.DTOs.Trails;
using Trail.Api.Infrastructure.Data;
using Trail.Api.Tests.Helpers;
using TrailEntity = Trail.Api.Domain.Entities.Trail;
using Xunit;

namespace Trail.Api.Tests.Services;

public class TrailServiceTests
{
    private static TrailEntity MakeTrail(string name, string description, int challengeCount)
    {
        var trail = new TrailEntity
        {
            Id = Guid.NewGuid(),
            Name = name,
            Description = description
        };

        for (var i = 1; i <= challengeCount; i++)
        {
            trail.Challenges.Add(new Challenge
            {
                Id = Guid.NewGuid(),
                Title = $"{name} - Desafio {i}",
                Description = "desc",
                Order = i
            });
        }

        return trail;
    }

    // Teste B7 — O filtro de nível "iniciante" (<= 3 desafios) deve retornar
    // apenas as trilhas correspondentes, ignorando as de nível mais alto.
    [Fact]
    public async Task ListAsync_FilterByLevelIniciante_ReturnsOnlyMatchingTrails()
    {
        using var db = TestDb.NewContext();
        db.Trails.Add(MakeTrail("Trilha Curta", "intro", challengeCount: 2));   // iniciante
        db.Trails.Add(MakeTrail("Trilha Longa", "avancada", challengeCount: 8)); // avançado
        await db.SaveChangesAsync();

        var service = new TrailService(db);
        var result = await service.ListAsync(new TrailListQuery { Level = "iniciante" });

        Assert.Single(result);
        Assert.Equal("Trilha Curta", result[0].Name);
        Assert.Equal("Iniciante", result[0].Level);
    }

    // Teste B8 — A busca textual deve filtrar por nome ou descrição.
    [Fact]
    public async Task ListAsync_WithSearchTerm_FiltersByNameOrDescription()
    {
        using var db = TestDb.NewContext();
        db.Trails.Add(MakeTrail("Fundamentos de .NET", "C# e ASP.NET", 3));
        db.Trails.Add(MakeTrail("Frontend com Next.js", "React e TypeScript", 2));
        await db.SaveChangesAsync();

        var service = new TrailService(db);
        var result = await service.ListAsync(new TrailListQuery { Search = "Next" });

        Assert.Single(result);
        Assert.Equal("Frontend com Next.js", result[0].Name);
    }

    // Teste B9 — Buscar desafios de uma trilha inexistente retorna null
    // (mapeado para 404 no controller), e não uma lista vazia.
    [Fact]
    public async Task GetChallengesAsync_WithUnknownTrail_ReturnsNull()
    {
        using var db = TestDb.NewContext();
        var service = new TrailService(db);

        var result = await service.GetChallengesAsync(Guid.NewGuid());

        Assert.Null(result);
    }

    // Teste B10 — Os desafios de uma trilha devem vir ordenados pelo campo Order.
    [Fact]
    public async Task GetChallengesAsync_WithKnownTrail_ReturnsChallengesOrderedByOrder()
    {
        using var db = TestDb.NewContext();
        var trail = new TrailEntity { Id = Guid.NewGuid(), Name = "T", Description = "d" };
        trail.Challenges.Add(new Challenge { Id = Guid.NewGuid(), Title = "Terceiro", Description = "d", Order = 3 });
        trail.Challenges.Add(new Challenge { Id = Guid.NewGuid(), Title = "Primeiro", Description = "d", Order = 1 });
        trail.Challenges.Add(new Challenge { Id = Guid.NewGuid(), Title = "Segundo", Description = "d", Order = 2 });
        db.Trails.Add(trail);
        await db.SaveChangesAsync();

        var service = new TrailService(db);
        var result = await service.GetChallengesAsync(trail.Id);

        Assert.NotNull(result);
        Assert.Equal(new[] { "Primeiro", "Segundo", "Terceiro" }, result!.Select(c => c.Title).ToArray());
    }
}
