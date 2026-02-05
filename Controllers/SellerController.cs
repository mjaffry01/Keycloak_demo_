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
