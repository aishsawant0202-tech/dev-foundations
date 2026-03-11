using System.ComponentModel.DataAnnotations;

namespace JobAggregator.Application.DT0s;

public class JobSearchRequest
{
    [Required]
    [MinLength(2, ErrorMessage = "Keywords must be at least 2 characters")]

    public string KeyWords { get; set; } = string.Empty;
    public string? Location { get; set; }
    public int MaxResults { get; set; } = 50;
}
