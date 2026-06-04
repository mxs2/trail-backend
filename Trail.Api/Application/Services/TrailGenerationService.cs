using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Trail.Api.Domain.Entities;
using Trail.Api.DTOs.Ai;
using Trail.Api.Infrastructure.Data;
using TrailEntity = Trail.Api.Domain.Entities.Trail;

namespace Trail.Api.Application.Services;

/// <summary>
/// Drives the end-to-end AI trail generation pipeline:
/// persist onboarding profile, build prompt, call Anthropic with
/// structured output, persist trail and challenges, auto-enroll student.
/// </summary>
public class TrailGenerationService(
    AppDbContext db,
    IAiService ai)
{
    // ── Tool schema (Generic AI JSON Schema for structured output) ─────────────

    private static readonly AiTool GenerateTool = new(
        Name: "generate_learning_trail",
        Description:
            "Generate a hyper-personalised software-engineering learning trail " +
            "for a student based on their profile. " +
            "Return ONLY the structured trail with sequential, practical challenges.",
        InputSchema: BuildSchema());

    // ── Public surface ────────────────────────────────────────────────────────

    public async Task<GenerateTrailResult> GenerateAsync(
        Guid studentId,
        OnboardingProfileRequest profile,
        CancellationToken ct = default)
    {
        // 1. Persist profile (upsert) ──────────────────────────────────────────
        await UpsertProfileAsync(studentId, profile, ct);

        // 2. Build prompt ──────────────────────────────────────────────────────
        var system = BuildSystemPrompt();
        var userMsg = BuildUserMessage(profile);

        // 3. Call AI (tool_choice forces structured output) ───────────────────
        var generated = await ai.InvokeToolAsync<GeneratedTrailDto>(
            system,
            [new AiMessage("user", userMsg)],
            GenerateTool,
            ct);

        // 4. Validate & persist ────────────────────────────────────────────────
        ValidateGenerated(generated);
        var trailId = await PersistTrailAsync(generated, ct);

        // 5. Auto-enroll ───────────────────────────────────────────────────────
        var alreadyEnrolled = await db.TrailEnrollments
            .AnyAsync(e => e.UserId == studentId && e.TrailId == trailId, ct);

        if (!alreadyEnrolled)
        {
            db.TrailEnrollments.Add(new TrailEnrollment
            {
                Id = Guid.NewGuid(),
                UserId = studentId,
                TrailId = trailId,
                EnrolledAt = DateTime.UtcNow,
            });
            await db.SaveChangesAsync(ct);
        }

        return new GenerateTrailResult(trailId, generated.Title);
    }

    // ── Persistence helpers ───────────────────────────────────────────────────

    private async Task UpsertProfileAsync(
        Guid userId, OnboardingProfileRequest req, CancellationToken ct)
    {
        var existing = await db.OnboardingProfiles
            .FirstOrDefaultAsync(p => p.UserId == userId, ct);

        if (existing is null)
        {
            db.OnboardingProfiles.Add(new StudentOnboardingProfile
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                TargetRole = req.TargetRole,
                TechnicalDepth = req.TechnicalDepth,
                WeeklyHours = req.WeeklyHours,
                LearningStyle = req.LearningStyle,
                ProjectGoal = req.ProjectGoal,
            });
        }
        else
        {
            existing.TargetRole = req.TargetRole;
            existing.TechnicalDepth = req.TechnicalDepth;
            existing.WeeklyHours = req.WeeklyHours;
            existing.LearningStyle = req.LearningStyle;
            existing.ProjectGoal = req.ProjectGoal;
            existing.UpdatedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(ct);
    }

    private async Task<Guid> PersistTrailAsync(GeneratedTrailDto dto, CancellationToken ct)
    {
        var trail = new TrailEntity
        {
            Id = Guid.NewGuid(),
            Name = dto.Title,
            Description = dto.Description,
            CreatedAt = DateTime.UtcNow,
        };

        trail.Challenges = dto.Challenges.Select((c, i) => new Challenge
        {
            Id = Guid.NewGuid(),
            TrailId = trail.Id,
            Title = c.Title,
            Description = c.Description,
            Order = i + 1,
            AiSearchTerms = JsonSerializer.Serialize(c.SearchTerms),
            CreatedAt = DateTime.UtcNow,
        }).ToList();

        db.Trails.Add(trail);
        await db.SaveChangesAsync(ct);
        return trail.Id;
    }

    // ── Validation ────────────────────────────────────────────────────────────

    private static void ValidateGenerated(GeneratedTrailDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Title))
            throw new InvalidOperationException("AI returned empty trail title.");
        if (dto.Challenges is null || dto.Challenges.Count == 0)
            throw new InvalidOperationException("AI returned no challenges.");
        if (dto.Challenges.Count > 20)
            throw new InvalidOperationException("AI returned too many challenges (>20).");
    }

    // ── Prompt builders ───────────────────────────────────────────────────────

    private static string BuildSystemPrompt() =>
        """
        You are an expert software-engineering curriculum designer.
        Your task is to produce a focused, practical learning trail.

        Rules:
        - Generate exactly 5 to 10 sequential challenges, from simpler to more complex.
        - Each challenge must have a concrete, hands-on deliverable (a GitHub repo or Gist).
        - Descriptions must be written in clear, accessible Brazilian Portuguese.
        - YouTube search terms must be in English and specific (not generic).
        - The trail must align with the student's stated project goal and technical depth.
        - Do NOT include challenges that require paid tools or cloud credits.
        """;

    private static string BuildUserMessage(OnboardingProfileRequest p) =>
        $"""
        Student profile:
        - Target role / stack: {p.TargetRole}
        - Technical depth: {p.TechnicalDepth}
        - Available weekly hours: {p.WeeklyHours}
        - Preferred learning style: {p.LearningStyle}
        - Immediate project goal: {p.ProjectGoal}

        Generate a personalised learning trail for this student.
        """;

    // ── JSON Schema for Anthropic tool ────────────────────────────────────────

    private static JsonObject BuildSchema() => JsonNode.Parse("""
        {
          "type": "object",
          "properties": {
            "title":          { "type": "string", "description": "Concise trail title in PT-BR" },
            "description":    { "type": "string", "description": "2-3 sentence trail overview in PT-BR" },
            "estimatedHours": { "type": "number", "description": "Total hours to complete the trail" },
            "challenges": {
              "type": "array",
              "minItems": 5,
              "maxItems": 10,
              "items": {
                "type": "object",
                "properties": {
                  "title":       { "type": "string" },
                  "description": { "type": "string", "description": "Full markdown description in PT-BR. Include: objective, acceptance criteria, tips." },
                  "searchTerms": {
                    "type": "array",
                    "items": { "type": "string" },
                    "minItems": 1,
                    "maxItems": 3,
                    "description": "Specific English YouTube search terms for self-study"
                  }
                },
                "required": ["title", "description", "searchTerms"]
              }
            }
          },
          "required": ["title", "description", "estimatedHours", "challenges"]
        }
        """)!.AsObject();
}
