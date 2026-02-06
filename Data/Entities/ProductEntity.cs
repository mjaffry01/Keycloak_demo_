namespace Marketplace.Api.Data.Entities;

public class ProductEntity
{
    public int Id { get; set; }
    public string SellerSub { get; set; } = "";
    public int CategoryId { get; set; }
    public string Name { get; set; } = "";
    public decimal Price { get; set; }
    public DateTime CreatedUtc { get; set; }

    public Category? Category { get; set; }
    public int StockQty { get; set; } = 0;

    public bool IsActive { get; set; } = true;
}
