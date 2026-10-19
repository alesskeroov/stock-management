namespace StockManagement.Application.Products;

public sealed record ReceiveStockRequest(Guid ProductId, Guid WarehouseId, decimal Quantity,
    decimal PurchasePrice, decimal SalePrice, DateTime? EntryDate = null, DateTime? ExpiryDate = null);
public sealed record BatchAllocation(string BatchNumber, decimal Quantity, decimal PurchasePrice);
public sealed record StockReceiptResult(string BatchNumber, decimal Quantity, DateTime EntryDate, string QrCodeSvg);
public sealed record InventoryScanResult(string Code, string CodeType, Guid ProductId, string ProductName,
    string Sku, string Barcode, string? BatchNumber, string? WarehouseName, decimal? RemainingQuantity,
    DateTime? EntryDate, DateTime? ExpiryDate);
public sealed record StockIssueResult(IReadOnlyList<BatchAllocation> Allocations);
public sealed record BatchInventoryItem(string batchNumber, DateTime entryDate, DateTime? expiryDate,
    decimal remainingQuantity, decimal purchasePrice);
public sealed record WarehouseOption(Guid id, string name);
