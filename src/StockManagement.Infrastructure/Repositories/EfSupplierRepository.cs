using Microsoft.EntityFrameworkCore;
using StockManagement.Application.Suppliers.Repositories;
using StockManagement.Domain.Entities;
using StockManagement.Infrastructure.Persistence;

namespace StockManagement.Infrastructure.Repositories;

public sealed class EfSupplierRepository(StockManagementDbContext db) : ISupplierRepository
{
    public async Task<(IReadOnlyCollection<Supplier> Items, int TotalCount)> GetPageAsync(int page, int size,
        string? search, bool? isActive, CancellationToken cancellationToken = default)
    {
        var query = db.Suppliers.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(x => x.Name.Contains(term) || x.Phone.Contains(term) || x.Email.Contains(term) || x.TaxId.Contains(term));
        }
        if (isActive.HasValue) query = query.Where(x => x.IsActive == isActive.Value);
        query = query.OrderBy(x => x.Name).ThenBy(x => x.Id);
        var count = await query.CountAsync(cancellationToken);
        var lastPage = (int)Math.Ceiling(count / (double)size);
        page = lastPage == 0 ? 1 : Math.Clamp(page, 1, lastPage);
        return (await query.Skip((page - 1) * size).Take(size).ToArrayAsync(cancellationToken), count);
    }

    public Task<Supplier?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        db.Suppliers.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<bool> HasPurchaseOrdersAsync(Guid supplierId, CancellationToken cancellationToken = default) =>
        db.PurchaseOrders.IgnoreQueryFilters().AnyAsync(x => x.SupplierId == supplierId, cancellationToken);

    public async Task AddAsync(Supplier supplier, CancellationToken cancellationToken = default)
    {
        db.Suppliers.Add(supplier);
        await db.SaveChangesAsync(cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) => db.SaveChangesAsync(cancellationToken);
}
