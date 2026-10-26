namespace StockManagement.Domain.Entities;

public sealed class PurchaseOrderItem
{
    private PurchaseOrderItem() { }

    internal PurchaseOrderItem(Guid purchaseOrderId, Guid productId, decimal orderedQty, decimal unitPrice)
    {
        if (productId == Guid.Empty || orderedQty <= 0 || unitPrice < 0)
            throw new ArgumentException("Məhsul, müsbət miqdar və sıfırdan kiçik olmayan qiymət tələb olunur.");
        Id = Guid.NewGuid();
        PurchaseOrderId = purchaseOrderId;
        ProductId = productId;
        OrderedQty = orderedQty;
        UnitPrice = unitPrice;
        RecalculateLineTotal();
    }

    public Guid Id { get; private set; }
    public Guid PurchaseOrderId { get; private set; }
    public PurchaseOrder PurchaseOrder { get; private set; } = null!;
    public Guid ProductId { get; private set; }
    public Product Product { get; private set; } = null!;
    public decimal OrderedQty { get; private set; }
    public decimal ReceivedQty { get; private set; }
    public decimal UnitPrice { get; private set; }
    public decimal LineTotal { get; private set; }

    internal void RecalculateLineTotal() => LineTotal = decimal.Round(OrderedQty * UnitPrice, 2, MidpointRounding.AwayFromZero);

    public void RecordReceived(decimal quantity)
    {
        if (quantity <= 0) throw new ArgumentOutOfRangeException(nameof(quantity));
        ReceivedQty += quantity;
    }

    public void ReverseReceived(decimal quantity)
    {
        if (quantity <= 0 || quantity > ReceivedQty) throw new InvalidOperationException("Geri qaytarma miqdarı qəbul edilən miqdardan çoxdur.");
        ReceivedQty -= quantity;
    }
}
