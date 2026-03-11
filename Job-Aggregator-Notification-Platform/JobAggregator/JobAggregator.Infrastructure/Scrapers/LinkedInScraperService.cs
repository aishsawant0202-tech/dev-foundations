using JobAggregator.Application.Interfaces;
using JobAggregator.Domain.Models;

namespace JobAggregator.Infrastructure.Scrapers;

public class LinkedInScraperService : IJobScraperService
{
    public JobSource Source => JobSource.LinkedIn;

    public async Task<IEnumerable<Job>> SearchAsync(
        SearchQuery query, CancellationToken ct = default)
    {
        // Real HTTP scraping comes later — return fake data for now
        await Task.Delay(100, ct);

        return new List<Job>
        {
            new Job
            {
                Title   = $"[LinkedIn] Senior .NET Developer",
                Company = "LinkedIn Corp",
                Url     = "https://linkedin.com/jobs/1",
                Source  = JobSource.LinkedIn,
                Tags    = new List<string> { "C#", ".NET", "Azure" }
            }
        };
    }
}