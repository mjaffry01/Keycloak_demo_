using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Marketplace.Api.Data;
using Marketplace.Api.Models;
using System.Linq;
using System;
using System.Security.Claims;

namespace Marketplace.Api.Controllers;

[ApiController]
[Route("api/admin/category-requests")]
[Authorize(Roles = "admin")]
[Tags("10 - Admin")]
public class AdminController : ControllerBase
{
    private readonly MarketplaceDbContext _db;

    public AdminController(MarketplaceDbContext db)
    {
        _db = db;
    }

    private string? GetAdminUsername()
        => User.FindFirstValue("preferred_username")
           ?? User.FindFirstValue(ClaimTypes.Name)
           ?? User.FindFirstValue(ClaimTypes.NameIdentifier);

    // ADMIN: GET /api/admin/category-requests/pending
    [HttpGet("pending")]
    public IActionResult GetPendingCategoryRequests()
    {
        var pending = _db.CategoryApprovalRequests
            .Where(r => r.Status == "Pending")
            .OrderBy(r => r.CreatedUtc)
            .Join(_db.Categories,
                r => r.CategoryId,
                c => c.Id,
                (r, c) => new
                {
                    requestId = r.Id,
                    type = r.Type,
                    sellerSub = r.SellerSub,
                    categoryId = r.CategoryId,
                    categoryName = c.Name,
                    status = r.Status,
                    createdUtc = r.CreatedUtc
                })
            .ToList();

        return Ok(pending);
    }

    // ADMIN: POST /api/admin/category-requests/{id}/review
    // Body: { "status": "Approved" | "Rejected", "rejectionReason": "..." }
    [HttpPost("{id:int}/review")]
    public IActionResult ReviewCategoryRequest([FromRoute] int id, [FromBody] CategoryRequestReviewDto body)
    {
        var req = _db.CategoryApprovalRequests.FirstOrDefault(r => r.Id == id);
        if (req is null)
            return NotFound(new { message = "Category request not found." });

        var status = (body?.Status ?? "").Trim();
        if (status != "Approved" && status != "Rejected")
            return BadRequest(new { message = "Status must be either 'Approved' or 'Rejected'." });

        // Idempotent-ish: if already decided, just return current
        if (req.Status == "Approved" || req.Status == "Rejected")
        {
            return Ok(new
            {
                requestId = req.Id,
                type = req.Type,
                sellerSub = req.SellerSub,
                categoryId = req.CategoryId,
                status = req.Status,
                approvedUtc = req.ApprovedUtc,
                approvedBy = GetAdminUsername()
            });
        }

        req.Status = status;
        req.ApprovedUtc = DateTime.UtcNow;

        _db.SaveChanges();

        return Ok(new
        {
            requestId = req.Id,
            type = req.Type,
            sellerSub = req.SellerSub,
            categoryId = req.CategoryId,
            status = req.Status,
            approvedUtc = req.ApprovedUtc,
            approvedBy = GetAdminUsername(),
            rejectionReason = status == "Rejected" ? (body?.RejectionReason ?? "") : null
        });
    }

    // ADMIN: GET /api/admin/category-requests/approved
    [HttpGet("approved")]
    public IActionResult GetApprovedCategoryRequests()
    {
        var approved = _db.CategoryApprovalRequests
            .Where(r => r.Status == "Approved")
            .OrderByDescending(r => r.ApprovedUtc)
            .Join(_db.Categories,
                r => r.CategoryId,
                c => c.Id,
                (r, c) => new
                {
                    requestId = r.Id,
                    sellerSub = r.SellerSub,
                    categoryId = r.CategoryId,
                    categoryName = c.Name,
                    approvedUtc = r.ApprovedUtc
                })
            .ToList();

        return Ok(approved);
    }
}
