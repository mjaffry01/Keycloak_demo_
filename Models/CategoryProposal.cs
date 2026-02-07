namespace Marketplace.Api.Models;

public class CategoryProposal
{
    public int Id { get; set; }
    public string SellerSub { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string Status { get; set; } = "Pending"; // Pending | Approved | Rejected
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

    public DateTime? ReviewedUtc { get; set; }
    public string? ReviewedBy { get; set; }
    public string? RejectionReason { get; set; }

    public int? CreatedCategoryId { get; set; }  // filled when approved
}
