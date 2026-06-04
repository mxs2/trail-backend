using System.Text.Json.Nodes;

namespace Trail.Api.Application.Services;

public record AiMessage(string Role, string Content);

public record AiTool(
    string Name,
    string Description,
    JsonObject InputSchema);

/// <summary>
/// Generic AI service interface for chat and structured tool invocation.
/// </summary>
public interface IAiService
{
    /// <summary>
    /// Forces the model to call <paramref name="tool"/> and returns its
    /// input payload deserialised as <typeparamref name="T"/>.
    /// </summary>
    Task<T> InvokeToolAsync<T>(
        string systemPrompt,
        IReadOnlyList<AiMessage> messages,
        AiTool tool,
        CancellationToken ct = default);

    /// <summary>
    /// Standard (non-streaming) chat completion. Returns the assistant's reply.
    /// </summary>
    Task<string> ChatAsync(
        string systemPrompt,
        IReadOnlyList<AiMessage> messages,
        int maxTokens = 1024,
        CancellationToken ct = default);
}
