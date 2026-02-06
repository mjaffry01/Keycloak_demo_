using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Marketplace.Api.Data;
using Marketplace.Api.Data.Entities;
using Marketplace.Api.Models;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;

namespace Marketplace.Api.Controllers;

[ApiController]
[Route("api/feedback")]
[Authorize(Roles = "buyer")]
[Tags("30 - Buyer")]
public class FeedbackController : ControllerBase
{
    private readonly MarketplaceDbContext _db;

    public FeedbackController(MarketplaceDbContext db)
    {
        _db = db;
    }

    // 6) BUYER: POST /api/feedback
    [HttpPost]
    public IActionResult SubmitFeedback([FromBody] CreateFeedbackRequest req)
    {
        var buyerSub = User.FindFirstValue("sub");
        if (string.IsNullOrWhiteSpace(buyerSub)) return Unauthorized("Missing sub claim.");

        var productExists = _db.Products.AsNoTracking().Any(p => p.Id == req.ProductId);
        if (!productExists)
            return BadRequest("Invalid ProductId.");

        if (req.Rating is < 1 or > 5) return BadRequest("Rating must be between 1 and 5.");
        if (string.IsNullOrWhiteSpace(req.Comment)) return BadRequest("Comment is required.");

        var feedback = new FeedbackEntity
        {
            ProductId = req.ProductId,
            BuyerSub = buyerSub,
            Rating = req.Rating,
            Comment = req.Comment.Trim(),
            CreatedUtc = DateTime.UtcNow
        };

        _db.Feedbacks.Add(feedback);

        try
        {
            _db.SaveChanges();
        }
        catch (DbUpdateException)
        {
            // Likely unique constraint: one feedback per buyer+product
            return Conflict("You have already submitted feedback for this product.");
        }

        return Ok(new FeedbackDto(feedback.Id, feedback.ProductId, feedback.Rating, feedback.Comment, feedback.CreatedUtc));
    }

    // BUYER: GET /api/feedback/{productId}
    [HttpGet("{productId:int}")]
    [AllowAnonymous]
    public IActionResult GetProductFeedback(int productId)
    {
        var items = _db.Feedbacks
            .AsNoTracking()
            .Where(f => f.ProductId == productId)
            .OrderByDescending(f => f.CreatedUtc)
            .Select(f => new FeedbackDto(f.Id, f.ProductId, f.Rating, f.Comment, f.CreatedUtc))
            .ToList();

        return Ok(items);
    }
}
