using StockManagement.Domain.Enums;

namespace StockManagement.Domain.Services;

public static class PurchaseOrderStateMachine
{
    public static PurchaseOrderStatus ReopenAfterReceiptReversal(PurchaseOrderStatus current, bool hasAcceptedQuantity)
    {
        var target = hasAcceptedQuantity ? PurchaseOrderStatus.PartiallyReceived : PurchaseOrderStatus.Approved;
        var allowed = current switch
        {
            PurchaseOrderStatus.Received => target is PurchaseOrderStatus.PartiallyReceived or PurchaseOrderStatus.Approved,
            PurchaseOrderStatus.PartiallyReceived => target == PurchaseOrderStatus.PartiallyReceived || target == PurchaseOrderStatus.Approved,
            _ => false
        };
        if (!allowed) throw new InvalidOperationException($"GRN reversal PO status keçidi qadağandır: {current} -> {target}.");
        return target;
    }

    public static void EnsureDraftEditable(PurchaseOrderStatus status)
    {
        if (status != PurchaseOrderStatus.Draft)
            throw new InvalidOperationException("Yalnız draft PO redaktə edilə bilər; dəyişiklik üçün ləğv edib yeni PO yaradın.");
    }

    public static void EnsureTransition(PurchaseOrderStatus current, PurchaseOrderStatus target)
    {
        var allowed = (current, target) switch
        {
            (PurchaseOrderStatus.Draft, PurchaseOrderStatus.PendingApproval) => true,
            (PurchaseOrderStatus.PendingApproval, PurchaseOrderStatus.Approved) => true,
            (PurchaseOrderStatus.Approved, PurchaseOrderStatus.PartiallyReceived) => true,
            (PurchaseOrderStatus.PartiallyReceived, PurchaseOrderStatus.Received) => true,
            (PurchaseOrderStatus.Draft or PurchaseOrderStatus.PendingApproval or PurchaseOrderStatus.Approved,
                PurchaseOrderStatus.Cancelled) => true,
            _ => false
        };
        if (!allowed) throw new InvalidOperationException($"PO status keçidi qadağandır: {current} -> {target}.");
    }
}
