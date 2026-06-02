using System.ComponentModel.DataAnnotations;

namespace Trail.Api.Configuration;

public class RefreshOptions
{
    public const string SectionName = "Refresh";

    [Range(1, 365)]
    public int ExpirationDays { get; set; } = 14;
}
