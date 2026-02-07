using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Linq;

namespace Marketplace.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/whoami")]
[Tags("00 - Authenticated (Any Credential)")]
public class WhoAmIController : ControllerBase
{
    [HttpGet]
    [Authorize]
    public IActionResult Get()
    {
        var username =
            User.FindFirst("preferred_username")?.Value
            ?? User.Identity?.Name
            ?? "unknown";

        var roles = User.Claims
            .Where(c => c.Type == ClaimTypes.Role)
            .Select(c => c.Value)
            .Distinct()
            .ToArray();

        return Ok(new { username, roles });
    }
}
