namespace Marketplace.Api.Data.Entities;

public class FeedbackEntity
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public string BuyerSub { get; set; } = "";
    public int Rating { get; set; }
    public string Comment { get; set; } = "";
    public DateTime CreatedUtc { get; set; }

    public ProductEntity? Product { get; set; }
}
