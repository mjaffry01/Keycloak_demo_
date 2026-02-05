using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Marketplace.Api.Data;
using Marketplace.Api.Data.Entities;
using Marketplace.Api.Models;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;

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

    // 5) BUYER: POST /api/orders
    [HttpPost]
    public IActionResult PlaceOrder([FromBody] CreateOrderRequest req)
    {
        var buyerSub = User.FindFirstValue("sub");
        if (string.IsNullOrWhiteSpace(buyerSub)) return Unauthorized("Missing sub claim.");

        if (req.Items is null || req.Items.Count == 0) return BadRequest("Order must contain items.");

        // validate products exist
        foreach (var item in req.Items)
        {
            if (item.Qty <= 0) return BadRequest("Qty must be > 0.");
            var exists = _db.Products.AsNoTracking().Any(p => p.Id == item.ProductId);
            if (!exists) return BadRequest($"Invalid ProductId: {item.ProductId}");
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

        return Ok(new { order.Id, order.CreatedUtc, Items = order.Items.Select(i => new { i.ProductId, i.Qty }) });
    }

    // BUYER: GET /api/orders
    [HttpGet]
    public IActionResult GetMyOrders()
    {
        var buyerSub = User.FindFirstValue("sub");
        if (string.IsNullOrWhiteSpace(buyerSub)) return Unauthorized("Missing sub claim.");

        var orders = _db.Orders
            .AsNoTracking()
            .Include(o => o.Items)
            .Where(o => o.BuyerSub == buyerSub)
            .OrderByDescending(o => o.CreatedUtc)
            .Select(o => new
            {
                o.Id,
                o.CreatedUtc,
                Items = o.Items.Select(i => new { i.ProductId, i.Qty }).ToList()
            })
            .ToList();

        return Ok(orders);
    }
}
