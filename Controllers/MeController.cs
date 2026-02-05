using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Linq;

namespace Marketplace.Api.Controllers;

[ApiController]
[Route("api/me")]
public class MeController : ControllerBase
{
    [HttpGet]
    [Authorize]
    public IActionResult Get()
    {
        var sub = User.FindFirstValue("sub");
        var username = User.FindFirstValue("preferred_username") ?? User.Identity?.Name;
        var roles = User.Claims.Where(c => c.Type == ClaimTypes.Role).Select(c => c.Value).Distinct().ToList();

        return Ok(new
        {
            sub,
            username,
            roles
        });
    }

    [HttpGet("seller-only")]
    [Authorize(Roles = "seller")]
    public IActionResult SellerOnly() => Ok("You are seller ✅");
}
