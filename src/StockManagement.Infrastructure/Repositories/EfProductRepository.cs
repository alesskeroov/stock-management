using Microsoft.EntityFrameworkCore;
using StockManagement.Application.Products.Repositories;
using StockManagement.Domain.Entities;
using StockManagement.Domain.Enums;
using StockManagement.Infrastructure.Persistence;
using StockManagement.Infrastructure.Services;

namespace StockManagement.Infrastructure.Repositories;

public sealed class EfProductRepository(StockManagementDbContext dbContext, StockLogService stockLogs)
    : EfRepository<Product>(dbContext), IProductRepository
{
    public EfProductRepository(StockManagementDbContext dbContext) : this(dbContext, new StockLogService(dbContext)) { }

    public async Task<(IReadOnlyCollection<Product> Items, int TotalCount)> GetPageAsync(int page, int size,
        CancellationToken cancellationToken = default)
    {
        var query = DbContext.Products.AsNoTracking();
        var totalCount = await query.CountAsync(cancellationToken);
        var totalPages = (int)Math.Ceiling(totalCount / (double)size);
        page = totalPages == 0 ? 1 : Math.Clamp(page, 1, totalPages);
        var items = await query.OrderBy(x => x.Name).ThenBy(x => x.Id)
            .Skip((page - 1) * size).Take(size).ToArrayAsync(cancellationToken);
        return (items, totalCount);
    }

    public Task<Product?> GetBySkuAsync(string sku, CancellationToken cancellationToken = default) =>
        DbContext.Products.AsNoTracking().SingleOrDefaultAsync(x => x.Sku == sku.Trim(), cancellationToken);

    public Task<Product?> GetByBarcodeAsync(string barcode, CancellationToken cancellationToken = default) =>
        DbContext.Products.AsNoTracking().SingleOrDefaultAsync(x => x.Barcode == barcode.Trim() && x.Barcode != string.Empty, cancellationToken);

    public async Task<IReadOnlyDictionary<Guid, decimal>> GetStockQuantitiesAsync(IReadOnlyCollection<Guid>? productIds = null,
        CancellationToken cancellationToken = default)
    {
        var query = DbContext.Stocks.AsNoTracking();
        if (productIds is not null) query = query.Where(x => productIds.Contains(x.ProductId));
        return await query
            .Join(DbContext.Products.AsNoTracking(), stock => stock.ProductId, product => product.Id, (stock, product) => stock)
            .GroupBy(x => x.ProductId)
            .Select(group => new { ProductId = group.Key, Quantity = group.Sum(x => x.Quantity) })
            .ToDictionaryAsync(x => x.ProductId, x => x.Quantity, cancellationToken);
    }

    public async Task<IReadOnlyDictionary<Guid, (DateTime? CreatedAt, DateTime? UpdatedAt)>> GetProductActivityDatesAsync(
        IReadOnlyCollection<Guid>? productIds = null, CancellationToken cancellationToken = default)
    {
        var createdQuery = DbContext.StockTransactions.AsNoTracking()
            .Where(x => x.TransactionType == StockTransactionType.Purchase);
        var updatedQuery = DbContext.Stocks.AsNoTracking();
        if (productIds is not null)
        {
            createdQuery = createdQuery.Where(x => productIds.Contains(x.ProductId));
            updatedQuery = updatedQuery.Where(x => productIds.Contains(x.ProductId));
        }
        var createdDates = await createdQuery
            .GroupBy(x => x.ProductId)
            .Select(group => new { ProductId = group.Key, CreatedAt = group.Max(x => x.CreatedAt) })
            .ToDictionaryAsync(x => x.ProductId, x => (DateTime?)x.CreatedAt, cancellationToken);
        var updatedDates = await updatedQuery
            .GroupBy(x => x.ProductId)
            .Select(group => new { ProductId = group.Key, UpdatedAt = group.Max(x => x.LastUpdatedDate) })
            .ToDictionaryAsync(x => x.ProductId, x => (DateTime?)x.UpdatedAt, cancellationToken);

        return createdDates.Keys.Union(updatedDates.Keys).ToDictionary(
            productId => productId,
            productId => (
                createdDates.GetValueOrDefault(productId),
                updatedDates.GetValueOrDefault(productId)));
    }

    public async Task<decimal> GetStockQuantityAsync(Guid productId, CancellationToken cancellationToken = default) =>
        await DbContext.Stocks.AsNoTracking().Where(x => x.ProductId == productId)
            .SumAsync(x => (decimal?)x.Quantity, cancellationToken) ?? 0m;

    public async Task<bool> SetStockQuantityAsync(Guid productId, decimal quantity, Guid appUserId,
        CancellationToken cancellationToken = default)
    {
        var product = await DbContext.Products.SingleOrDefaultAsync(x => x.Id == productId, cancellationToken);
        if (product is null) return false;
        if (!await DbContext.Users.AnyAsync(x => x.Id == appUserId, cancellationToken))
            throw new InvalidOperationException("Stok düzəlişini edən istifadəçi tapılmadı.");

        var stocks = await DbContext.Stocks.Where(x => x.ProductId == productId)
            .OrderBy(x => x.WarehouseId).ToListAsync(cancellationToken);
        var currentQuantity = stocks.Sum(x => x.Quantity);
        var difference = quantity - currentQuantity;
        if (difference == 0) return true;

        await using var transaction = await DbContext.Database.BeginTransactionAsync(cancellationToken);
        var transactions = new List<StockTransaction>();
        if (difference > 0)
        {
            Stock stock;
            if (stocks.Count > 0)
                stock = stocks[0];
            else
            {
                var warehouse = await DbContext.Warehouses.OrderBy(x => x.Name).FirstOrDefaultAsync(cancellationToken)
                    ?? throw new InvalidOperationException("Stok yazmaq üçün əvvəlcə anbar yaradılmalıdır.");
                stock = new Stock(productId, warehouse.Id, 0);
                DbContext.Stocks.Add(stock);
                stocks.Add(stock);
            }

            stock.SetQuantity(stock.Quantity + difference);
            transactions.Add(CreateAdjustment(product, stock, difference, appUserId));
        }
        else
        {
            var remaining = -difference;
            foreach (var stock in stocks)
            {
                if (remaining == 0) break;
                var deduction = Math.Min(stock.Quantity, remaining);
                if (deduction == 0) continue;
                stock.SetQuantity(stock.Quantity - deduction);
                remaining -= deduction;
                transactions.Add(CreateAdjustment(product, stock, -deduction, appUserId));
            }
        }

        stockLogs.Record(productId, product.Name, StockLogType.ADJUST, currentQuantity, quantity,
            "Admin stok düzəlişi", appUserId);
        DbContext.StockTransactions.AddRange(transactions);
        await DbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    private static StockTransaction CreateAdjustment(Product product, Stock stock, decimal quantity, Guid appUserId) =>
        new(product.Id, stock.WarehouseId, StockTransactionType.Adjustment, quantity,
            $"STK-{Guid.NewGuid():N}", product.UnitPrice, "Məhsul redaktəsi ilə stok düzəlişi", appUserId);

    public override async Task DeleteAsync(Product product, CancellationToken cancellationToken = default)
    {
        if (DbContext.Entry(product).State == EntityState.Detached)
            DbContext.Products.Attach(product);
        var deletedAt = DateTime.UtcNow;
        var batches = await DbContext.ProductBatches.IgnoreQueryFilters()
            .Where(x => x.ProductId == product.Id && !x.IsDeleted).ToListAsync(cancellationToken);
        foreach (var batch in batches) batch.MarkDeleted(deletedAt);
        product.MarkDeleted(deletedAt);
        await DbContext.SaveChangesAsync(cancellationToken);
    }
}
