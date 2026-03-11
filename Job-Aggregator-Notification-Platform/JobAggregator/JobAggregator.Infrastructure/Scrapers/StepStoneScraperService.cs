using JobAggregator.Application.Interfaces;
using JobAggregator.Domain.Models;

namespace JobAggregator.Infrastructure.Scrapers;

public class StepStoneScraperService : IJobScraperService
{
    public JobSource Source => JobSource.StepStone;

    public async Task<IEnumerable<Job>> SearchAsync(
        SearchQuery query, CancellationToken ct = default)
    {
        await Task.Delay(100, ct);

        return new List<Job>
        {
            new Job
            {
                Title   = $"[StepStone] C# Backend Engineer",
                Company = "StepStone GmbH",
                Url     = "https://stepstone.de/jobs/1",
                Source  = JobSource.StepStone,
                Tags    = new List<string> { "C#", "ASP.NET Core", "SQL" }
            }
        };
    }
}