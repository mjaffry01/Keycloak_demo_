using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Marketplace.Api.Data;
using Marketplace.Api.Data.Entities;
using Marketplace.Api.Models;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;

namespace Marketplace.Api.Controllers;

[ApiController]
[ApiExplorerSettings(IgnoreApi = true)]
[Route("api/seller/product-proposals")]
[Authorize(Roles = "seller")]
[Tags("20 - Seller")]
public class SellerProductProposalsController : ControllerBase
{
    private readonly MarketplaceDbContext _db;

    public SellerProductProposalsController(MarketplaceDbContext db)
    {
        _db = db;
    }

    private string? GetSub()
    {
        return User.FindFirstValue("sub")
            ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
    }

    public class CreateProductProposalRequest
    {
        public int CategoryId { get; set; }
        public string Name { get; set; } = default!;
        public decimal Price { get; set; }
        public int InitialStockQty { get; set; } = 0;
    }

    /// <summary>
    /// Seller proposes a product listing. Admin must approve before the product is created.
    /// </summary>
    [HttpPost]
    public IActionResult Propose([FromBody] CreateProductProposalRequest req)
    {
        if (req.CategoryId <= 0)
            return BadRequest("CategoryId must be valid.");

        var name = (req.Name ?? "").Trim();
        if (string.IsNullOrWhiteSpace(name))
            return BadRequest("Name is required.");

        if (req.Price <= 0)
            return BadRequest("Price must be > 0.");

        if (req.InitialStockQty < 0)
            return BadRequest("InitialStockQty must be >= 0.");

        var sellerSub = GetSub();
        if (string.IsNullOrWhiteSpace(sellerSub))
            return Unauthorized("Missing sub claim.");

        var categoryExists = _db.Categories.AsNoTracking().Any(c => c.Id == req.CategoryId);
        if (!categoryExists)
            return BadRequest("Unknown CategoryId.");

        // prevent spam duplicates: same seller + same category + same name pending
        var existing = _db.ProductProposals
            .FirstOrDefault(p =>
                p.SellerSub == sellerSub &&
                p.CategoryId == req.CategoryId &&
                p.Name == name &&
                p.Status == "Pending");

        if (existing is not null)
            return Ok(existing);

        var proposal = new ProductProposal
        {
            SellerSub = sellerSub,
            CategoryId = req.CategoryId,
            Name = name,
            Price = req.Price,
            InitialStockQty = req.InitialStockQty,
            Status = "Pending",
            CreatedUtc = DateTime.UtcNow
        };

        _db.ProductProposals.Add(proposal);
        _db.SaveChanges();

        return Ok(proposal);
    }

    /// <summary>
    /// Seller can view their own product proposals.
    /// </summary>
    [HttpGet]
    public IActionResult GetMine()
    {
        var sellerSub = GetSub();
        if (string.IsNullOrWhiteSpace(sellerSub))
            return Unauthorized("Missing sub claim.");

        var items = _db.ProductProposals
            .AsNoTracking()
            .Where(p => p.SellerSub == sellerSub)
            .OrderByDescending(p => p.CreatedUtc)
            .ToList();

        return Ok(items);
    }
}
