using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Marketplace.Api.Data;
using Marketplace.Api.Models;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;

namespace Marketplace.Api.Controllers;

[ApiController]
[Route("api/seller")]
[Authorize(Roles = "seller")]
[Tags("20 - Seller")]
public class SellerController : ControllerBase
{
    private readonly MarketplaceDbContext _db;

    public SellerController(MarketplaceDbContext db)
    {
        _db = db;
    }

    private string? GetSub()
    {
        return User.FindFirstValue("sub")
            ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
    }

    // SELLER: POST /api/seller/category-requests
    [HttpPost("category-requests")]
    public IActionResult RequestCategoryApproval([FromBody] CategoryRequestCreate req)
    {
        var sellerSub = GetSub();
        if (string.IsNullOrWhiteSpace(sellerSub))
            return Unauthorized("Missing sub claim.");

        var categoryExists = _db.Categories.AsNoTracking().Any(c => c.Id == req.CategoryId);
        if (!categoryExists)
            return BadRequest("Unknown CategoryId.");

        // Return existing request if already created
        var existing = _db.CategoryApprovalRequests
            .FirstOrDefault(r => r.SellerSub == sellerSub && r.CategoryId == req.CategoryId);

        if (existing is not null)
        {
            return Ok(new
            {
                existing.Id,
                existing.Type,
                existing.SellerSub,
                existing.CategoryId,
                existing.Status,
                existing.CreatedUtc,
                existing.ApprovedUtc
            });
        }

        var request = new CategoryApprovalRequest
        {
            Type = "CategoryApproval",
            SellerSub = sellerSub,
            CategoryId = req.CategoryId,
            Status = "Pending",
            CreatedUtc = DateTime.UtcNow,
            ApprovedUtc = null
        };

        _db.CategoryApprovalRequests.Add(request);
        _db.SaveChanges();

        return Ok(new
        {
            request.Id,
            request.Type,
            request.SellerSub,
            request.CategoryId,
            request.Status,
            request.CreatedUtc
        });
    }

    // SELLER: GET /api/seller/category-requests
    [HttpGet("category-requests")]
    public IActionResult GetMyCategoryRequests()
    {
        var sellerSub = GetSub();
        if (string.IsNullOrWhiteSpace(sellerSub))
            return Unauthorized("Missing sub claim.");

        var items = _db.CategoryApprovalRequests
            .AsNoTracking()
            .Where(r => r.SellerSub == sellerSub)
            .OrderByDescending(r => r.CreatedUtc)
            .Select(r => new
            {
                r.Id,
                r.Type,
                r.CategoryId,
                r.Status,
                r.CreatedUtc,
                r.ApprovedUtc
            })
            .ToList();

        return Ok(items);
    }

    // SELLER: GET /api/seller/approved-categories
    // Shows categories this seller is approved to sell in
    [HttpGet("approved-categories")]
    public IActionResult GetApprovedCategories()
    {
        var sellerSub = GetSub();
        if (string.IsNullOrWhiteSpace(sellerSub))
            return Unauthorized("Missing sub claim.");

        var approved = _db.CategoryApprovalRequests
            .AsNoTracking()
            .Where(r => r.SellerSub == sellerSub && r.Status == "Approved")
            .Join(_db.Categories.AsNoTracking(),
                r => r.CategoryId,
                c => c.Id,
                (r, c) => new
                {
                    requestId = r.Id,
                    categoryId = r.CategoryId,
                    categoryName = c.Name,
                    approvedUtc = r.ApprovedUtc
                })
            .OrderBy(x => x.categoryId)
            .ToList();

        return Ok(approved);
    }

    // SELLER: GET /api/seller/products
    // Lists only this seller's products + a small dashboard summary
    [HttpGet("products")]
    public IActionResult GetMyProducts()
    {
        var sellerSub = GetSub();
        if (string.IsNullOrWhiteSpace(sellerSub))
            return Unauthorized("Missing sub claim.");

        var items = _db.Products
            .AsNoTracking()
            .Where(p => p.SellerSub == sellerSub)
            .OrderByDescending(p => p.Id)
            .Select(p => new
            {
                id = p.Id,
                categoryId = p.CategoryId,
                name = p.Name,
                price = p.Price,
                stockQty = p.StockQty,
                isActive = p.IsActive,
                createdUtc = p.CreatedUtc
            })
            .ToList();

        var count = items.Count;
        var totalValue = items.Sum(p => p.price * p.stockQty);

        return Ok(new
        {
            count,
            totalValue,
            items
        });
    }

    // SELLER: POST /api/seller/products
    // Create product only if seller is approved for the category
    [HttpPost("products")]
    public IActionResult CreateMyProduct([FromBody] CreateProductRequest req)
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

        var product = new Marketplace.Api.Data.Entities.ProductEntity
        {
            SellerSub = sellerSub,
            CategoryId = req.CategoryId,
            Name = req.Name.Trim(),
            Price = req.Price,
            StockQty = req.StockQty,
            CreatedUtc = DateTime.UtcNow,
            IsActive = true
        };

        _db.Products.Add(product);
        _db.SaveChanges();

        return Created($"/api/seller/products/{product.Id}", new ProductDto(
            product.Id,
            product.CategoryId,
            product.Name,
            product.Price,
            product.StockQty
        ));
    }

    // SELLER: PUT /api/seller/products/{id}
    // Update price + stock for an existing product (only if it belongs to this seller)
    [HttpPut("products/{id:int}")]
    public IActionResult UpdateMyProduct([FromRoute] int id, [FromBody] UpdateProductRequest req)
    {
        if (req.Price <= 0)
            return BadRequest("Price must be > 0.");

        if (req.StockQty < 0)
            return BadRequest("StockQty must be >= 0.");

        var sellerSub = GetSub();
        if (string.IsNullOrWhiteSpace(sellerSub))
            return Unauthorized("Missing sub claim.");

        var product = _db.Products.FirstOrDefault(p => p.Id == id && p.SellerSub == sellerSub);
        if (product is null)
            return NotFound(new { message = "Product not found for this seller." });

        product.Price = req.Price;
        product.StockQty = req.StockQty;
        _db.SaveChanges();

        return Ok(new ProductDto(
            product.Id,
            product.CategoryId,
            product.Name,
            product.Price,
            product.StockQty
        ));
    }

    // SELLER: GET /api/seller/feedback (only for own products)
    [HttpGet("feedback")]
    public IActionResult GetMyProductFeedback()
    {
        var sellerSub = GetSub();
        if (string.IsNullOrWhiteSpace(sellerSub))
            return Unauthorized("Missing sub claim.");

        var myProductIds = _db.Products
            .AsNoTracking()
            .Where(p => p.SellerSub == sellerSub)
            .Select(p => p.Id)
            .ToList();

        var feedback = _db.Feedbacks
            .AsNoTracking()
            .Where(f => myProductIds.Contains(f.ProductId))
            .OrderByDescending(f => f.CreatedUtc)
            .Select(f => new FeedbackDto(f.Id, f.ProductId, f.Rating, f.Comment, f.CreatedUtc))
            .ToList();

        return Ok(feedback);
    }
}
