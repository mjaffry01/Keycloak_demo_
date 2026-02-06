using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace Marketplace.Api.Controllers;

[ApiController]
[Route("api/diag")]
[Tags("90 - Public / Tools")]
public class DiagController : ControllerBase
{
    private readonly IHttpClientFactory _http;
    private readonly KeycloakOptions _kc;

    public DiagController(IHttpClientFactory http, IOptions<KeycloakOptions> kc)
    {
        _http = http;
        _kc = kc.Value;
    }

    [HttpGet("keycloak")]
    public async Task<IActionResult> Keycloak()
    {
        var authority = (_kc.Authority ?? "").TrimEnd('/');
        if (string.IsNullOrWhiteSpace(authority))
            return Problem("Keycloak:Authority is missing in configuration.");

        var discoveryUrl = $"{authority}/.well-known/openid-configuration";

        var client = _http.CreateClient();
        var discoveryJson = await client.GetStringAsync(discoveryUrl);

        using var doc = JsonDocument.Parse(discoveryJson);
        var issuer = doc.RootElement.GetProperty("issuer").GetString();
        var jwksUri = doc.RootElement.GetProperty("jwks_uri").GetString();

        return Ok(new { authority, discoveryUrl, issuer, jwksUri });
    }
}
