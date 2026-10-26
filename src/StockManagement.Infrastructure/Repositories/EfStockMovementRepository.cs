using Microsoft.EntityFrameworkCore;
using StockManagement.Application.Products.Repositories;
using StockManagement.Infrastructure.Persistence;

namespace StockManagement.Infrastructure.Repositories;

public sealed class EfStockMovementRepository(StockManagementDbContext db) : IStockMovementRepository
{
    public async Task<decimal> GetStockAsync(Guid productId, Guid warehouseId, CancellationToken cancellationToken = default) =>
        await db.StockMovements.AsNoTracking()
            .Where(x => x.ProductId == productId && x.WarehouseId == warehouseId)
            .SumAsync(x => (decimal?)x.Quantity, cancellationToken) ?? 0m;
}
