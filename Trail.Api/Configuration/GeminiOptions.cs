using System.ComponentModel.DataAnnotations;

namespace Trail.Api.Configuration;

public class GeminiOptions
{
    public const string SectionName = "Gemini";

    [Required]
    public string ApiKey { get; init; } = string.Empty;

    public string Model { get; init; } = "gemini-2.0-flash";

    public int MaxTokens { get; init; } = 8192;
}
