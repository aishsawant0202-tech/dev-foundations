using JobAggregator.Application.DTOs;
using JobAggregator.Application.Services;
using JobAggregator.Domain.Models;
using Microsoft.AspNetCore.Mvc;

namespace JobAggregator.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class JobsController : ControllerBase
{
    private readonly JobSearchService _searchService;
    private readonly ILogger<JobsController> _logger;

    public JobsController(JobSearchService searchService, ILogger<JobsController> logger)
    {
        _searchService = searchService;
        _logger = logger;

    }

    // GET /api/jobs/search?keywords=.net+developer&location=Berlin
    //[HttpGet("search")]
    //[ProducesResponseType(typeof(IEnumerable<JobDto>), 200)]
    //[ProducesResponseType(400)]

}
