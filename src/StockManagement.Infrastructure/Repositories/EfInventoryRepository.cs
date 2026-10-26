using Microsoft.EntityFrameworkCore;
using StockManagement.Application.Products;
using StockManagement.Application.Products.Repositories;
using StockManagement.Domain.Entities;
using StockManagement.Domain.Enums;
using StockManagement.Infrastructure.Persistence;
using StockManagement.Infrastructure.Services;

namespace StockManagement.Infrastructure.Repositories;

public sealed class EfInventoryRepository(StockManagementDbContext db, StockLogService stockLogs) : IInventoryRepository
{
    public async Task<IReadOnlyList<BatchInventoryItem>> GetActiveBatchesAsync(Guid productId, CancellationToken cancellationToken = default) =>
        await db.ProductBatches.AsNoTracking().Where(x => x.ProductId == productId && x.RemainingQuantity > 0)
            .OrderBy(x => x.EntryDate).ThenBy(x => x.Id)
            .Select(x => new BatchInventoryItem(x.BatchNumber, x.EntryDate, x.ExpiryDate, x.RemainingQuantity, x.PurchasePrice))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<WarehouseOption>> GetWarehousesAsync(CancellationToken cancellationToken = default) =>
        await db.Warehouses.AsNoTracking().OrderBy(x => x.Name)
            .Select(x => new WarehouseOption(x.Id, x.Name)).ToListAsync(cancellationToken);

    public async Task<InventoryScanResult?> FindByScanCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        var batch = await db.ProductBatches.AsNoTracking()
            .Where(x => x.BatchNumber == code)
            .Select(x => new InventoryScanResult(code, "batch", x.ProductId, x.Product.Name,
                x.Product.Sku, x.Product.Barcode, x.BatchNumber, x.Warehouse.Name,
                x.RemainingQuantity, x.EntryDate, x.ExpiryDate))
            .FirstOrDefaultAsync(cancellationToken);
        if (batch is not null) return batch;

        return await db.Products.AsNoTracking()
            .Where(x => x.Barcode == code && x.Barcode != string.Empty)
            .Select(x => new InventoryScanResult(code, "product", x.Id, x.Name,
                x.Sku, x.Barcode, null, null, null, null, null))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<ProductBatch> ReceiveAsync(Guid productId, Guid warehouseId, decimal quantity,
        decimal purchasePrice, decimal salePrice, DateTime? entryDate, DateTime? expiryDate,
        Guid appUserId, CancellationToken cancellationToken = default)
    {
        await EnsureReferencesAsync(productId, warehouseId, appUserId, cancellationToken);
        var occurredAt = entryDate ?? DateTime.UtcNow;
        var batch = new ProductBatch(productId, warehouseId, $"LOT-{Guid.NewGuid():N}"[..20], quantity,
            occurredAt, expiryDate, purchasePrice, salePrice);
        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        var productName = await db.Products.Where(x => x.Id == productId).Select(x => x.Name).SingleAsync(cancellationToken);
        var totalBefore = await db.Stocks.Where(x => x.ProductId == productId)
            .SumAsync(x => (decimal?)x.Quantity, cancellationToken) ?? 0m;
        db.ProductBatches.Add(batch);
        var stock = await db.Stocks.SingleOrDefaultAsync(x => x.ProductId == productId && x.WarehouseId == warehouseId, cancellationToken);
        if (stock is null) { stock = new Stock(productId, warehouseId, quantity); db.Stocks.Add(stock); }
        else stock.SetQuantity(stock.Quantity + quantity);
        stockLogs.Record(productId, productName, StockLogType.IN, totalBefore, totalBefore + quantity,
            $"Anbara giriş: {batch.BatchNumber}", appUserId);
        db.StockTransactions.Add(new StockTransaction(productId, warehouseId, StockTransactionType.Purchase,
            quantity, batch.BatchNumber, purchasePrice, $"Anbara giriş: {batch.BatchNumber}", appUserId));
        await db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
        return batch;
    }

    public async Task<IReadOnlyList<BatchAllocation>> IssueFifoAsync(Guid productId, Guid warehouseId,
        decimal quantity, string referenceNo, Guid appUserId, CancellationToken cancellationToken = default)
    {
        await EnsureReferencesAsync(productId, warehouseId, appUserId, cancellationToken);
        // Serializable range locks serialize issues and receipts for the same indexed product/warehouse range.
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken);
        var stock = await db.Stocks.SingleOrDefaultAsync(x => x.ProductId == productId && x.WarehouseId == warehouseId, cancellationToken);
        var totalBefore = await db.Stocks.Where(x => x.ProductId == productId)
            .SumAsync(x => (decimal?)x.Quantity, cancellationToken) ?? 0m;
        var batches = await db.ProductBatches.Where(x => x.ProductId == productId && x.WarehouseId == warehouseId && x.RemainingQuantity > 0)
            .OrderBy(x => x.EntryDate).ThenBy(x => x.Id).ToListAsync(cancellationToken);
        var available = batches.Sum(x => x.RemainingQuantity);
        if (available < quantity)
            throw new InvalidOperationException($"Kifayət qədər stok yoxdur. Mövcud: {available}, tələb olunan: {quantity}.");
        if (stock is null || stock.Quantity < quantity)
            throw new InvalidOperationException("Batch qalığı ilə ümumi stok uyğun gəlmir; əvvəlcə inventar uzlaşdırılmalıdır.");

        var remaining = quantity;
        var allocations = new List<BatchAllocation>();
        foreach (var batch in batches)
        {
            if (remaining <= 0) break;
            var taken = Math.Min(batch.RemainingQuantity, remaining);
            batch.Consume(taken);
            remaining -= taken;
            allocations.Add(new BatchAllocation(batch.BatchNumber, taken, batch.PurchasePrice));
        }
        stock.SetQuantity(stock.Quantity - quantity);
        var product = await db.Products.SingleAsync(x => x.Id == productId, cancellationToken);
        stockLogs.Record(productId, product.Name, StockLogType.OUT, totalBefore,
            totalBefore - quantity, $"FIFO üzrə çıxış: {referenceNo}", appUserId);
        db.StockTransactions.Add(new StockTransaction(productId, warehouseId, StockTransactionType.Sale,
            -quantity, referenceNo, allocations[0].PurchasePrice, "FIFO üzrə satış", appUserId));
        await db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
        return allocations;
    }

    private async Task EnsureReferencesAsync(Guid productId, Guid warehouseId, Guid appUserId, CancellationToken ct)
    {
        if (!await db.Products.AnyAsync(x => x.Id == productId, ct)) throw new KeyNotFoundException("Məhsul tapılmadı.");
        if (!await db.Warehouses.AnyAsync(x => x.Id == warehouseId, ct)) throw new KeyNotFoundException("Anbar tapılmadı.");
        if (!await db.Users.AnyAsync(x => x.Id == appUserId, ct)) throw new InvalidOperationException("Stok əməliyyatını edən istifadəçi tapılmadı.");
    }
}
