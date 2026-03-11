using System;
using System.Collections.Generic;
using System.Text;
using JobAggregator.Domain.Models;

namespace JobAggregator.Application.Interfaces;

public interface IJobRepository
{
    Task SaveJobAsync(IEnumerable<Job> jobs);
    Task<IEnumerable<Job>> GetCachedJobsAsync(string keywords);
    Task<bool> JobExistsAsync(string url);
}

