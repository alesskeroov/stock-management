using StockManagement.Domain.Entities;

namespace StockManagement.Application.Products.Repositories;

public interface IInventoryRepository
{
    Task<IReadOnlyList<BatchInventoryItem>> GetActiveBatchesAsync(Guid productId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<WarehouseOption>> GetWarehousesAsync(CancellationToken cancellationToken = default);
    Task<InventoryScanResult?> FindByScanCodeAsync(string code, CancellationToken cancellationToken = default);
    Task<ProductBatch> ReceiveAsync(Guid productId, Guid warehouseId, decimal quantity,
        decimal purchasePrice, decimal salePrice, DateTime? entryDate, DateTime? expiryDate,
        Guid appUserId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BatchAllocation>> IssueFifoAsync(Guid productId, Guid warehouseId,
        decimal quantity, string referenceNo, Guid appUserId, CancellationToken cancellationToken = default);
}
