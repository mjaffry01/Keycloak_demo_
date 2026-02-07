namespace Marketplace.Api.Models;

public class ProductProposal
{
    public int Id { get; set; }

    public string SellerSub { get; set; } = default!;
    public int CategoryId { get; set; }

    public string Name { get; set; } = default!;
    public decimal Price { get; set; }
    public int InitialStockQty { get; set; }

    public string Status { get; set; } = "Pending"; // Pending | Approved | Rejected
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

    public DateTime? ReviewedUtc { get; set; }
    public string? ReviewedBy { get; set; }
    public string? RejectionReason { get; set; }

    public int? CreatedProductId { get; set; } // filled when approved
}
