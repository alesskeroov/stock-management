namespace StockManagement.Domain.Enums;

public static class PurchaseOrderStatusCodes
{
    public static string ToStorage(PurchaseOrderStatus status) => status switch
    {
        PurchaseOrderStatus.Draft => "draft",
        PurchaseOrderStatus.PendingApproval => "pending_approval",
        PurchaseOrderStatus.Approved => "approved",
        PurchaseOrderStatus.PartiallyReceived => "partially_received",
        PurchaseOrderStatus.Received => "received",
        PurchaseOrderStatus.Cancelled => "cancelled",
        _ => throw new ArgumentOutOfRangeException(nameof(status))
    };

    public static PurchaseOrderStatus FromStorage(string value) => value.ToLowerInvariant() switch
    {
        "draft" => PurchaseOrderStatus.Draft,
        "pending_approval" => PurchaseOrderStatus.PendingApproval,
        "approved" => PurchaseOrderStatus.Approved,
        "partially_received" => PurchaseOrderStatus.PartiallyReceived,
        "received" => PurchaseOrderStatus.Received,
        "cancelled" => PurchaseOrderStatus.Cancelled,
        _ => throw new ArgumentOutOfRangeException(nameof(value))
    };
}
