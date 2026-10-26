using StockManagement.Application.Abstractions.Repositories;
using StockManagement.Domain.Entities;

namespace StockManagement.Application.Products.Repositories;

public interface IProductRepository : IGenericRepository<Product>
{
    Task<(IReadOnlyCollection<Product> Items, int TotalCount)> GetPageAsync(int page, int size,
        CancellationToken cancellationToken = default);
    Task<Product?> GetBySkuAsync(string sku, CancellationToken cancellationToken = default);
    Task<Product?> GetByBarcodeAsync(string barcode, CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<Guid, decimal>> GetStockQuantitiesAsync(IReadOnlyCollection<Guid>? productIds = null,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<Guid, (DateTime? CreatedAt, DateTime? UpdatedAt)>> GetProductActivityDatesAsync(
        IReadOnlyCollection<Guid>? productIds = null, CancellationToken cancellationToken = default);
    Task<decimal> GetStockQuantityAsync(Guid productId, CancellationToken cancellationToken = default);
    Task<bool> SetStockQuantityAsync(Guid productId, decimal quantity, Guid appUserId,
        CancellationToken cancellationToken = default);
}
