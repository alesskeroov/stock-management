using StockManagement.Application.Products.Repositories;

namespace StockManagement.Application.Products;

public sealed class StockService(IStockMovementRepository repository)
{
    public Task<decimal> GetStock(Guid productId, Guid warehouseId, CancellationToken cancellationToken = default) =>
        repository.GetStockAsync(productId, warehouseId, cancellationToken);
}
