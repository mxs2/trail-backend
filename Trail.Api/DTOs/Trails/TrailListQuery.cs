using System.ComponentModel.DataAnnotations;

namespace Trail.Api.DTOs.Trails;

public class TrailListQuery
{
    [Range(1, int.MaxValue)]
    public int? Page { get; init; }

    [Range(1, 200)]
    public int? PerPage { get; init; }

    [MaxLength(256)]
    public string? Search { get; init; }

    [MaxLength(128)]
    public string? Level { get; init; }
}
