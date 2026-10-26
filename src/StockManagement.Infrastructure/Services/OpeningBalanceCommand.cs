using Microsoft.EntityFrameworkCore;
using StockManagement.Domain.Entities;
using StockManagement.Domain.Enums;
using StockManagement.Infrastructure.Persistence;

namespace StockManagement.Infrastructure.Services;

/// <summary>Copies the current stock cache into the ledger once per product/warehouse.</summary>
public sealed class OpeningBalanceCommand(StockManagementDbContext db)
{
    public async Task<int> ExecuteAsync(Guid createdBy, CancellationToken cancellationToken = default)
    {
        if (createdBy == Guid.Empty) throw new ArgumentException("Əməliyyatı edən istifadəçi tələb olunur.", nameof(createdBy));
        await using var transaction = db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken)
            : null;

        var existing = await db.StockMovements.AsNoTracking()
            .Where(x => x.Type == StockMovementType.OpeningBalance)
            .Select(x => new { x.ProductId, x.WarehouseId })
            .ToListAsync(cancellationToken);
        var existingPairs = existing.Select(x => (x.ProductId, x.WarehouseId)).ToHashSet();
        var stocks = await db.Stocks.AsNoTracking().Include(x => x.Product).ToArrayAsync(cancellationToken);
        var added = 0;
        foreach (var stock in stocks)
        {
            if (!existingPairs.Add((stock.ProductId, stock.WarehouseId))) continue;
            db.StockMovements.Add(new StockMovement(stock.ProductId, stock.WarehouseId,
                StockMovementType.OpeningBalance, stock.Quantity, stock.Product.UnitPrice,
                "OPENING_BALANCE", stock.Id, createdBy));
            added++;
        }

        if (added > 0) await db.SaveChangesAsync(cancellationToken);
        if (transaction is not null) await transaction.CommitAsync(cancellationToken);
        return added;
    }
}

public sealed record StockDifference(Guid ProductId, Guid WarehouseId, decimal CachedQuantity, decimal MovementQuantity);

/// <summary>Reports cache/ledger differences without modifying either value.</summary>
public sealed class StockReconciliationCommand(StockManagementDbContext db)
{
    public async Task<IReadOnlyCollection<StockDifference>> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var cached = await db.Stocks.AsNoTracking()
            .ToDictionaryAsync(x => (x.ProductId, x.WarehouseId), x => x.Quantity, cancellationToken);
        var ledger = await db.StockMovements.AsNoTracking()
            .GroupBy(x => new { x.ProductId, x.WarehouseId })
            .Select(group => new { group.Key.ProductId, group.Key.WarehouseId, Quantity = group.Sum(x => x.Quantity) })
            .ToDictionaryAsync(x => (x.ProductId, x.WarehouseId), x => x.Quantity, cancellationToken);
        return cached.Keys.Union(ledger.Keys)
            .Select(key => new StockDifference(key.ProductId, key.WarehouseId,
                cached.GetValueOrDefault(key), ledger.GetValueOrDefault(key)))
            .Where(x => x.CachedQuantity != x.MovementQuantity)
            .OrderBy(x => x.ProductId).ThenBy(x => x.WarehouseId).ToArray();
    }
}
