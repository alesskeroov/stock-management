namespace StockManagement.Domain.Entities;

public sealed class GoodsReceiptItem
{
    private GoodsReceiptItem() { }

    public GoodsReceiptItem(Guid goodsReceiptId, Guid purchaseOrderItemId, Guid productId,
        decimal receivedQty, decimal rejectedQty, string? batchNo, DateOnly? expiryDate, decimal unitCost)
    {
        if (goodsReceiptId == Guid.Empty || purchaseOrderItemId == Guid.Empty || productId == Guid.Empty)
            throw new ArgumentException("GRN və PO sətir istinadları tələb olunur.");
        if (receivedQty < 0 || rejectedQty < 0 || receivedQty + rejectedQty <= 0)
            throw new ArgumentOutOfRangeException(nameof(receivedQty), "Qəbul və ya qüsurlu miqdar müsbət olmalıdır; heç biri mənfi ola bilməz.");
        if (unitCost < 0) throw new ArgumentOutOfRangeException(nameof(unitCost));
        Id = Guid.NewGuid();
        GoodsReceiptId = goodsReceiptId;
        PurchaseOrderItemId = purchaseOrderItemId;
        ProductId = productId;
        ReceivedQty = receivedQty;
        RejectedQty = rejectedQty;
        BatchNo = string.IsNullOrWhiteSpace(batchNo) ? null : batchNo.Trim();
        ExpiryDate = expiryDate;
        UnitCost = unitCost;
    }

    public Guid Id { get; private set; }
    public Guid GoodsReceiptId { get; private set; }
    public GoodsReceipt GoodsReceipt { get; private set; } = null!;
    public Guid PurchaseOrderItemId { get; private set; }
    public PurchaseOrderItem PurchaseOrderItem { get; private set; } = null!;
    public Guid ProductId { get; private set; }
    public Product Product { get; private set; } = null!;
    public decimal ReceivedQty { get; private set; }
    public decimal RejectedQty { get; private set; }
    public string? BatchNo { get; private set; }
    public DateOnly? ExpiryDate { get; private set; }
    public decimal UnitCost { get; private set; }
}
