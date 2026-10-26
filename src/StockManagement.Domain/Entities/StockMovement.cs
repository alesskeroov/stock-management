using StockManagement.Domain.Enums;

namespace StockManagement.Domain.Entities;

/// <summary>An immutable stock ledger entry. Corrections must be recorded as a new reversal movement.</summary>
public sealed class StockMovement
{
    private StockMovement() { }

    public StockMovement(Guid productId, Guid warehouseId, StockMovementType type, decimal quantity,
        decimal unitCost, string referenceType, Guid? referenceId, Guid createdBy, DateTime? createdAt = null)
    {
        if (productId == Guid.Empty) throw new ArgumentException("Məhsul seçilməlidir.", nameof(productId));
        if (warehouseId == Guid.Empty) throw new ArgumentException("Anbar seçilməlidir.", nameof(warehouseId));
        if (createdBy == Guid.Empty) throw new ArgumentException("Əməliyyatı edən istifadəçi tələb olunur.", nameof(createdBy));
        if (quantity == 0 && type != StockMovementType.OpeningBalance)
            throw new ArgumentOutOfRangeException(nameof(quantity), "Sıfır miqdar yalnız açılış qalığı üçündür.");
        if (unitCost < 0) throw new ArgumentOutOfRangeException(nameof(unitCost));
        if (string.IsNullOrWhiteSpace(referenceType)) throw new ArgumentException("İstinad növü tələb olunur.", nameof(referenceType));

        Id = Guid.NewGuid();
        ProductId = productId;
        WarehouseId = warehouseId;
        Type = type;
        Quantity = quantity;
        UnitCost = unitCost;
        ReferenceType = referenceType.Trim();
        ReferenceId = referenceId;
        CreatedBy = createdBy;
        CreatedAt = createdAt ?? DateTime.UtcNow;
    }

    public Guid Id { get; private set; }
    public Guid ProductId { get; private set; }
    public Guid WarehouseId { get; private set; }
    public StockMovementType Type { get; private set; }
    public decimal Quantity { get; private set; }
    public decimal UnitCost { get; private set; }
    public string ReferenceType { get; private set; } = string.Empty;
    public Guid? ReferenceId { get; private set; }
    public Guid CreatedBy { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public Product Product { get; private set; } = null!;
    public Warehouse Warehouse { get; private set; } = null!;
}
