using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Marketplace.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Marketplace.Api.Controllers;

[ApiController]
[Route("api/buyer/products")]
[Authorize(Roles = "buyer")]
[Tags("30 - Buyer")]
public class BuyerProductsController : ControllerBase
{
    private readonly MarketplaceDbContext _db;

    public BuyerProductsController(MarketplaceDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// List products available for buyers to purchase.
    /// Only returns active products (and optionally filter by category).
    /// </summary>
    /// <param name="categoryId">Optional category filter</param>
    /// <param name="page">1-based page number</param>
    /// <param name="pageSize">Items per page (max 100)</param>
    [HttpGet]
    public async Task<IActionResult> GetAvailableProducts(
        [FromQuery] int? categoryId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 20;
        if (pageSize > 100) pageSize = 100;

        var q = _db.Products
            .AsNoTracking()
            .Where(p => p.IsActive);

        if (categoryId.HasValue)
            q = q.Where(p => p.CategoryId == categoryId.Value);

        var total = await q.CountAsync();

        var items = await q
            .OrderByDescending(p => p.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new
            {
                id = p.Id,
                name = p.Name,
                price = p.Price,
                categoryId = p.CategoryId
            })
            .ToListAsync();

        return Ok(new
        {
            page,
            pageSize,
            total,
            items
        });
    }

    /// <summary>
    /// Get a single product details for a buyer (only if active).
    /// </summary>
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetProductById([FromRoute] int id)
    {
        var p = await _db.Products
            .AsNoTracking()
            .Where(x => x.IsActive && x.Id == id)
            .Select(x => new
            {
                id = x.Id,
                name = x.Name,
                price = x.Price,
                categoryId = x.CategoryId
            })
            .FirstOrDefaultAsync();

        if (p == null)
            return NotFound(new { message = "Product not found (or not available)." });

        return Ok(p);
    }
}
