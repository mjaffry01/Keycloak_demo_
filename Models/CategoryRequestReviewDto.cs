namespace Marketplace.Api.Models;

/// <summary>
/// Admin decision payload for a seller's category request.
/// </summary>
public class CategoryRequestReviewDto
{
    /// <summary>
    /// Must be either <c>Approved</c> or <c>Rejected</c>.
    /// </summary>
    public string Status { get; set; } = "";

    /// <summary>
    /// Optional reason when rejecting a request.
    /// (Not persisted in DB in this minimal assignment scope.)
    /// </summary>
    public string? RejectionReason { get; set; }
}
