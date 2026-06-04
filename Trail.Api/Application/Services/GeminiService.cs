using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;
using Trail.Api.Configuration;

namespace Trail.Api.Application.Services;

public sealed class GeminiService(
    IHttpClientFactory httpClientFactory,
    IOptions<GeminiOptions> options) : IAiService
{
    private readonly GeminiOptions _cfg = options.Value;

    private static readonly JsonSerializerOptions _jsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    public async Task<T> InvokeToolAsync<T>(
        string systemPrompt,
        IReadOnlyList<AiMessage> messages,
        AiTool tool,
        CancellationToken ct = default)
    {
        var body = new
        {
            system_instruction = new { parts = new[] { new { text = systemPrompt } } },
            contents = messages.Select(m => new
            {
                role = m.Role == "user" ? "user" : "model",
                parts = new[] { new { text = m.Content } }
            }),
            tools = new[]
            {
                new
                {
                    function_declarations = new[]
                    {
                        new
                        {
                            name = tool.Name,
                            description = tool.Description,
                            parameters = tool.InputSchema
                        }
                    }
                }
            },
            tool_config = new
            {
                function_calling_config = new
                {
                    mode = "ANY",
                    allowed_function_names = new[] { tool.Name }
                }
            }
        };

        var response = await PostAsync(body, ct);

        // Parse Gemini function call response
        // Path: candidates[0].content.parts[0].functionCall.args
        var candidate = response?["candidates"]?[0];
        var functionCall = candidate?["content"]?["parts"]?[0]?["functionCall"];
        var args = functionCall?["args"];

        if (args is null)
            throw new InvalidOperationException($"Gemini did not return a function call for '{tool.Name}'.");

        return JsonSerializer.Deserialize<T>(args.ToJsonString(), _jsonOpts)
               ?? throw new InvalidOperationException("Failed to deserialise tool input.");
    }

    public async Task<string> ChatAsync(
        string systemPrompt,
        IReadOnlyList<AiMessage> messages,
        int maxTokens = 1024,
        CancellationToken ct = default)
    {
        var body = new
        {
            system_instruction = new { parts = new[] { new { text = systemPrompt } } },
            contents = messages.Select(m => new
            {
                role = m.Role == "user" ? "user" : "model",
                parts = new[] { new { text = m.Content } }
            }),
            generationConfig = new
            {
                maxOutputTokens = maxTokens
            }
        };

        var response = await PostAsync(body, ct);

        // Path: candidates[0].content.parts[0].text
        var text = response?["candidates"]?[0]?["content"]?["parts"]?[0]?["text"]?.GetValue<string>();
        return text ?? string.Empty;
    }

    private async Task<JsonObject?> PostAsync(object body, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_cfg.ApiKey))
            throw new InvalidOperationException("Gemini API key is not configured.");

        var client = httpClientFactory.CreateClient("gemini");
        var url = $"v1beta/models/{_cfg.Model}:generateContent?key={_cfg.ApiKey}";
        
        var resp = await client.PostAsJsonAsync(url, body, _jsonOpts, ct);

        if (!resp.IsSuccessStatusCode)
        {
            var err = await resp.Content.ReadAsStringAsync(ct);
            throw new HttpRequestException($"Gemini API error {(int)resp.StatusCode}: {err}");
        }

        return await resp.Content.ReadFromJsonAsync<JsonObject>(_jsonOpts, ct);
    }
}
