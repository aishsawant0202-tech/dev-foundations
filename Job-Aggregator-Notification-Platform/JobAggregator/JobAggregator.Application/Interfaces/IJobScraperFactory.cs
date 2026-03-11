using JobAggregator.Domain.Models;

namespace JobAggregator.Application.Interfaces;

public interface IJobScraperFactory
{
    IJobScraperService GetScraper(JobSource source);
    IEnumerable<IJobScraperService> GetAll(IEnumerable<JobSource> sources);
}