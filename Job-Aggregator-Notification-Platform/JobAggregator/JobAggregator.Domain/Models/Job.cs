using System;
using System.Collections.Generic;
using System.Text;

namespace JobAggregator.Domain.Models;

public record Job
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string Title { get; init; }
    public required string Company { get; init; }
    public string? Location { get; init; }
    public string? SalaryRange { get; init; }
    public required string Url { get; init; }
    public required JobSource Source { get; init; }
    public DateTime ScrapedAt { get; init; } = DateTime.UtcNow;
    public List<string> Tags { get; init; } = new();
    public string? Description { get; init; }


}
