using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Trail.Api.Domain.Enums;
using Trail.Api.DTOs.Ai;
using Trail.Api.Infrastructure.Data;

namespace Trail.Api.Application.Services;

/// <summary>
/// Fetches submitted repository content via the GitHub API and invokes
/// the AI to produce a structured draft review for the mentor.
///
/// Pipeline:
///  1. Load submission + challenge from DB.
///  2. Parse the GitHub URL to extract owner/repo or gist ID.
///  3. Fetch README and top-level file tree (or Gist files).
///  4. Send challenge description + code context to Anthropic.
///  5. Return a structured draft the mentor can edit before submitting.
/// </summary>
public class GitHubReviewService(
    AppDbContext db,
    IAiService ai,
    IHttpClientFactory httpClientFactory)
{
    private static readonly AiTool ReviewTool = new(
        Name: "generate_code_review_draft",
        Description: "Analyse student code against challenge criteria and produce a structured review draft.",
        InputSchema: BuildReviewSchema());

    public async Task<AiReviewDraft> GenerateReviewDraftAsync(
        Guid submissionId,
        CancellationToken ct = default)
    {
        // 1. Load submission + challenge ───────────────────────────────────────
        var submission = await db.Submissions
            .Include(s => s.Challenge)
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == submissionId, ct)
            ?? throw new KeyNotFoundException($"Submission {submissionId} not found.");

        if (submission.Status != SubmissionStatus.Submitted)
            throw new InvalidOperationException("Only pending submissions can be AI-reviewed.");

        // 2. Fetch code from GitHub ─────────────────────────────────────────────
        var codeContext = await FetchCodeContextAsync(submission.GitHubUrl, ct);

        // 3. Build & invoke AI ─────────────────────────────────────────────────
        var system = BuildSystemPrompt(submission.Challenge.Title, submission.Challenge.Description);
        var userMsg = BuildUserMessage(codeContext);

        var draft = await ai.InvokeToolAsync<AiReviewDraft>(
            system,
            [new AiMessage("user", userMsg)],
            ReviewTool,
            ct);

        return draft;
    }

    // ── GitHub fetching ───────────────────────────────────────────────────────

    private async Task<string> FetchCodeContextAsync(string gitHubUrl, CancellationToken ct)
    {
        var client = httpClientFactory.CreateClient("github");

        // Gist path
        if (gitHubUrl.Contains("gist.github.com"))
        {
            var gistId = gitHubUrl.Split('/').Last().Split('?').First();
            var gist = await client.GetFromJsonAsync<JsonObject>($"gists/{gistId}", ct);
            return ExtractGistContent(gist);
        }

        // Repo path: github.com/{owner}/{repo}
        var (owner, repo) = ParseRepoUrl(gitHubUrl);

        // Fetch README
        string readme;
        try
        {
            var readmeResp = await client.GetFromJsonAsync<JsonObject>(
                $"repos/{owner}/{repo}/readme", ct);
            var encoded = readmeResp?["content"]?.GetValue<string>() ?? "";
            readme = System.Text.Encoding.UTF8.GetString(
                Convert.FromBase64String(encoded.Replace("\n", "")));
        }
        catch
        {
            readme = "(README not found)";
        }

        // Fetch top-level file tree (first 20 files)
        string tree;
        try
        {
            var treeResp = await client.GetFromJsonAsync<JsonObject>(
                $"repos/{owner}/{repo}/git/trees/HEAD?recursive=0", ct);
            var items = treeResp?["tree"]?.AsArray()
                .Take(20)
                .Select(n => n?["path"]?.GetValue<string>())
                .Where(p => p is not null);
            tree = string.Join("\n", items ?? []);
        }
        catch
        {
            tree = "(file tree unavailable)";
        }

        return $"Repository: {owner}/{repo}\n\nFile tree:\n{tree}\n\nREADME:\n{readme}";
    }

    private static string ExtractGistContent(JsonObject? gist)
    {
        if (gist?["files"] is not JsonObject files) return "(empty gist)";
        var sb = new System.Text.StringBuilder();
        foreach (var (name, node) in files)
        {
            sb.AppendLine($"--- {name} ---");
            sb.AppendLine(node?["content"]?.GetValue<string>() ?? "");
        }
        return sb.ToString();
    }

    private static (string owner, string repo) ParseRepoUrl(string url)
    {
        // Strip trailing slashes, query strings
        var clean = url.TrimEnd('/').Split('?')[0];
        var parts = clean.Replace("https://github.com/", "").Split('/');
        return parts.Length >= 2
            ? (parts[0], parts[1])
            : throw new ArgumentException($"Cannot parse GitHub repo URL: {url}");
    }

    // ── Prompts ───────────────────────────────────────────────────────────────

    private static string BuildSystemPrompt(string challengeTitle, string challengeDescription) => $"""
        You are a senior software engineer conducting a code review for a student submission.
        Your role is to HELP the mentor, not to replace them. Produce a structured review DRAFT.

        CHALLENGE BEING ASSESSED
        Title: {challengeTitle}
        Requirements:
        {challengeDescription}

        REVIEW GUIDELINES:
        - Be constructive and specific. Point to concrete issues, not vague criticism.
        - Identify at least one strength, even for weak submissions.
        - Edge cases should be specific to the challenge requirements.
        - The suggested_comment must be < 400 characters and actionable.
        - suggested_decision: "Approved" if the core requirements are met; "NeedsRevision" otherwise.
        - Respond in Brazilian Portuguese.
        """;

    private static string BuildUserMessage(string codeContext) =>
        $"Review the following student submission:\n\n{codeContext}";

    // ── Tool schema ───────────────────────────────────────────────────────────

    private static JsonObject BuildReviewSchema() => JsonNode.Parse("""
        {
          "type": "object",
          "properties": {
            "qualityAnalysis": {
              "type": "string",
              "description": "2-4 sentence analysis of code quality, structure, and correctness"
            },
            "edgeCases": {
              "type": "array",
              "items": { "type": "string" },
              "description": "Specific unhandled edge cases relevant to the challenge"
            },
            "suggestedComment": {
              "type": "string",
              "description": "Ready-to-use mentor comment, max 400 chars, in PT-BR"
            },
            "suggestedDecision": {
              "type": "string",
              "enum": ["Approved", "NeedsRevision"]
            }
          },
          "required": ["qualityAnalysis", "edgeCases", "suggestedComment", "suggestedDecision"]
        }
        """)!.AsObject();
}
