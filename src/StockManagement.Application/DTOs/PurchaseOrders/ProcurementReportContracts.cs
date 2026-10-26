namespace StockManagement.Application.DTOs.PurchaseOrders;

public sealed record ProcurementReportPage<T>(IReadOnlyCollection<T> Items, int CurrentPage, int PageSize,
    int TotalCount, int TotalPages, bool HasNext, bool HasPrevious);
public sealed record OpenPurchaseOrderReport(Guid Id, string PoNumber, string SupplierName, string WarehouseName,
    string Status, DateOnly OrderDate, DateOnly? ExpectedDate, decimal TotalAmount);
public sealed record OverduePurchaseOrderReport(Guid Id, string PoNumber, string SupplierName,
    DateOnly ExpectedDate, int DaysOverdue, decimal TotalAmount);
public sealed record PurchaseOrderReceiptComparison(Guid PurchaseOrderId, string PoNumber, string SupplierName,
    Guid ProductId, string ProductName, decimal OrderedQty, decimal ReceivedQty, decimal RejectedQty);
public sealed record SupplierReceiptAmount(Guid SupplierId, string SupplierName, string Currency,
    decimal ReceivedAmount, int ReceiptCount);
