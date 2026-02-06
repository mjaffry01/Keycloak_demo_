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
[Tags("30 - Buyer")]
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

    // BUYER: POST /api/orders
    // - Validates stock
    // - Reduces stock
    // - Creates order (all in one transaction)
    [HttpPost]
    public IActionResult PlaceOrder([FromBody] CreateOrderRequest req)
    {
        var buyerSub = GetSub();
        if (string.IsNullOrWhiteSpace(buyerSub))
            return Unauthorized("Missing sub claim.");

        if (req?.Items is null || req.Items.Count == 0)
            return BadRequest("Order must contain items.");

        // 1️⃣ Merge duplicate products (same product appears multiple times)
        var lines = req.Items
            .GroupBy(i => i.ProductId)
            .Select(g => new { ProductId = g.Key, Qty = g.Sum(x => x.Qty) })
            .ToList();

        if (lines.Any(l => l.Qty <= 0))
            return BadRequest("Qty must be > 0.");

        using var tx = _db.Database.BeginTransaction();

        // 2️⃣ Load all products in ONE query
        var productIds = lines.Select(l => l.ProductId).ToList();

        var products = _db.Products
            .Where(p => productIds.Contains(p.Id))
            .ToList();

        // 3️⃣ Validate all products exist
        if (products.Count != productIds.Count)
        {
            var found = products.Select(p => p.Id).ToHashSet();
            var missing = productIds.Where(id => !found.Contains(id));
            tx.Rollback();
            return BadRequest($"Invalid ProductId(s): {string.Join(",", missing)}");
        }

        // 4️⃣ Check stock and reduce
        foreach (var line in lines)
        {
            var p = products.Single(x => x.Id == line.ProductId);

            if (p.StockQty < line.Qty)
            {
                tx.Rollback();
                return BadRequest(
                    $"Insufficient stock for ProductId {p.Id} ({p.Name}). Available: {p.StockQty}, Requested: {line.Qty}"
                );
            }

            p.StockQty -= line.Qty;
        }

        // 5️⃣ Create order
        var order = new OrderEntity
        {
            BuyerSub = buyerSub,
            CreatedUtc = DateTime.UtcNow,
            Items = lines.Select(l => new OrderItemEntity
            {
                ProductId = l.ProductId,
                Qty = l.Qty
            }).ToList()
        };

        _db.Orders.Add(order);

        // 6️⃣ Save stock updates + order together
        _db.SaveChanges();
        tx.Commit();

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
