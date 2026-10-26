using StockManagement.Domain.Enums;
using StockManagement.Domain.Services;

namespace StockManagement.Domain.Entities;

public sealed class PurchaseOrder : ISoftDeletable
{
    private PurchaseOrder() { }

    public PurchaseOrder(string poNumber, Guid supplierId, Guid warehouseId, DateOnly orderDate,
        DateOnly? expectedDate, string currency, string? note, Guid createdBy)
    {
        if (string.IsNullOrWhiteSpace(poNumber)) throw new ArgumentException("PO nömrəsi tələb olunur.", nameof(poNumber));
        if (supplierId == Guid.Empty || warehouseId == Guid.Empty || createdBy == Guid.Empty)
            throw new ArgumentException("Təchizatçı, anbar və yaradan seçilməlidir.");
        if (expectedDate < orderDate) throw new ArgumentException("Gözlənilən tarix sifariş tarixindən əvvəl ola bilməz.");
        Id = Guid.NewGuid();
        PoNumber = poNumber;
        SupplierId = supplierId;
        WarehouseId = warehouseId;
        OrderDate = orderDate;
        ExpectedDate = expectedDate;
        Currency = string.IsNullOrWhiteSpace(currency) ? "AZN" : currency.Trim().ToUpperInvariant();
        Note = note?.Trim();
        CreatedBy = createdBy;
        CreatedAt = UpdatedAt = DateTime.UtcNow;
        Status = PurchaseOrderStatus.Draft;
        Items = new List<PurchaseOrderItem>();
    }

    public Guid Id { get; private set; }
    public string PoNumber { get; private set; } = string.Empty;
    public Guid SupplierId { get; private set; }
    public Supplier Supplier { get; private set; } = null!;
    public Guid WarehouseId { get; private set; }
    public Warehouse Warehouse { get; private set; } = null!;
    public PurchaseOrderStatus Status { get; private set; }
    public DateOnly OrderDate { get; private set; }
    public DateOnly? ExpectedDate { get; private set; }
    public string Currency { get; private set; } = "AZN";
    public decimal TotalAmount { get; private set; }
    public string? Note { get; private set; }
    public Guid CreatedBy { get; private set; }
    public Guid? ApprovedBy { get; private set; }
    public DateTime? ApprovedAt { get; private set; }
    public Guid? CancelledBy { get; private set; }
    public DateTime? CancelledAt { get; private set; }
    public string? CancelReason { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public bool IsDeleted { get; private set; }
    public DateTime? DeletedAt { get; private set; }
    public ICollection<PurchaseOrderItem> Items { get; private set; } = new List<PurchaseOrderItem>();
    public ICollection<GoodsReceipt> GoodsReceipts { get; private set; } = new List<GoodsReceipt>();

    public void UpdateDraft(Guid supplierId, Guid warehouseId, DateOnly orderDate, DateOnly? expectedDate,
        string currency, string? note)
    {
        PurchaseOrderStateMachine.EnsureDraftEditable(Status);
        if (expectedDate < orderDate) throw new ArgumentException("Gözlənilən tarix sifariş tarixindən əvvəl ola bilməz.");
        SupplierId = supplierId;
        WarehouseId = warehouseId;
        OrderDate = orderDate;
        ExpectedDate = expectedDate;
        Currency = string.IsNullOrWhiteSpace(currency) ? "AZN" : currency.Trim().ToUpperInvariant();
        Note = note?.Trim();
        UpdatedAt = DateTime.UtcNow;
    }

    public void ReplaceItems(IEnumerable<(Guid ProductId, decimal Quantity, decimal UnitPrice)> lines)
    {
        PurchaseOrderStateMachine.EnsureDraftEditable(Status);
        var values = lines.ToArray();
        if (values.Length == 0) throw new ArgumentException("PO-da ən azı bir sətir olmalıdır.");
        if (values.Any(x => x.ProductId == Guid.Empty || x.Quantity <= 0 || x.UnitPrice < 0))
            throw new ArgumentException("Məhsul, müsbət miqdar və sıfırdan kiçik olmayan qiymət tələb olunur.");
        if (values.Select(x => x.ProductId).Distinct().Count() != values.Length)
            throw new ArgumentException("Eyni məhsul PO-da təkrar ola bilməz.");
        Items.Clear();
        foreach (var line in values) Items.Add(new PurchaseOrderItem(Id, line.ProductId, line.Quantity, line.UnitPrice));
        RecalculateTotal();
        UpdatedAt = DateTime.UtcNow;
    }

    public void RecalculateTotal()
    {
        foreach (var item in Items) item.RecalculateLineTotal();
        TotalAmount = Items.Sum(x => x.LineTotal);
    }

    public void TransitionTo(PurchaseOrderStatus target, Guid actorId, string? cancelReason = null, bool approveOverride = false)
    {
        PurchaseOrderStateMachine.EnsureTransition(Status, target);
        if (target == PurchaseOrderStatus.Approved && CreatedBy == actorId && !approveOverride)
            throw new InvalidOperationException("PO-nu yaradan istifadəçi onu təsdiqləyə bilməz.");
        if (target == PurchaseOrderStatus.Cancelled)
        {
            if (Items.Any(x => x.ReceivedQty > 0)) throw new InvalidOperationException("Mal qəbul edilmiş PO ləğv edilə bilməz.");
            if (string.IsNullOrWhiteSpace(cancelReason)) throw new ArgumentException("Ləğv səbəbi məcburidir.", nameof(cancelReason));
            CancelledBy = actorId;
            CancelledAt = DateTime.UtcNow;
            CancelReason = cancelReason.Trim();
        }
        if (target == PurchaseOrderStatus.Approved)
        {
            ApprovedBy = actorId;
            ApprovedAt = DateTime.UtcNow;
        }
        Status = target;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkPartiallyReceived() => TransitionTo(PurchaseOrderStatus.PartiallyReceived, Guid.Empty);
    public void MarkReceived() => TransitionTo(PurchaseOrderStatus.Received, Guid.Empty);

    public void ReopenAfterReceiptReversal()
    {
        Status = PurchaseOrderStateMachine.ReopenAfterReceiptReversal(Status, Items.Any(x => x.ReceivedQty > 0));
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkDeleted()
    {
        if (Status != PurchaseOrderStatus.Draft) throw new InvalidOperationException("Yalnız draft PO silinə bilər.");
        IsDeleted = true;
        DeletedAt = DateTime.UtcNow;
    }
}
