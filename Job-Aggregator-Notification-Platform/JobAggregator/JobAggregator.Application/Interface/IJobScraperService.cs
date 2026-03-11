using System;
using System.Collections.Generic;
using System.Text;
using JobAggregator.Domain.Models;

namespace JobAggregator.Application.Interfaces;

public interface IJobScrapperService
{
    Task<IEnumerable<Job>> SearchAsync(SearchQuery query, CancellationToken ct = default);

    JobSource Source { get; }
}
