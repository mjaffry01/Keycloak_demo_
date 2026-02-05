namespace Marketplace.Api.Data.Entities;

public class OrderItemEntity
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public int ProductId { get; set; }
    public int Qty { get; set; }

    public OrderEntity? Order { get; set; }
    public ProductEntity? Product { get; set; }
}
