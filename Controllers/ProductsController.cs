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
[Tags("20 - Seller")]
[ApiExplorerSettings(IgnoreApi = true)]
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

    // ----------------------------------------------------
    // PUBLIC: GET /api/products
    // Lists products from approved seller-category combos
    // Includes stockQty
    // ----------------------------------------------------
    [HttpGet]
    [AllowAnonymous]
    public IActionResult GetProducts()
    {
        var products = _db.Products
            .AsNoTracking()
            .Where(p => _db.CategoryApprovalRequests.Any(r =>
                r.Status == "Approved" &&
                r.SellerSub == p.SellerSub &&
                r.CategoryId == p.CategoryId))
            .OrderByDescending(p => p.Id)
            .Select(p => new ProductDto(
                p.Id,
                p.CategoryId,
                p.Name,
                p.Price,
                p.StockQty
            ))
            .ToList();

        return Ok(products);
    }

    // ----------------------------------------------------
    // SELLER: POST /api/products
    // Create product only if seller approved for category
    // Allows setting initial stock
    // ----------------------------------------------------
    [HttpPost]
    [Authorize(Roles = "seller")]
    public IActionResult CreateProduct([FromBody] CreateProductRequest req)
    {
        if (req.CategoryId <= 0)
            return BadRequest("CategoryId must be valid.");

        if (string.IsNullOrWhiteSpace(req.Name))
            return BadRequest("Name is required.");

        if (req.Price <= 0)
            return BadRequest("Price must be > 0.");

        if (req.StockQty < 0)
            return BadRequest("StockQty must be >= 0.");

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
            StockQty = req.StockQty,
            CreatedUtc = DateTime.UtcNow
        };

        _db.Products.Add(product);
        _db.SaveChanges();

        return Created(
            $"/api/products/{product.Id}",
            new ProductDto(
                product.Id,
                product.CategoryId,
                product.Name,
                product.Price,
                product.StockQty
            )
        );
    }
}
