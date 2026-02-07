namespace Marketplace.Api.Models;

public record ProductDto(int Id, int CategoryId, string Name, decimal Price, int StockQty);
public record CreateProductRequest(int CategoryId, string Name, decimal Price, int StockQty);
public record UpdateProductRequest(decimal Price, int StockQty);

public record PendingApprovalDto(string SellerSub, int CategoryId, string Status, DateTime CreatedUtc);
public record ApproveCategoryRequest(string SellerSub, int CategoryId);
public record CategoryRequestCreate(int CategoryId);

public record CreateOrderRequest(List<CreateOrderItem> Items);
public record CreateOrderItem(int ProductId, int Qty);

public record CreateFeedbackRequest(int ProductId, int Rating, string Comment);
public record FeedbackDto(int Id, int ProductId, int Rating, string Comment, DateTime CreatedUtc);

public record LoginRequest(string Username, string Password);