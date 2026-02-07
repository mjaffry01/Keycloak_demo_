using Marketplace.Api.Data.Entities;
using Marketplace.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Marketplace.Api.Data;

public class MarketplaceDbContext : DbContext
{
    public MarketplaceDbContext(DbContextOptions<MarketplaceDbContext> options) : base(options) { }

    public DbSet<Category> Categories => Set<Category>();
    public DbSet<ProductEntity> Products => Set<ProductEntity>();
    public DbSet<OrderEntity> Orders => Set<OrderEntity>();
    public DbSet<OrderItemEntity> OrderItems => Set<OrderItemEntity>();
    public DbSet<FeedbackEntity> Feedbacks => Set<FeedbackEntity>();

    // Reuse existing model as an EF entity (approval workflow)
    public DbSet<CategoryApprovalRequest> CategoryApprovalRequests => Set<CategoryApprovalRequest>();
    public DbSet<CategoryProposal> CategoryProposals => Set<CategoryProposal>();
    public DbSet<ProductProposal> ProductProposals => Set<ProductProposal>();


    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Categories are reference data
        modelBuilder.Entity<Category>().HasData(
            new Category { Id = 1, Name = "Electronics" },
            new Category { Id = 2, Name = "Books" },
            new Category { Id = 3, Name = "Clothing" }
        );

        // Product price precision (Postgres numeric)
        modelBuilder.Entity<ProductEntity>()
            .Property(p => p.Price)
            .HasPrecision(18, 2);

        // ProductProposal price precision (Postgres numeric)
        modelBuilder.Entity<ProductProposal>()
            .Property(p => p.Price)
            .HasPrecision(18, 2);

        // Helpful index for pending duplicate detection
        modelBuilder.Entity<ProductProposal>()
            .HasIndex(p => new { p.SellerSub, p.CategoryId, p.Name, p.Status });

        // One request per seller + category (matches your in-memory index)
        modelBuilder.Entity<CategoryApprovalRequest>()
            .HasIndex(r => new { r.SellerSub, r.CategoryId })
            .IsUnique();

        // Orders: one-to-many items
        modelBuilder.Entity<OrderItemEntity>()
            .HasOne(i => i.Order)
            .WithMany(o => o.Items)
            .HasForeignKey(i => i.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        // Feedback: optional business rule (one feedback per buyer per product)
        modelBuilder.Entity<FeedbackEntity>()
            .HasIndex(f => new { f.ProductId, f.BuyerSub })
            .IsUnique();

        base.OnModelCreating(modelBuilder);
    }
}
