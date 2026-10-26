namespace StockManagement.Application.Products.Repositories;

public interface IStockMovementRepository
{
    Task<decimal> GetStockAsync(Guid productId, Guid warehouseId, CancellationToken cancellationToken = default);
}
