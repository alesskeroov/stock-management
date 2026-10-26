using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using StockManagement.Domain.Entities;
using StockManagement.Domain.Enums;
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
    public DbSet<StockMovement> StockMovements => Set<StockMovement>();
    public DbSet<StockLog> StockLogs => Set<StockLog>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<DocumentSequence> DocumentSequences => Set<DocumentSequence>();
    public DbSet<Document> Documents => Set<Document>();
    public DbSet<DocumentItem> DocumentItems => Set<DocumentItem>();
    public DbSet<PurchaseOrder> PurchaseOrders => Set<PurchaseOrder>();
    public DbSet<PurchaseOrderItem> PurchaseOrderItems => Set<PurchaseOrderItem>();
    public DbSet<GoodsReceipt> GoodsReceipts => Set<GoodsReceipt>();
    public DbSet<GoodsReceiptItem> GoodsReceiptItems => Set<GoodsReceiptItem>();
    public DbSet<StockNotification> StockNotifications => Set<StockNotification>();
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
            entity.Property(x => x.Stock).HasColumnName("stock").HasPrecision(18, 3).HasDefaultValue(0m).IsRequired();
            entity.Property(x => x.ReorderLevel).HasPrecision(18, 3).HasDefaultValue(10m).IsRequired();
            entity.Property(x => x.AverageCost).HasColumnName("average_cost").HasPrecision(18, 2).HasDefaultValue(0m).IsRequired();
            entity.Property(x => x.EntryDate).HasColumnName("EntryDate").HasDefaultValueSql("SYSUTCDATETIME()").IsRequired();
            entity.HasOne(x => x.Category).WithMany(x => x.Products)
                .HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<StockNotification>(entity =>
        {
            entity.ToTable("stock_notifications");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Message).HasMaxLength(500).IsRequired();
            entity.Property(x => x.Type).HasConversion<string>().HasMaxLength(32).IsRequired();
            entity.Property(x => x.IsRead).HasDefaultValue(false).IsRequired();
            entity.Property(x => x.IsActive).HasDefaultValue(true).IsRequired();
            entity.Property(x => x.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()").IsRequired();
            entity.HasIndex(x => new { x.ProductId, x.IsRead });
            entity.HasIndex(x => x.CreatedAt);
            entity.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId)
                .OnDelete(DeleteBehavior.Restrict);
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
            entity.HasQueryFilter(x => !x.IsDeleted);
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
            entity.Property(x => x.ContactPerson).HasMaxLength(150);
            entity.Property(x => x.Email).HasMaxLength(320);
            entity.Property(x => x.Phone).HasMaxLength(32);
            entity.Property(x => x.Address).HasMaxLength(500);
            entity.Property(x => x.TaxId).HasMaxLength(32).IsRequired();
            entity.Property(x => x.IsActive).HasDefaultValue(true).IsRequired();
            entity.Property(x => x.IsDeleted).HasDefaultValue(false).IsRequired();
            entity.Property(x => x.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()").IsRequired();
            entity.Property(x => x.UpdatedAt).HasDefaultValueSql("SYSUTCDATETIME()").IsRequired();
            entity.HasIndex(x => x.Name);
        });

        modelBuilder.Entity<StockMovement>(entity =>
        {
            entity.ToTable("stock_movements", table => table.HasCheckConstraint(
                "CK_stock_movements_quantity", "[quantity] <> 0 OR [type] = 'OPENING_BALANCE'"));
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.ProductId).HasColumnName("product_id").IsRequired();
            entity.Property(x => x.WarehouseId).HasColumnName("warehouse_id").IsRequired();
            entity.Property(x => x.Type).HasColumnName("type").HasMaxLength(24).IsRequired()
                .HasConversion(new ValueConverter<StockMovementType, string>(
                    value => StockMovementTypeCodes.ToStorage(value),
                    value => StockMovementTypeCodes.FromStorage(value)));
            entity.Property(x => x.Quantity).HasColumnName("quantity").HasPrecision(18, 3).IsRequired();
            entity.Property(x => x.UnitCost).HasColumnName("unit_cost").HasPrecision(18, 2).IsRequired();
            entity.Property(x => x.ReferenceType).HasColumnName("reference_type").HasMaxLength(40).IsRequired();
            entity.Property(x => x.ReferenceId).HasColumnName("reference_id");
            entity.Property(x => x.CreatedBy).HasColumnName("created_by").IsRequired();
            entity.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("SYSUTCDATETIME()").IsRequired();
            entity.HasIndex(x => new { x.ProductId, x.WarehouseId }, "IX_stock_movements_product_warehouse");
            entity.HasIndex(x => new { x.ReferenceType, x.ReferenceId }).HasDatabaseName("IX_stock_movements_reference");
            entity.HasIndex(x => new { x.ProductId, x.WarehouseId }, "UX_stock_movements_opening_balance")
                .IsUnique().HasDatabaseName("UX_stock_movements_opening_balance")
                .HasFilter("[type] = 'OPENING_BALANCE'");
            entity.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Warehouse).WithMany().HasForeignKey(x => x.WarehouseId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.CreatedBy).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<StockLog>(entity =>
        {
            entity.ToTable("stock_logs");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.ProductId).HasColumnName("product_id");
            entity.Property(x => x.ProductName).HasColumnName("product_name").HasMaxLength(200).IsRequired();
            entity.Property(x => x.Type).HasColumnName("type").HasMaxLength(8).HasConversion<string>().IsRequired();
            entity.Property(x => x.QuantityChange).HasColumnName("quantity_change").HasPrecision(18, 3).IsRequired();
            entity.Property(x => x.QuantityBefore).HasColumnName("quantity_before").HasPrecision(18, 3).IsRequired();
            entity.Property(x => x.QuantityAfter).HasColumnName("quantity_after").HasPrecision(18, 3).IsRequired();
            entity.Property(x => x.Reason).HasColumnName("reason").HasMaxLength(1000);
            entity.Property(x => x.CreatedBy).HasColumnName("created_by");
            entity.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("SYSUTCDATETIME()").IsRequired();
            entity.HasIndex(x => x.CreatedAt).HasDatabaseName("IX_stock_logs_created_at");
            entity.HasIndex(x => new { x.ProductId, x.CreatedAt }).HasDatabaseName("IX_stock_logs_product_created_at");
            entity.HasOne<Product>().WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.CreatedBy).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.ToTable("audit_logs");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.UserId).HasColumnName("user_id");
            entity.Property(x => x.Action).HasColumnName("action");
            entity.Property(x => x.EntityType).HasColumnName("entity_type");
            entity.Property(x => x.EntityId).HasColumnName("entity_id");
            entity.Property(x => x.OldValues).HasColumnName("old_values");
            entity.Property(x => x.NewValues).HasColumnName("new_values");
            entity.Property(x => x.CreatedAt).HasColumnName("created_at");
            entity.Property(x => x.Action).HasMaxLength(64).IsRequired();
            entity.Property(x => x.EntityType).HasMaxLength(64).IsRequired();
            entity.Property(x => x.OldValues).HasColumnType("nvarchar(max)");
            entity.Property(x => x.NewValues).HasColumnType("nvarchar(max)");
            entity.Property(x => x.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()").IsRequired();
            entity.HasIndex(x => new { x.EntityType, x.EntityId, x.CreatedAt });
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<DocumentSequence>(entity =>
        {
            entity.ToTable("document_sequences");
            entity.HasKey(x => new { x.Type, x.Year });
            entity.Property(x => x.Type).HasColumnName("type").HasMaxLength(3).IsUnicode(false);
            entity.Property(x => x.Year).HasColumnName("year").ValueGeneratedNever();
            entity.Property(x => x.LastNumber).HasColumnName("last_number").IsRequired();
        });

        modelBuilder.Entity<Document>(entity =>
        {
            entity.ToTable("documents", table =>
            {
                table.HasCheckConstraint("CK_documents_type", "[type] IN ('in', 'out')");
                table.HasCheckConstraint("CK_documents_status", "[status] IN ('draft', 'confirmed', 'cancelled')");
            });
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.Number).HasColumnName("number").HasMaxLength(20).IsRequired();
            entity.Property(x => x.Type).HasColumnName("type").HasMaxLength(3).IsUnicode(false).IsRequired();
            entity.Property(x => x.Date).HasColumnName("date").HasColumnType("date").IsRequired();
            entity.Property(x => x.Partner).HasColumnName("partner").HasMaxLength(200);
            entity.Property(x => x.Status).HasColumnName("status").HasMaxLength(16).IsUnicode(false).HasDefaultValue("draft").IsRequired();
            entity.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("SYSUTCDATETIME()").IsRequired();
            entity.HasIndex(x => x.Number).IsUnique();
            entity.HasMany(x => x.Items).WithOne(x => x.Document).HasForeignKey(x => x.DocumentId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<DocumentItem>(entity =>
        {
            entity.ToTable("document_items", table =>
            {
                table.HasCheckConstraint("CK_document_items_qty", "[qty] > 0");
                table.HasCheckConstraint("CK_document_items_price", "[price] >= 0");
            });
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.DocumentId).HasColumnName("document_id").IsRequired();
            entity.Property(x => x.ProductId).HasColumnName("product_id").IsRequired();
            entity.Property(x => x.Qty).HasColumnName("qty").HasPrecision(18, 3).IsRequired();
            entity.Property(x => x.Price).HasColumnName("price").HasPrecision(18, 2).IsRequired();
            entity.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<PurchaseOrder>(entity =>
        {
            entity.ToTable("purchase_orders", table => table.HasCheckConstraint(
                "CK_purchase_orders_total_amount", "[total_amount] >= 0"));
            entity.HasQueryFilter(x => !x.IsDeleted);
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.PoNumber).HasColumnName("po_number").HasMaxLength(32).IsRequired();
            entity.HasIndex(x => x.PoNumber).IsUnique();
            entity.Property(x => x.SupplierId).HasColumnName("supplier_id");
            entity.Property(x => x.WarehouseId).HasColumnName("warehouse_id");
            entity.Property(x => x.Status).HasColumnName("status").HasConversion(
                value => PurchaseOrderStatusCodes.ToStorage(value),
                value => PurchaseOrderStatusCodes.FromStorage(value)).HasMaxLength(32).IsRequired();
            entity.Property(x => x.OrderDate).HasColumnName("order_date");
            entity.Property(x => x.ExpectedDate).HasColumnName("expected_date");
            entity.Property(x => x.Currency).HasColumnName("currency").HasMaxLength(3).IsUnicode(false).IsRequired();
            entity.Property(x => x.TotalAmount).HasColumnName("total_amount").HasPrecision(18, 2).IsRequired();
            entity.Property(x => x.Note).HasColumnName("note").HasMaxLength(2000);
            entity.Property(x => x.CreatedBy).HasColumnName("created_by");
            entity.Property(x => x.ApprovedBy).HasColumnName("approved_by");
            entity.Property(x => x.ApprovedAt).HasColumnName("approved_at");
            entity.Property(x => x.CancelledBy).HasColumnName("cancelled_by");
            entity.Property(x => x.CancelledAt).HasColumnName("cancelled_at");
            entity.Property(x => x.CancelReason).HasColumnName("cancel_reason").HasMaxLength(1000);
            entity.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("SYSUTCDATETIME()").IsRequired();
            entity.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("SYSUTCDATETIME()").IsRequired();
            entity.Property(x => x.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false).IsRequired();
            entity.Property(x => x.DeletedAt).HasColumnName("deleted_at");
            entity.HasIndex(x => x.Status);
            entity.HasIndex(x => x.SupplierId);
            entity.HasIndex(x => x.ExpectedDate);
            entity.HasOne(x => x.Supplier).WithMany().HasForeignKey(x => x.SupplierId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Warehouse).WithMany().HasForeignKey(x => x.WarehouseId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.CreatedBy).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.ApprovedBy).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.CancelledBy).OnDelete(DeleteBehavior.Restrict);
            entity.HasMany(x => x.Items).WithOne(x => x.PurchaseOrder).HasForeignKey(x => x.PurchaseOrderId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(x => x.GoodsReceipts).WithOne(x => x.PurchaseOrder).HasForeignKey(x => x.PurchaseOrderId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<PurchaseOrderItem>(entity =>
        {
            entity.ToTable("purchase_order_items", table =>
            {
                table.HasCheckConstraint("CK_purchase_order_items_ordered_qty", "[ordered_qty] > 0");
                table.HasCheckConstraint("CK_purchase_order_items_received_qty", "[received_qty] >= 0");
                table.HasCheckConstraint("CK_purchase_order_items_unit_price", "[unit_price] >= 0");
            });
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.PurchaseOrderId).HasColumnName("purchase_order_id");
            entity.Property(x => x.ProductId).HasColumnName("product_id");
            entity.Property(x => x.OrderedQty).HasColumnName("ordered_qty").HasPrecision(18, 3).IsRequired();
            entity.Property(x => x.ReceivedQty).HasColumnName("received_qty").HasPrecision(18, 3).HasDefaultValue(0).IsRequired();
            entity.Property(x => x.UnitPrice).HasColumnName("unit_price").HasPrecision(18, 2).IsRequired();
            entity.Property(x => x.LineTotal).HasColumnName("line_total").HasPrecision(18, 2).IsRequired();
            entity.HasIndex(x => new { x.PurchaseOrderId, x.ProductId }).IsUnique();
            entity.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<GoodsReceipt>(entity =>
        {
            // SQL Server OUTPUT clauses are incompatible with the append-only/reversal trigger.
            entity.ToTable("goods_receipts", table => table.UseSqlOutputClause(false));
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.GrnNumber).HasColumnName("grn_number").HasMaxLength(32).IsRequired();
            entity.Property(x => x.PurchaseOrderId).HasColumnName("purchase_order_id");
            entity.Property(x => x.WarehouseId).HasColumnName("warehouse_id");
            entity.Property(x => x.ReceivedDate).HasColumnName("received_date");
            entity.Property(x => x.ReceivedBy).HasColumnName("received_by");
            entity.Property(x => x.Note).HasColumnName("note").HasMaxLength(2000);
            entity.Property(x => x.IdempotencyKey).HasColumnName("idempotency_key").HasMaxLength(128).IsUnicode(false);
            entity.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("SYSUTCDATETIME()").IsRequired();
            entity.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("SYSUTCDATETIME()").IsRequired();
            entity.Property(x => x.ReversedAt).HasColumnName("reversed_at");
            entity.Property(x => x.ReversedBy).HasColumnName("reversed_by");
            entity.Property(x => x.ReverseReason).HasColumnName("reverse_reason").HasMaxLength(1000);
            entity.HasIndex(x => x.GrnNumber).IsUnique();
            entity.HasIndex(x => x.IdempotencyKey).IsUnique().HasFilter("[idempotency_key] IS NOT NULL");
            entity.HasIndex(x => new { x.PurchaseOrderId, x.ReceivedDate });
            entity.HasOne(x => x.PurchaseOrder).WithMany(x => x.GoodsReceipts).HasForeignKey(x => x.PurchaseOrderId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Warehouse).WithMany().HasForeignKey(x => x.WarehouseId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.ReceivedBy).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.ReversedBy).OnDelete(DeleteBehavior.Restrict);
            entity.HasMany(x => x.Items).WithOne(x => x.GoodsReceipt).HasForeignKey(x => x.GoodsReceiptId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<GoodsReceiptItem>(entity =>
        {
            entity.ToTable("goods_receipt_items", table =>
            {
                table.HasCheckConstraint("CK_goods_receipt_items_received_qty", "[received_qty] >= 0");
                table.HasCheckConstraint("CK_goods_receipt_items_rejected_qty", "[rejected_qty] >= 0");
                table.HasCheckConstraint("CK_goods_receipt_items_any_qty", "[received_qty] + [rejected_qty] > 0");
                table.HasCheckConstraint("CK_goods_receipt_items_unit_cost", "[unit_cost] >= 0");
            });
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.GoodsReceiptId).HasColumnName("goods_receipt_id");
            entity.Property(x => x.PurchaseOrderItemId).HasColumnName("purchase_order_item_id");
            entity.Property(x => x.ProductId).HasColumnName("product_id");
            entity.Property(x => x.ReceivedQty).HasColumnName("received_qty").HasPrecision(18, 3).IsRequired();
            entity.Property(x => x.RejectedQty).HasColumnName("rejected_qty").HasPrecision(18, 3).IsRequired();
            entity.Property(x => x.BatchNo).HasColumnName("batch_no").HasMaxLength(64);
            entity.Property(x => x.ExpiryDate).HasColumnName("expiry_date");
            entity.Property(x => x.UnitCost).HasColumnName("unit_cost").HasPrecision(18, 2).IsRequired();
            entity.HasIndex(x => new { x.GoodsReceiptId, x.PurchaseOrderItemId }).IsUnique();
            entity.HasOne(x => x.PurchaseOrderItem).WithMany().HasForeignKey(x => x.PurchaseOrderItemId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
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

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        EnsureMovementsAreAppendOnly();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        EnsureMovementsAreAppendOnly();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void EnsureMovementsAreAppendOnly()
    {
        var invalidLog = ChangeTracker.Entries<StockLog>()
            .FirstOrDefault(entry => entry.State is EntityState.Modified or EntityState.Deleted);
        if (invalidLog is not null)
            throw new InvalidOperationException("StockLog dəyişdirilə və silinə bilməz.");

        var invalid = ChangeTracker.Entries<StockMovement>()
            .FirstOrDefault(entry => entry.State is EntityState.Modified or EntityState.Deleted);
        if (invalid is not null)
            throw new InvalidOperationException("StockMovement append-only jurnaldır; dəyişdirilə və silinə bilməz. Düzəliş üçün REVERSAL yaradın.");

        var changedReceipt = ChangeTracker.Entries<GoodsReceipt>().FirstOrDefault(entry => entry.State == EntityState.Deleted ||
            (entry.State == EntityState.Modified && (entry.Property(x => x.ReversedAt).IsModified == false ||
             entry.Property(x => x.ReversedBy).IsModified == false || entry.Property(x => x.ReverseReason).IsModified == false ||
             entry.Properties.Any(p => p.IsModified && p.Metadata.Name is not (nameof(GoodsReceipt.ReversedAt) or nameof(GoodsReceipt.ReversedBy) or nameof(GoodsReceipt.ReverseReason) or nameof(GoodsReceipt.UpdatedAt))))));
        if (changedReceipt is not null || ChangeTracker.Entries<GoodsReceiptItem>().Any(entry => entry.State is EntityState.Modified or EntityState.Deleted))
            throw new InvalidOperationException("GoodsReceipt append-only sənəddir; dəyişdirilə və silinə bilməz.");
    }
}

internal static class StockMovementTypeCodes
{
    public static string ToStorage(StockMovementType type) => type switch
    {
        StockMovementType.PurchaseIn => "PURCHASE_IN",
        StockMovementType.OpeningBalance => "OPENING_BALANCE",
        StockMovementType.SaleOut => "SALE_OUT",
        StockMovementType.Adjustment => "ADJUSTMENT",
        StockMovementType.Transfer => "TRANSFER",
        StockMovementType.Return => "RETURN",
        StockMovementType.Reversal => "REVERSAL",
        _ => throw new ArgumentOutOfRangeException(nameof(type))
    };

    public static StockMovementType FromStorage(string value) => value switch
    {
        "PURCHASE_IN" => StockMovementType.PurchaseIn,
        "OPENING_BALANCE" => StockMovementType.OpeningBalance,
        "SALE_OUT" => StockMovementType.SaleOut,
        "ADJUSTMENT" => StockMovementType.Adjustment,
        "TRANSFER" => StockMovementType.Transfer,
        "RETURN" => StockMovementType.Return,
        "REVERSAL" => StockMovementType.Reversal,
        _ => throw new ArgumentOutOfRangeException(nameof(value))
    };
}
