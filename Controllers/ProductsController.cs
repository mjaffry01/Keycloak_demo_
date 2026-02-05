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
[Route("api/products")]
public class ProductsController : ControllerBase
{
    private readonly MarketplaceDbContext _db;

    public ProductsController(MarketplaceDbContext db)
    {
        _db = db;
    }

    private string? GetSub()
    {
        return User.FindFirstValue("sub")
            ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
    }

    // PUBLIC: GET /api/products
    // List products only from approved seller-category combos
    [HttpGet]
    [AllowAnonymous]
    public IActionResult GetProducts()
    {
        var approvedKeys = _db.CategoryApprovalRequests
            .AsNoTracking()
            .Where(r => r.Status == "Approved")
            .Select(r => new { r.SellerSub, r.CategoryId })
            .ToList()
            .Select(x => $"{x.SellerSub}:{x.CategoryId}")
            .ToHashSet();

        var products = _db.Products
            .AsNoTracking()
            .Where(p => approvedKeys.Contains($"{p.SellerSub}:{p.CategoryId}"))
            .Select(p => new ProductDto(p.Id, p.CategoryId, p.Name, p.Price))
            .OrderByDescending(p => p.Id)
            .ToList();

        return Ok(products);
    }

    // SELLER: POST /api/products
    // Create product only if seller approved for that category
    [HttpPost]
    [Authorize(Roles = "seller")]
    public IActionResult CreateProduct([FromBody] CreateProductRequest req)
    {
        if (req.CategoryId <= 0) return BadRequest("CategoryId must be valid.");
        if (string.IsNullOrWhiteSpace(req.Name)) return BadRequest("Name is required.");
        if (req.Price <= 0) return BadRequest("Price must be > 0.");

        var sellerSub = GetSub();
        if (string.IsNullOrWhiteSpace(sellerSub))
            return Unauthorized("Missing sub claim.");

        var isApproved = _db.CategoryApprovalRequests.Any(r =>
            r.SellerSub == sellerSub &&
            r.CategoryId == req.CategoryId &&
            r.Status == "Approved");

        if (!isApproved)
            return Forbid("Seller is not approved for this category.");

        var product = new ProductEntity
        {
            SellerSub = sellerSub,
            CategoryId = req.CategoryId,
            Name = req.Name.Trim(),
            Price = req.Price,
            CreatedUtc = DateTime.UtcNow
        };

        _db.Products.Add(product);
        _db.SaveChanges();

        return Created($"/api/products/{product.Id}", new ProductDto(product.Id, product.CategoryId, product.Name, product.Price));
    }
}
