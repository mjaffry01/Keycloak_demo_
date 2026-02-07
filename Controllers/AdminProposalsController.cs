using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Marketplace.Api.Data;
using Marketplace.Api.Data.Entities;
using Marketplace.Api.Models;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;

namespace Marketplace.Api.Controllers;

[ApiController]
[ApiExplorerSettings(IgnoreApi = true)]
[Route("api/admin/proposals")]
[Authorize(Roles = "admin")]
[Tags("10 - Admin")]
public class AdminProposalsController : ControllerBase
{
    private readonly MarketplaceDbContext _db;

    public AdminProposalsController(MarketplaceDbContext db)
    {
        _db = db;
    }

    public class ReviewRequest
    {
        public int ProposalId { get; set; }
        public string Decision { get; set; } = default!; // Approved | Rejected
        public string? RejectionReason { get; set; }
    }

    // -----------------------------
    // PRODUCT PROPOSALS
    // -----------------------------

    [HttpGet("pending-product-proposals")]
    public IActionResult PendingProductProposals()
    {
        var pending = _db.ProductProposals
            .AsNoTracking()
            .Where(p => p.Status == "Pending")
            .OrderBy(p => p.CreatedUtc)
            .ToList();

        return Ok(pending);
    }

    [HttpPost("review-product-proposal")]
    public IActionResult ReviewProductProposal([FromBody] ReviewRequest dto)
    {
        var decision = (dto.Decision ?? "").Trim();
        if (decision != "Approved" && decision != "Rejected")
            return BadRequest("Decision must be 'Approved' or 'Rejected'.");

        var proposal = _db.ProductProposals.FirstOrDefault(p => p.Id == dto.ProposalId);
        if (proposal is null)
            return NotFound("Product proposal not found.");

        if (proposal.Status != "Pending")
            return Ok(proposal); // idempotent

        var adminSub = User.FindFirstValue("sub") ?? "admin";

        if (decision == "Rejected")
        {
            proposal.Status = "Rejected";
            proposal.RejectionReason = (dto.RejectionReason ?? "").Trim();
            proposal.ReviewedUtc = DateTime.UtcNow;
            proposal.ReviewedBy = adminSub;

            _db.SaveChanges();
            return Ok(proposal);
        }

        // Decision == Approved
        // Business rule: seller must be approved for this category before a product can be created
        var isApprovedForCategory = _db.CategoryApprovalRequests.Any(r =>
            r.Status == "Approved" &&
            r.SellerSub == proposal.SellerSub &&
            r.CategoryId == proposal.CategoryId);

        if (!isApprovedForCategory)
            return BadRequest("Seller is not approved for this category. Approve the category first, then approve the product proposal.");

        // Create product record
        var product = new ProductEntity
        {
            SellerSub = proposal.SellerSub,
            CategoryId = proposal.CategoryId,
            Name = proposal.Name,
            Price = proposal.Price,
            StockQty = proposal.InitialStockQty,
            CreatedUtc = DateTime.UtcNow
        };

        _db.Products.Add(product);

        // mark proposal approved
        proposal.Status = "Approved";
        proposal.ReviewedUtc = DateTime.UtcNow;
        proposal.ReviewedBy = adminSub;

        _db.SaveChanges();

        proposal.CreatedProductId = product.Id;
        _db.SaveChanges();

        return Ok(proposal);
    }

    // -----------------------------
    // CATEGORY PROPOSALS (optional but fixes the flow)
    // -----------------------------

    [HttpGet("pending-category-proposals")]
    public IActionResult PendingCategoryProposals()
    {
        var pending = _db.CategoryProposals
            .AsNoTracking()
            .Where(p => p.Status == "Pending")
            .OrderBy(p => p.CreatedUtc)
            .ToList();

        return Ok(pending);
    }

    [HttpPost("review-category-proposal")]
    public IActionResult ReviewCategoryProposal([FromBody] ReviewRequest dto)
    {
        var decision = (dto.Decision ?? "").Trim();
        if (decision != "Approved" && decision != "Rejected")
            return BadRequest("Decision must be 'Approved' or 'Rejected'.");

        var proposal = _db.CategoryProposals.FirstOrDefault(p => p.Id == dto.ProposalId);
        if (proposal is null)
            return NotFound("Category proposal not found.");

        if (proposal.Status != "Pending")
            return Ok(proposal);

        var adminSub = User.FindFirstValue("sub") ?? "admin";

        if (decision == "Rejected")
        {
            proposal.Status = "Rejected";
            proposal.RejectionReason = (dto.RejectionReason ?? "").Trim();
            proposal.ReviewedUtc = DateTime.UtcNow;
            proposal.ReviewedBy = adminSub;

            _db.SaveChanges();
            return Ok(proposal);
        }

        // Approved: create category if doesn't exist (case-insensitive)
        var name = proposal.Name.Trim();
        var existing = _db.Categories.FirstOrDefault(c => c.Name.ToLower() == name.ToLower());
        if (existing is null)
        {
            existing = new Category { Name = name };
            _db.Categories.Add(existing);
            _db.SaveChanges();
        }

        proposal.Status = "Approved";
        proposal.ReviewedUtc = DateTime.UtcNow;
        proposal.ReviewedBy = adminSub;
        proposal.CreatedCategoryId = existing.Id;

        _db.SaveChanges();
        return Ok(proposal);
    }
}
