using StockManagement.Domain.Enums;

namespace StockManagement.Domain.Entities;

public sealed class StockTransaction
{
    private StockTransaction() { }

    public StockTransaction(Guid productId, Guid warehouseId, StockTransactionType transactionType,
        decimal quantity, string referenceNo, decimal unitPriceAtTime, string description, Guid appUserId)
    {
        if (productId == Guid.Empty) throw new ArgumentException("Məhsul seçilməlidir.", nameof(productId));
        if (warehouseId == Guid.Empty) throw new ArgumentException("Anbar seçilməlidir.", nameof(warehouseId));
        if (appUserId == Guid.Empty) throw new ArgumentException("Əməliyyatı edən istifadəçi tələb olunur.", nameof(appUserId));
        if (quantity == 0) throw new ArgumentOutOfRangeException(nameof(quantity));
        if (string.IsNullOrWhiteSpace(referenceNo)) throw new ArgumentException("Əməliyyat nömrəsi tələb olunur.", nameof(referenceNo));

        Id = Guid.NewGuid();
        ProductId = productId;
        WarehouseId = warehouseId;
        TransactionType = transactionType;
        Quantity = quantity;
        ReferenceNo = referenceNo.Trim();
        UnitPriceAtTime = unitPriceAtTime;
        Description = description?.Trim() ?? string.Empty;
        AppUserId = appUserId;
        CreatedAt = DateTime.UtcNow;
    }

    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid ProductId { get; private set; }
    public Guid WarehouseId { get; private set; }
    public StockTransactionType TransactionType { get; private set; }
    // Positive values represent stock coming in; negative values represent stock going out.
    public decimal Quantity { get; private set; }
    public string ReferenceNo { get; private set; } = string.Empty;
    public decimal UnitPriceAtTime { get; private set; }
    public string Description { get; private set; } = string.Empty;
    // AppUserId is reserved for the future authentication/user entity.
    public Guid AppUserId { get; private set; }
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;
    public Product Product { get; private set; } = null!;
    public Warehouse Warehouse { get; private set; } = null!;
}
