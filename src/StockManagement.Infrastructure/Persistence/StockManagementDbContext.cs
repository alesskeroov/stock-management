using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using StockManagement.Domain.Entities;
using StockManagement.Infrastructure.Identity;

namespace StockManagement.Infrastructure.Persistence;

public sealed class StockManagementDbContext(DbContextOptions<StockManagementDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Warehouse> Warehouses => Set<Warehouse>();
    public DbSet<Stock> Stocks => Set<Stock>();
    public DbSet<ProductBatch> ProductBatches => Set<ProductBatch>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<StockTransaction> StockTransactions => Set<StockTransaction>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<ApplicationUser>(entity =>
        {
            entity.Property(x => x.FirstName).HasMaxLength(100).IsRequired();
            entity.Property(x => x.LastName).HasMaxLength(100).IsRequired();
            entity.Property(x => x.IsActive).IsRequired();
        });

        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.TokenHash).HasMaxLength(64).IsRequired();
            entity.HasIndex(x => x.TokenHash).IsUnique();
            entity.HasIndex(x => new { x.UserId, x.ExpiresAtUtc });
            entity.HasOne(x => x.User).WithMany(x => x.RefreshTokens)
                .HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Product>(entity =>
        {
            entity.HasQueryFilter(x => !x.IsDeleted);
            entity.Property(x => x.IsDeleted).HasDefaultValue(false).IsRequired();
            entity.Property(x => x.DeletedAt);
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Sku).HasMaxLength(64).IsRequired();
            entity.HasIndex(x => x.Sku).IsUnique();
            entity.Property(x => x.Barcode).HasMaxLength(64).IsRequired();
            entity.HasIndex(x => x.Barcode).IsUnique().HasFilter("[Barcode] <> N''");
            entity.Property(x => x.Description).HasMaxLength(1000);
            entity.Property(x => x.UnitOfMeasure).HasMaxLength(32).IsRequired();
            entity.Property(x => x.UnitPrice).HasPrecision(18, 2);
            entity.Property(x => x.EntryDate).HasColumnName("EntryDate").HasDefaultValueSql("SYSUTCDATETIME()").IsRequired();
            entity.HasOne(x => x.Category).WithMany(x => x.Products)
                .HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Category>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(150).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(1000);
            entity.HasOne(x => x.ParentCategory).WithMany(x => x.Subcategories)
                .HasForeignKey(x => x.ParentCategoryId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Warehouse>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(150).IsRequired();
            entity.Property(x => x.Location).HasMaxLength(500).IsRequired();
        });

        modelBuilder.Entity<Stock>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.ProductId, x.WarehouseId }).IsUnique();
            entity.Property(x => x.Quantity).HasPrecision(18, 3);
            entity.Property(x => x.CriticalLowLevel).HasPrecision(18, 3);
            entity.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Warehouse).WithMany(x => x.Stocks).HasForeignKey(x => x.WarehouseId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ProductBatch>(entity =>
        {
            entity.HasQueryFilter(x => !x.IsDeleted);
            entity.Property(x => x.IsDeleted).HasDefaultValue(false).IsRequired();
            entity.Property(x => x.DeletedAt);
            entity.HasKey(x => x.Id);
            entity.Property(x => x.BatchNumber).HasColumnName("BatchNumber").HasMaxLength(64).IsRequired();
            entity.HasIndex(x => x.BatchNumber).IsUnique();
            entity.HasIndex(x => new { x.ProductId, x.WarehouseId, x.EntryDate, x.Id });
            entity.Property(x => x.EntryDate).HasColumnName("EntryDate").IsRequired();
            entity.Property(x => x.InitialQuantity).HasColumnName("InitialQuantity").HasPrecision(18, 3).IsRequired();
            entity.Property(x => x.RemainingQuantity).HasColumnName("RemainingQuantity").HasPrecision(18, 3).IsRequired();
            entity.Property(x => x.ExpiryDate).HasColumnName("ExpiryDate");
            entity.Property(x => x.PurchasePrice).HasPrecision(18, 2);
            entity.Property(x => x.SalePrice).HasPrecision(18, 2);
            entity.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Warehouse).WithMany().HasForeignKey(x => x.WarehouseId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Supplier>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
            entity.Property(x => x.ContactPerson).HasMaxLength(150);
            entity.Property(x => x.Email).HasMaxLength(320);
            entity.Property(x => x.Phone).HasMaxLength(32);
            entity.Property(x => x.Address).HasMaxLength(500);
        });

        modelBuilder.Entity<StockTransaction>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.TransactionType).HasConversion<int>();
            entity.Property(x => x.Quantity).HasPrecision(18, 3);
            entity.Property(x => x.ReferenceNo).HasMaxLength(100);
            entity.Property(x => x.UnitPriceAtTime).HasPrecision(18, 2);
            entity.Property(x => x.Description).HasMaxLength(1000);
            entity.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Warehouse).WithMany(x => x.StockTransactions)
                .HasForeignKey(x => x.WarehouseId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.AppUserId)
                .IsRequired().OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Warehouse>()
            .HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.ManagerId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
