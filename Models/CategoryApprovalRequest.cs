namespace Marketplace.Api.Models;

public class CategoryApprovalRequest
{
    public int Id { get; set; }
    public string Type { get; set; } = "CategoryApproval";
    public string SellerSub { get; set; } = "";
    public int CategoryId { get; set; }
    public string Status { get; set; } = "Pending"; // Pending / Approved / Rejected (optional later)
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ApprovedUtc { get; set; }
}
