using System;
using System.Collections.Generic;
using System.Text;

namespace JobAggregator.Domain.Models;

public record SearchQuery
{
    public required string Keywords { get; init; }
    public string? Location { get; init; }
    public int MaxResults { get; init; } = 50;
    public List<JobSource> Sources { get; init; } = new();
}
