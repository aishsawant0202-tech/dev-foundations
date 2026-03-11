using JobAggregator.Application.Interfaces;
using JobAggregator.Domain.Models;

namespace JobAggregator.Application.Services;

public class JobSearchService
{
    private readonly IJobScraperFactory _factory;

    public JobSearchService(IJobScraperFactory factory)
    {
        _factory = factory;
    }

    public async Task<IEnumerable<Job>> SearchAllPlatformsAsync(SearchQuery query)
    {
        if (string.IsNullOrWhiteSpace(query.Keywords))
            throw new ArgumentException("Keywords cannot be empty");

        // Get all registered scrapers
        var scrapers = _factory.GetAll(
            Enum.GetValues<JobSource>());

        // Fire ALL scrapers simultaneously
        var tasks = scrapers.Select(s => s.SearchAsync(query));
        var results = await Task.WhenAll(tasks);

        // Flatten results from all platforms into one list
        return results.SelectMany(r => r).ToList();
    }
}