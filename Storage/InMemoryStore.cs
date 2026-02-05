using System.Collections.Concurrent;
using Marketplace.Api.Models;
using System.Threading;

namespace Marketplace.Api.Storage;

/// <summary>
/// Minimal in-memory "database" for MVP/demo.
/// Swap with EF Core/Postgres later.
/// </summary>
public static class InMemoryStore
{
    // ---- Records (immutable) ----
    public record ProductRecord(int Id, string SellerSub, int CategoryId, string Name, decimal Price, DateTime CreatedUtc);
    public record ApprovalRecord(string SellerSub, int CategoryId, string Status, DateTime CreatedUtc, DateTime? ApprovedUtc);
    public record OrderItemRecord(int ProductId, int Qty);
    public record OrderRecord(int Id, string BuyerSub, List<OrderItemRecord> Items, DateTime CreatedUtc);
    public record FeedbackRecord(int Id, int ProductId, string BuyerSub, int Rating, string Comment, DateTime CreatedUtc);

    // ---- Data ----
    // Categories: ID -> Name (seeded)
    public static ConcurrentDictionary<int, string> Categories { get; } = new();

    // Approvals: "{sellerSub}:{categoryId}" -> Approval
    public static ConcurrentDictionary<string, ApprovalRecord> Approvals { get; } = new();


    // ---- Category approval requests (with ID + type) ----
    private static int _approvalRequestSeq = 0;

    // requestId -> request
    public static ConcurrentDictionary<int, CategoryApprovalRequest> ApprovalRequests { get; } = new();

    // unique constraint: one request per seller+category (optional but recommended)
    // key = $"{sellerSub}:{categoryId}" -> requestId
    public static ConcurrentDictionary<string, int> ApprovalRequestIndex { get; } = new();

    public static int NextApprovalRequestId() => Interlocked.Increment(ref _approvalRequestSeq);

    // Products: productId -> Product
    public static ConcurrentDictionary<int, ProductRecord> Products { get; } = new();

    // Orders: orderId -> Order
    public static ConcurrentDictionary<int, OrderRecord> Orders { get; } = new();

    // Feedbacks: feedbackId -> Feedback
    public static ConcurrentDictionary<int, FeedbackRecord> Feedbacks { get; } = new();

    private static int _productId = 0;
    private static int _orderId = 0;
    private static int _feedbackId = 0;

    static InMemoryStore()
    {
        // Seed categories (adjust anytime)
        Categories.TryAdd(1, "Electronics");
        Categories.TryAdd(2, "Books");
        Categories.TryAdd(3, "Clothing");
    }

    public static int NextProductId() => Interlocked.Increment(ref _productId);
    public static int NextOrderId() => Interlocked.Increment(ref _orderId);
    public static int NextFeedbackId() => Interlocked.Increment(ref _feedbackId);
}
