using StockManagement.Application.DTOs.GoodsReceipts;

namespace StockManagement.Application.DTOs.PurchaseOrders;

public sealed record PurchaseOrderLineRequest(Guid ProductId, decimal OrderedQty, decimal UnitPrice, decimal? LineTotal = null);

public sealed record SavePurchaseOrderRequest(Guid SupplierId, Guid WarehouseId, DateOnly OrderDate,
    DateOnly? ExpectedDate, string? Currency, string? Note, IReadOnlyCollection<PurchaseOrderLineRequest>? Items,
    decimal? TotalAmount = null);

public sealed record PurchaseOrderItemDto(Guid Id, Guid ProductId, string ProductName, string Sku,
    decimal OrderedQty, decimal ReceivedQty, decimal RemainingQty, decimal UnitPrice, decimal LineTotal);

public sealed record PurchaseOrderDto(Guid Id, string PoNumber, Guid SupplierId, string SupplierName,
    string SupplierPhone, string SupplierEmail, string SupplierAddress, string SupplierTaxId,
    Guid WarehouseId, string WarehouseName, string Status, DateOnly OrderDate, DateOnly? ExpectedDate,
    string Currency, decimal TotalAmount, string? Note, Guid CreatedBy, Guid? ApprovedBy,
    DateTime? ApprovedAt, Guid? CancelledBy, DateTime? CancelledAt, string? CancelReason,
    DateTime CreatedAt, DateTime UpdatedAt, IReadOnlyCollection<PurchaseOrderItemDto> Items,
    IReadOnlyCollection<GoodsReceiptSummaryDto> GoodsReceipts);

public sealed record PurchaseOrderPage(IReadOnlyCollection<PurchaseOrderDto> Items, int CurrentPage,
    int PageSize, int TotalCount, int TotalPages, bool HasNext, bool HasPrevious);

public sealed record CancelPurchaseOrderRequest(string? Reason);
public sealed record PurchaseOrderOptions(IReadOnlyCollection<PurchaseOrderOption> Suppliers,
    IReadOnlyCollection<PurchaseOrderOption> Warehouses, IReadOnlyCollection<PurchaseOrderOption> Products);
public sealed record PurchaseOrderOption(Guid Id, string Name, string? Sku = null);
public sealed record GoodsReceiptSummaryDto(Guid Id, string GrnNumber, DateOnly ReceivedDate,
    decimal AcceptedQty, decimal RejectedQty);
