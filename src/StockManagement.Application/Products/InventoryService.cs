using StockManagement.Application.Products.Repositories;
using StockManagement.Application.Abstractions;

namespace StockManagement.Application.Products;

public sealed class InventoryService(IInventoryRepository repository, IQrCodeGenerator qrCodeGenerator)
{
    public Task<IReadOnlyList<BatchInventoryItem>> GetActiveBatchesAsync(Guid productId, CancellationToken cancellationToken = default) =>
        repository.GetActiveBatchesAsync(productId, cancellationToken);

    public Task<IReadOnlyList<WarehouseOption>> GetWarehousesAsync(CancellationToken cancellationToken = default) =>
        repository.GetWarehousesAsync(cancellationToken);

    public Task<InventoryScanResult?> FindByScanCodeAsync(string code, CancellationToken cancellationToken = default) =>
        string.IsNullOrWhiteSpace(code) ? Task.FromResult<InventoryScanResult?>(null) :
            repository.FindByScanCodeAsync(code.Trim(), cancellationToken);

    public async Task<StockReceiptResult> ReceiveAsync(ReceiveStockRequest request, Guid appUserId,
        CancellationToken cancellationToken = default)
    {
        if (request.ProductId == Guid.Empty) throw new ArgumentException("Məhsul seçilməlidir.", nameof(request.ProductId));
        if (request.WarehouseId == Guid.Empty) throw new ArgumentException("Anbar seçilməlidir.", nameof(request.WarehouseId));
        if (request.Quantity < 1 || decimal.Truncate(request.Quantity) != request.Quantity)
            throw new ArgumentOutOfRangeException(nameof(request.Quantity), "Miqdar ən azı 1 olan tam ədəd olmalıdır.");
        if (request.PurchasePrice < 0 || request.SalePrice < 0) throw new ArgumentOutOfRangeException(nameof(request.PurchasePrice), "Qiymət mənfi ola bilməz.");
        if (request.ExpiryDate.HasValue && request.ExpiryDate.Value < (request.EntryDate ?? DateTime.UtcNow))
            throw new ArgumentException("Son istifadə tarixi giriş tarixindən əvvəl ola bilməz.", nameof(request.ExpiryDate));
        var batch = await repository.ReceiveAsync(request.ProductId, request.WarehouseId, request.Quantity,
            request.PurchasePrice, request.SalePrice, request.EntryDate, request.ExpiryDate, appUserId, cancellationToken);
        return new StockReceiptResult(batch.BatchNumber, batch.InitialQuantity, batch.EntryDate,
            qrCodeGenerator.ToSvg(batch.BatchNumber));
    }

    public async Task<StockIssueResult> IssueFifoAsync(Guid productId, Guid warehouseId, decimal quantity,
        string referenceNo, Guid appUserId, CancellationToken cancellationToken = default)
    {
        if (quantity <= 0) throw new ArgumentOutOfRangeException(nameof(quantity), "Miqdar sıfırdan böyük olmalıdır.");
        if (string.IsNullOrWhiteSpace(referenceNo)) throw new ArgumentException("Əməliyyat nömrəsi tələb olunur.", nameof(referenceNo));
        return new StockIssueResult(await repository.IssueFifoAsync(productId, warehouseId, quantity,
            referenceNo, appUserId, cancellationToken));
    }
}
