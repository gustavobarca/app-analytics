using Microsoft.AspNetCore.Mvc;

namespace InstrumentationTest.Controllers;

[ApiController]
[Route("external")]
public class ExternalController(IHttpClientFactory httpClientFactory) : ControllerBase
{
    private HttpClient Http => httpClientFactory.CreateClient("external");

    [HttpGet("posts")]
    public async Task<IActionResult> GetPosts()
    {
        var response = await Http.GetAsync("posts");
        var body = await response.Content.ReadAsStringAsync();
        return Content(body, "application/json");
    }

    [HttpGet("posts/{id:int}")]
    public async Task<IActionResult> GetPost(int id)
    {
        var response = await Http.GetAsync($"posts/{id}");
        var body = await response.Content.ReadAsStringAsync();
        return Content(body, "application/json");
    }

    [HttpPost("posts")]
    public async Task<IActionResult> CreatePost([FromBody] object payload)
    {
        var response = await Http.PostAsJsonAsync("posts", payload);
        var body = await response.Content.ReadAsStringAsync();
        return Content(body, "application/json");
    }

    // Triggers a 404 from the external service — exercises error logging
    [HttpGet("not-found")]
    public async Task<IActionResult> NotFound_()
    {
        var response = await Http.GetAsync("posts/99999999");
        var body = await response.Content.ReadAsStringAsync();
        return StatusCode((int)response.StatusCode, body);
    }
}
