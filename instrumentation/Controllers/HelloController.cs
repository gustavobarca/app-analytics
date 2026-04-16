using Microsoft.AspNetCore.Mvc;

namespace InstrumentationTest.Controllers;

[ApiController]
[Route("/")]
public class HelloController : ControllerBase
{
    [HttpGet]
    public IActionResult Get()
    {
        return Ok("Hello, World!");
    }

    [HttpPost]
    public IActionResult Post([FromBody] HelloRequest request)
    {
        return Ok($"Hello, {request.Name}!");
    }
}

public record HelloRequest(string Name);
