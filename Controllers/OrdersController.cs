using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Marketplace.Api.Data;
using Marketplace.Api.Data.Entities;
using Marketplace.Api.Models;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;

namespace Marketplace.Api.Controllers;

[ApiController]
[Route("api/orders")]
[Authorize(Roles = "buyer")]
public class OrdersController : ControllerBase
{
    private readonly MarketplaceDbContext _db;

    public OrdersController(MarketplaceDbContext db)
    {
        _db = db;
    }

    // ✅ Robust sub extraction (works even if inbound claims are mapped)
    private string? GetSub()
    {
        return User.FindFirstValue("sub")
            ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier");
    }

    // 5) BUYER: POST /api/orders
    [HttpPost]
    public IActionResult PlaceOrder([FromBody] CreateOrderRequest req)
    {
        var buyerSub = GetSub();
        if (string.IsNullOrWhiteSpace(buyerSub))
            return Unauthorized("Missing sub claim.");

        if (req?.Items is null || req.Items.Count == 0)
            return BadRequest("Order must contain items.");

        // Validate products exist + qty
        foreach (var item in req.Items)
        {
            if (item.Qty <= 0)
                return BadRequest("Qty must be > 0.");

            var exists = _db.Products.AsNoTracking().Any(p => p.Id == item.ProductId);
            if (!exists)
                return BadRequest($"Invalid ProductId: {item.ProductId}");
        }

        var order = new OrderEntity
        {
            BuyerSub = buyerSub,
            CreatedUtc = DateTime.UtcNow,
            Items = req.Items.Select(i => new OrderItemEntity
            {
                ProductId = i.ProductId,
                Qty = i.Qty
            }).ToList()
        };

        _db.Orders.Add(order);
        _db.SaveChanges();

        return Ok(new
        {
            orderId = order.Id,
            createdUtc = order.CreatedUtc,
            items = order.Items.Select(i => new { productId = i.ProductId, qty = i.Qty }).ToList()
        });
    }

    // BUYER: GET /api/orders
    [HttpGet]
    public IActionResult GetMyOrders()
    {
        var buyerSub = GetSub();
        if (string.IsNullOrWhiteSpace(buyerSub))
            return Unauthorized("Missing sub claim.");

        var orders = _db.Orders
            .AsNoTracking()
            .Include(o => o.Items)
            .Where(o => o.BuyerSub == buyerSub)
            .OrderByDescending(o => o.CreatedUtc)
            .Select(o => new
            {
                orderId = o.Id,
                createdUtc = o.CreatedUtc,
                items = o.Items.Select(i => new { productId = i.ProductId, qty = i.Qty }).ToList()
            })
            .ToList();

        return Ok(orders);
    }
}
