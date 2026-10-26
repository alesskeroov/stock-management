namespace StockManagement.Domain.Entities;

/// <summary>A posted, immutable goods receipt.</summary>
public sealed class GoodsReceipt
{
    private GoodsReceipt() { }

    public GoodsReceipt(string grnNumber, Guid purchaseOrderId, Guid warehouseId, DateOnly receivedDate,
        Guid receivedBy, string? note, string? idempotencyKey)
    {
        if (string.IsNullOrWhiteSpace(grnNumber)) throw new ArgumentException("GRN nömrəsi tələb olunur.", nameof(grnNumber));
        if (purchaseOrderId == Guid.Empty || warehouseId == Guid.Empty || receivedBy == Guid.Empty)
            throw new ArgumentException("PO, anbar və qəbul edən istifadəçi tələb olunur.");
        if (idempotencyKey?.Length > 128) throw new ArgumentException("Idempotency key ən çox 128 simvol ola bilər.", nameof(idempotencyKey));
        Id = Guid.NewGuid();
        GrnNumber = grnNumber;
        PurchaseOrderId = purchaseOrderId;
        WarehouseId = warehouseId;
        ReceivedDate = receivedDate;
        ReceivedBy = receivedBy;
        Note = note?.Trim();
        IdempotencyKey = string.IsNullOrWhiteSpace(idempotencyKey) ? null : idempotencyKey.Trim();
        CreatedAt = UpdatedAt = DateTime.UtcNow;
        Items = new List<GoodsReceiptItem>();
    }

    public Guid Id { get; private set; }
    public string GrnNumber { get; private set; } = string.Empty;
    public Guid PurchaseOrderId { get; private set; }
    public PurchaseOrder PurchaseOrder { get; private set; } = null!;
    public Guid WarehouseId { get; private set; }
    public Warehouse Warehouse { get; private set; } = null!;
    public DateOnly ReceivedDate { get; private set; }
    public Guid ReceivedBy { get; private set; }
    public string? Note { get; private set; }
    public string? IdempotencyKey { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public DateTime? ReversedAt { get; private set; }
    public Guid? ReversedBy { get; private set; }
    public string? ReverseReason { get; private set; }
    public ICollection<GoodsReceiptItem> Items { get; private set; } = new List<GoodsReceiptItem>();

    public void MarkReversed(Guid userId, string reason)
    {
        if (ReversedAt.HasValue) throw new InvalidOperationException("Bu GRN artıq geri qaytarılıb.");
        if (userId == Guid.Empty || string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Geri qaytarma istifadəçisi və səbəbi məcburidir.");
        if (reason.Trim().Length > 1000) throw new ArgumentException("Geri qaytarma səbəbi ən çox 1000 simvol ola bilər.");
        ReversedAt = DateTime.UtcNow;
        ReversedBy = userId;
        ReverseReason = reason.Trim();
        UpdatedAt = DateTime.UtcNow;
    }
}
