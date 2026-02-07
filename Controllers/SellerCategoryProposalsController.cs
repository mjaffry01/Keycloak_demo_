using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Marketplace.Api.Data;
using Marketplace.Api.Models;
using System.Security.Claims;

namespace Marketplace.Api.Controllers;

[ApiController]
[ApiExplorerSettings(IgnoreApi = true)]
[Route("api/seller/category-proposals")]
[Authorize(Roles = "seller")]
[Tags("20 - Seller")]
public class SellerCategoryProposalsController : ControllerBase
{
    private readonly MarketplaceDbContext _db;

    public SellerCategoryProposalsController(MarketplaceDbContext db)
    {
        _db = db;
    }

    public class CreateCategoryProposalRequest
    {
        public string Name { get; set; } = default!;
    }

    [HttpPost]
    public IActionResult Propose([FromBody] CreateCategoryProposalRequest req)
    {
        var name = (req.Name ?? "").Trim();
        if (string.IsNullOrWhiteSpace(name))
            return BadRequest("Name is required.");

        var sellerSub = User.FindFirstValue("sub");
        if (string.IsNullOrWhiteSpace(sellerSub))
            return Unauthorized("Missing sub.");

        // avoid duplicates (same seller + same name pending)
        var existing = _db.CategoryProposals.FirstOrDefault(p =>
            p.SellerSub == sellerSub && p.Name == name && p.Status == "Pending");

        if (existing != null)
            return Ok(existing);

        var proposal = new CategoryProposal
        {
            SellerSub = sellerSub,
            Name = name,
            Status = "Pending",
            CreatedUtc = DateTime.UtcNow
        };

        _db.CategoryProposals.Add(proposal);
        _db.SaveChanges();

        return Ok(proposal);
    }
}
