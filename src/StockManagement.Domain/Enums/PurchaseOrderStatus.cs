namespace StockManagement.Domain.Enums;

public enum PurchaseOrderStatus
{
    Draft,
    PendingApproval,
    Approved,
    PartiallyReceived,
    Received,
    Cancelled
}
