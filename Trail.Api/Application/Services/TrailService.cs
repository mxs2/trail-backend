using Microsoft.EntityFrameworkCore;
using TrailEntity = Trail.Api.Domain.Entities.Trail;
using Trail.Api.DTOs.Trails;
using Trail.Api.Infrastructure.Data;

namespace Trail.Api.Application.Services;

public class TrailService(AppDbContext db)
{
    public async Task<IReadOnlyList<TrailResponse>> ListAsync(TrailListQuery query, CancellationToken ct = default)
    {
        query ??= new TrailListQuery();

        var trails = db.Trails.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            trails = trails.Where(t => t.Name.Contains(term) || t.Description.Contains(term));
        }

        if (!string.IsNullOrWhiteSpace(query.Level))
        {
            trails = ApplyLevelFilter(trails, query.Level.Trim());
        }

        trails = trails.OrderBy(t => t.Name);

        if (query.Page is not null || query.PerPage is not null)
        {
            const int defaultPageSize = 50;
            var page = query.Page ?? 1;
            var pageSize = query.PerPage ?? defaultPageSize;

            trails = trails.Skip((page - 1) * pageSize).Take(pageSize);
        }

        return await trails
            .Select(t => new TrailResponse(
                t.Id,
                t.Name,
                t.Description,
                t.CreatedAt,
                t.Challenges.Count,
                ResolveLevel(t.Challenges.Count),
                ResolveEstimatedHours(t.Challenges.Count)))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<ChallengeResponse>?> GetChallengesAsync(Guid trailId, CancellationToken ct = default)
    {
        var trailExists = await db.Trails.AnyAsync(t => t.Id == trailId, ct);
        if (!trailExists) return null;

        return await db.Challenges
            .AsNoTracking()
            .Where(c => c.TrailId == trailId)
            .OrderBy(c => c.Order)
            .Select(c => new ChallengeResponse(
                c.Id,
                c.TrailId,
                c.Title,
                c.Description,
                c.Order,
                c.CreatedAt,
                false,
                null,
                null))
            .ToListAsync(ct);
    }

    private static IQueryable<TrailEntity> ApplyLevelFilter(IQueryable<TrailEntity> trails, string level)
    {
        return level.ToLowerInvariant() switch
        {
            "iniciante" => trails.Where(t => t.Challenges.Count <= 3),
            "intermediario" => trails.Where(t => t.Challenges.Count > 3 && t.Challenges.Count <= 6),
            "intermediário" => trails.Where(t => t.Challenges.Count > 3 && t.Challenges.Count <= 6),
            "avancado" => trails.Where(t => t.Challenges.Count > 6),
            "avançado" => trails.Where(t => t.Challenges.Count > 6),
            _ => trails
        };
    }

    private static string ResolveLevel(int challengesCount)
        => challengesCount switch
        {
            <= 3 => "Iniciante",
            <= 6 => "Intermediário",
            _ => "Avançado"
        };

    private static decimal ResolveEstimatedHours(int challengesCount)
        => Math.Round(challengesCount * 1.5m, 1);
}
