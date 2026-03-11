using JobAggregator.Application.Interfaces;
using JobAggregator.Domain.Models;

namespace JobAggregator.Infrastructure.Factories;

public class JobScraperFactory : IJobScraperFactory
{
    private readonly IEnumerable<IJobScraperService> _scrapers;

    public JobScraperFactory(IEnumerable<IJobScraperService> scrapers)
        => _scrapers = scrapers;

    public IJobScraperService GetScraper(JobSource source)
        => _scrapers.FirstOrDefault(s => s.Source == source)
           ?? throw new NotSupportedException($"No scraper for {source}");

    public IEnumerable<IJobScraperService> GetAll(IEnumerable<JobSource> sources)
        => _scrapers.Where(s => sources.Contains(s.Source));
}