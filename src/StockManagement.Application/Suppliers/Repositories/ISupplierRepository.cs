using StockManagement.Domain.Entities;

namespace StockManagement.Application.Suppliers.Repositories;

public interface ISupplierRepository
{
    Task<(IReadOnlyCollection<Supplier> Items, int TotalCount)> GetPageAsync(int page, int size, string? search,
        bool? isActive, CancellationToken cancellationToken = default);
    Task<Supplier?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> HasPurchaseOrdersAsync(Guid supplierId, CancellationToken cancellationToken = default);
    Task AddAsync(Supplier supplier, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
