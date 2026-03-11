using JobAggregator.Domain.Models;

namespace JobAggregator.Application.DTOs;

public record JobDto(
    Guid Id,
    string Title,
    string Company,
    string? Location,
    string? SalaryRange,
    string Url,
    string Source,
    DateTime ScrapedAt,
    List<string> Tags )
{
    //Maps from domain model --> DTO
    // Controller calls this - never exposes job direclty

    public static JobDto FromDomain(Job job) => new (job.Id,
        job.Title,
        job.Company,
        job.Location,
        job.SalaryRange,
        job.Url,
        job.Source.ToString(),
        job.ScrapedAt,
        job.Tags);
}

