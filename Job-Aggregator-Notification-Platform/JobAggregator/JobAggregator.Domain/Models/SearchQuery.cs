using System;
using System.Collections.Generic;
using System.Text;

namespace JobAggregator.Domain.Models;

public record SearchQuery
{
    public required string Keywords { get; set; }
    public string? Location { get; set; }
    public int MaxResults { get; set; }
    public List<JobSource> Sources { get; set; } = new();
}
