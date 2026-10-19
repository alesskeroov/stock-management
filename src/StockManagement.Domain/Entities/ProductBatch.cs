namespace StockManagement.Domain.Entities;

/// <summary>A single, traceable receipt of a product into a warehouse.</summary>
public sealed class ProductBatch : ISoftDeletable
{
    private ProductBatch() { }

    public ProductBatch(Guid productId, Guid warehouseId, string batchNumber, decimal quantity,
        DateTime? entryDate, DateTime? expiryDate, decimal purchasePrice, decimal salePrice)
    {
        if (productId == Guid.Empty) throw new ArgumentException("Məhsul seçilməlidir.", nameof(productId));
        if (warehouseId == Guid.Empty) throw new ArgumentException("Anbar seçilməlidir.", nameof(warehouseId));
        if (quantity < 1 || decimal.Truncate(quantity) != quantity)
            throw new ArgumentOutOfRangeException(nameof(quantity), "Miqdar ən azı 1 olan tam ədəd olmalıdır.");
        if (purchasePrice < 0 || salePrice < 0) throw new ArgumentOutOfRangeException(nameof(purchasePrice), "Qiymət mənfi ola bilməz.");
        if (string.IsNullOrWhiteSpace(batchNumber)) throw new ArgumentException("Partiya nömrəsi tələb olunur.", nameof(batchNumber));
        var receivedAt = entryDate ?? DateTime.UtcNow;
        if (expiryDate.HasValue && expiryDate.Value < receivedAt) throw new ArgumentException("Son istifadə tarixi giriş tarixindən əvvəl ola bilməz.", nameof(expiryDate));
        Id = Guid.NewGuid(); ProductId = productId; WarehouseId = warehouseId;
        BatchNumber = batchNumber.Trim(); InitialQuantity = quantity; RemainingQuantity = quantity;
        EntryDate = receivedAt.Kind == DateTimeKind.Utc ? receivedAt : receivedAt.ToUniversalTime();
        ExpiryDate = expiryDate?.ToUniversalTime(); PurchasePrice = purchasePrice; SalePrice = salePrice;
    }

    public Guid Id { get; private set; }
    public Guid ProductId { get; private set; }
    public Guid WarehouseId { get; private set; }
    public string BatchNumber { get; private set; } = string.Empty;
    public decimal InitialQuantity { get; private set; }
    public decimal RemainingQuantity { get; private set; }
    public DateTime EntryDate { get; private set; }
    public DateTime? ExpiryDate { get; private set; }
    public bool IsDeleted { get; private set; }
    public DateTime? DeletedAt { get; private set; }
    public decimal PurchasePrice { get; private set; }
    public decimal SalePrice { get; private set; }
    public Product Product { get; private set; } = null!;
    public Warehouse Warehouse { get; private set; } = null!;

    public void MarkDeleted(DateTime? deletedAt = null)
    {
        if (IsDeleted) return;
        IsDeleted = true;
        DeletedAt = deletedAt ?? DateTime.UtcNow;
    }

    public decimal Consume(decimal quantity)
    {
        if (quantity <= 0 || quantity > RemainingQuantity) throw new ArgumentOutOfRangeException(nameof(quantity));
        RemainingQuantity -= quantity;
        return quantity;
    }
}
