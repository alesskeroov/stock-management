namespace StockManagement.Domain.Entities;

public sealed class Stock
{
    private Stock() { }

    public Stock(Guid productId, Guid warehouseId, decimal quantity, decimal criticalLowLevel = 5)
    {
        if (productId == Guid.Empty) throw new ArgumentException("Məhsul seçilməlidir.", nameof(productId));
        if (warehouseId == Guid.Empty) throw new ArgumentException("Anbar seçilməlidir.", nameof(warehouseId));
        if (quantity < 0) throw new ArgumentOutOfRangeException(nameof(quantity));
        if (criticalLowLevel < 0) throw new ArgumentOutOfRangeException(nameof(criticalLowLevel));

        Id = Guid.NewGuid();
        ProductId = productId;
        WarehouseId = warehouseId;
        Quantity = quantity;
        CriticalLowLevel = criticalLowLevel;
        LastUpdatedDate = DateTime.UtcNow;
    }

    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid ProductId { get; private set; }
    public Guid WarehouseId { get; private set; }
    public decimal Quantity { get; private set; }
    public decimal CriticalLowLevel { get; private set; }
    public DateTime LastUpdatedDate { get; private set; } = DateTime.UtcNow;
    public Product Product { get; private set; } = null!;
    public Warehouse Warehouse { get; private set; } = null!;

    public void SetQuantity(decimal quantity)
    {
        if (quantity < 0) throw new ArgumentOutOfRangeException(nameof(quantity));
        Quantity = quantity;
        LastUpdatedDate = DateTime.UtcNow;
    }
}
