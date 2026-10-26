namespace StockManagement.Application.DTOs.GoodsReceipts;

public sealed record GoodsReceiptItemRequest(Guid PurchaseOrderItemId, decimal ReceivedQty, decimal RejectedQty,
    string? BatchNo, DateOnly? ExpiryDate, decimal UnitCost);

public sealed record CreateGoodsReceiptRequest(Guid PurchaseOrderId, DateOnly ReceivedDate, string? Note,
    string? IdempotencyKey, IReadOnlyCollection<GoodsReceiptItemRequest>? Items);
public sealed record ReverseGoodsReceiptRequest(string? Reason);

public sealed record GoodsReceiptItemDto(Guid Id, Guid PurchaseOrderItemId, Guid ProductId, string ProductName,
    decimal ReceivedQty, decimal RejectedQty, string? BatchNo, DateOnly? ExpiryDate, decimal UnitCost);

public sealed record GoodsReceiptDto(Guid Id, string GrnNumber, Guid PurchaseOrderId, string PoNumber,
    Guid WarehouseId, string WarehouseName, DateOnly ReceivedDate, Guid ReceivedBy, string ReceivedByName, string? Note,
    string? IdempotencyKey, DateTime CreatedAt, IReadOnlyCollection<GoodsReceiptItemDto> Items,
    DateTime? ReversedAt, Guid? ReversedBy, string? ReverseReason);

public sealed record GoodsReceiptPage(IReadOnlyCollection<GoodsReceiptDto> Items, int CurrentPage,
    int PageSize, int TotalCount, int TotalPages, bool HasNext, bool HasPrevious);
