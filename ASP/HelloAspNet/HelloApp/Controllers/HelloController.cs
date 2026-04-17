using Microsoft.AspNetCore.Mvc;

namespace HelloApp.Controllers;

[ApiController]  ///switches the controller into API mode: auto-validates requests, auto-binds request body, enforces attribute routing, and returns structured JSON errors.
[Route("[controller]")] ///sets the URL prefix for the controller by reading the class name, so you never hardcode it.

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