using Microsoft.EntityFrameworkCore;
using Trail.Api.DTOs.Ai;
using Trail.Api.Infrastructure.Data;

namespace Trail.Api.Application.Services;

/// <summary>
/// Implements the Socratic AI Tutor for challenge-level assistance.
///
/// Invariants enforced by system prompt:
///  - NEVER reveals or writes code solutions.
///  - NEVER gives direct answers — only guiding questions and conceptual hints.
///  - Uses the student's onboarding profile to calibrate depth of explanation.
///  - Responds only about the active challenge.
/// </summary>
public class SocraticAssistantService(AppDbContext db, IAiService ai)
{
    public async Task<SocraticChatResponse> ChatAsync(
        Guid studentId,
        SocraticChatRequest request,
        CancellationToken ct = default)
    {
        // Load challenge
        var challenge = await db.Challenges
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == request.ChallengeId, ct)
            ?? throw new KeyNotFoundException($"Challenge {request.ChallengeId} not found.");

        // Load onboarding profile for personalisation (optional — degrade gracefully)
        var profile = await db.OnboardingProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.UserId == studentId, ct);

        var systemPrompt = BuildSystemPrompt(challenge.Title, challenge.Description, profile);

        // Build message history
        var messages = request.History
            .Select(h => new AiMessage(h.Role, h.Content))
            .Append(new AiMessage("user", request.NewMessage))
            .ToList();

        var reply = await ai.ChatAsync(systemPrompt, messages, maxTokens: 512, ct);
        return new SocraticChatResponse(reply);
    }

    private static string BuildSystemPrompt(
        string challengeTitle,
        string challengeDescription,
        Domain.Entities.StudentOnboardingProfile? profile)
    {
        var depth = profile?.TechnicalDepth ?? "intermediate";
        var style = profile?.LearningStyle ?? "hands-on";

        return $"""
            You are a Socratic programming tutor embedded in a challenge platform.

            ACTIVE CHALLENGE
            Title: {challengeTitle}
            Requirements:
            {challengeDescription}

            STUDENT PROFILE
            Technical depth: {depth}
            Learning style: {style}

            STRICT RULES — never break these:
            1. NEVER write or reveal code solutions, even partial ones.
            2. NEVER give direct answers. Always respond with guiding questions or conceptual explanations.
            3. If the student asks for code, respond: "Posso te ajudar a pensar no raciocínio, mas escrever o código é a sua missão."
            4. Calibrate explanation depth to the student's level: {depth}.
            5. Keep responses under 150 words.
            6. Respond in Brazilian Portuguese.
            7. Stay strictly on topic — only discuss this challenge.
            """;
    }
}
