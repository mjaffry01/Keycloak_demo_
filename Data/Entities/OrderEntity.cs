namespace Marketplace.Api.Data.Entities;

public class OrderEntity
{
    public int Id { get; set; }
    public string BuyerSub { get; set; } = "";
    public DateTime CreatedUtc { get; set; }

    public List<OrderItemEntity> Items { get; set; } = new();
}
