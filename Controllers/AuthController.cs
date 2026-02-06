using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Marketplace.Api.Controllers;

[ApiController]
[Route("api/auth")]
[Tags("90 - Public / Tools")]
public class AuthController : ControllerBase
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _config;

    public AuthController(IHttpClientFactory httpClientFactory, IConfiguration config)
    {
        _httpClientFactory = httpClientFactory;
        _config = config;
    }

    // POST /api/auth/login
    // NOTE: This is a proxy to Keycloak for MVP/testing.
    // In production, clients should login directly with Keycloak and send the JWT to API.
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] Models.LoginRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Username) || string.IsNullOrWhiteSpace(req.Password))
            return BadRequest("Username and Password are required.");

        var tokenUrl = _config["Keycloak:TokenUrl"];
        var clientId = _config["Keycloak:ClientId"];

        if (string.IsNullOrWhiteSpace(tokenUrl) || string.IsNullOrWhiteSpace(clientId))
            return StatusCode(500, "Keycloak TokenUrl/ClientId is missing in appsettings.json");

        var http = _httpClientFactory.CreateClient();

        var form = new Dictionary<string, string>
        {
            ["grant_type"] = "password",
            ["client_id"] = clientId,
            ["username"] = req.Username,
            ["password"] = req.Password
        };

        using var content = new FormUrlEncodedContent(form);

        using var resp = await http.PostAsync(tokenUrl, content);
        var body = await resp.Content.ReadAsStringAsync();

        if (!resp.IsSuccessStatusCode)
        {
            // Return Keycloak error payload as-is to help debugging
            return StatusCode((int)resp.StatusCode, body);
        }

        // body is JSON (access_token, expires_in, refresh_token, etc.)
        return Content(body, "application/json");
    }
}
