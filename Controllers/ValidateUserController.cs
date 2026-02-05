using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Linq;

namespace Marketplace.Api.Controllers;

[ApiController]
[Route("api/validate-user")]
[Authorize] // Any authenticated user
public class ValidateUserController : ControllerBase
{
    [HttpGet]
    public IActionResult WhoAmI()
    {
        // Auth check (extra safety)
        if (User?.Identity?.IsAuthenticated != true)
            return Unauthorized("You are not authenticated.");

        // Keycloak subject (user id)
        var sub = User.FindFirstValue("sub");

        // Username resolution
        var username =
            User.FindFirstValue("preferred_username")
            ?? User.Identity?.Name
            ?? "unknown";

        // Roles mapped from realm_access.roles -> ClaimTypes.Role
        var roles = User.Claims
            .Where(c => c.Type == ClaimTypes.Role)
            .Select(c => c.Value)
            .Distinct()
            .ToList();

        return Ok(new
        {
            authenticated = true,
            sub,
            username,
            roles,
            loginAs = roles // same type, no null ambiguity
        });
    }
}
