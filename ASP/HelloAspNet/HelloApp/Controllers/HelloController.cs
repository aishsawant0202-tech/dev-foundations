using Microsoft.AspNetCore.Mvc;

namespace HelloApp.Controllers;

[ApiController]
[Route("[controller]")]
public class HelloController : ControllerBase
{
    private readonly IConfiguration _config;

    public HelloController(IConfiguration config)
    {
        _config = config;
    }

    [HttpGet]
    public IActionResult Get()
    {
        var env = _config["AppSettings:Environment"];

        return Ok(new
        {
            Name = "Aishwarya Sawant",
            ServerTime = DateTime.UtcNow,
            ActiveEnvironment = env
        });
    }
}