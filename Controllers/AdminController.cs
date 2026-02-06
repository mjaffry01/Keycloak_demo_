using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Marketplace.Api.Data;
using Marketplace.Api.Models;
using System.Linq;

namespace Marketplace.Api.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize(Roles = "admin")]
[Tags("10 - Admin")]
public class AdminController : ControllerBase
{
    private readonly MarketplaceDbContext _db;

    public AdminController(MarketplaceDbContext db)
    {
        _db = db;
    }

    // ADMIN: GET /api/admin/pending-category-requests
    [HttpGet("pending-category-requests")]
    public IActionResult GetPendingCategoryRequests()
    {
        var pending = _db.CategoryApprovalRequests
            .Where(r => r.Status == "Pending")
            .OrderBy(r => r.CreatedUtc)
            .Select(r => new
            {
                r.Id,
                r.Type,
                r.SellerSub,
                r.CategoryId,
                r.Status,
                r.CreatedUtc
            })
            .ToList();

        return Ok(pending);
    }

    // ADMIN: POST /api/admin/approve-category-request
    [HttpPost("approve-category-request")]
    public IActionResult ApproveCategoryRequest([FromBody] ApproveCategoryRequestDto dto)
    {
        var req = _db.CategoryApprovalRequests.FirstOrDefault(r => r.Id == dto.RequestId);
        if (req is null)
            return NotFound("Approval request not found.");

        if (req.Status == "Approved")
            return Ok(new { req.Id, req.Type, req.SellerSub, req.CategoryId, req.Status, req.ApprovedUtc });

        req.Status = "Approved";
        req.ApprovedUtc = DateTime.UtcNow;

        _db.SaveChanges();

        return Ok(new
        {
            req.Id,
            req.Type,
            req.SellerSub,
            req.CategoryId,
            req.Status,
            req.ApprovedUtc
        });
    }
}
