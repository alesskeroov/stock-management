using Microsoft.EntityFrameworkCore;
using StockManagement.Application.DTOs.StockLogs;
using StockManagement.Domain.Entities;
using StockManagement.Domain.Enums;
using StockManagement.Infrastructure.Persistence;

namespace StockManagement.Infrastructure.Services;

public sealed class StockLogService(StockManagementDbContext db)
{
    public const int DefaultPageSize = 10;

    public void Record(Guid productId, string productName, StockLogType type, decimal quantityBefore,
        decimal quantityAfter, string? reason, Guid? createdBy)
    {
        var change = quantityAfter - quantityBefore;
        if (change == 0) return;
        db.StockLogs.Add(new StockLog(productId, productName, type, change, quantityBefore, quantityAfter, reason, createdBy));
    }

    public async Task<StockLogPage> GetPageAsync(int page, int size, string? product, string? type,
        DateOnly? fromDate, DateOnly? toDate, CancellationToken cancellationToken = default)
    {
        var query = BuildQuery(null, type, fromDate, toDate);
        if (!string.IsNullOrWhiteSpace(product))
        {
            var productSearch = product.Trim();
            query = Guid.TryParse(productSearch, out var id)
                ? query.Where(x => x.ProductId == id)
                : query.Where(x => x.ProductName.Contains(productSearch));
        }
        return await PageAsync(query, page, size, cancellationToken);
    }

    public async Task<StockLogPage> GetProductPageAsync(Guid productId, int page, int size,
        CancellationToken cancellationToken = default) =>
        await PageAsync(BuildQuery(productId, null, null, null), page, size, cancellationToken);

    private IQueryable<StockLog> BuildQuery(Guid? productId, string? type, DateOnly? fromDate, DateOnly? toDate)
    {
        var query = db.StockLogs.AsNoTracking();
        if (productId.HasValue) query = query.Where(x => x.ProductId == productId.Value);
        if (!string.IsNullOrWhiteSpace(type))
        {
            if (!Enum.TryParse<StockLogType>(type, true, out var parsed) || !Enum.IsDefined(parsed))
                return query.Where(_ => false);
            query = query.Where(x => x.Type == parsed);
        }
        if (fromDate.HasValue)
        {
            var from = fromDate.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            query = query.Where(x => x.CreatedAt >= from);
        }
        if (toDate.HasValue)
        {
            var until = toDate.Value.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            query = query.Where(x => x.CreatedAt < until);
        }
        return query;
    }

    private async Task<StockLogPage> PageAsync(IQueryable<StockLog> query, int page, int requestedSize, CancellationToken cancellationToken)
    {
        var pageSize = requestedSize < 1 ? DefaultPageSize : Math.Min(requestedSize, DefaultPageSize);
        var count = await query.CountAsync(cancellationToken);
        var totalPages = (int)Math.Ceiling(count / (double)pageSize);
        page = totalPages == 0 ? 1 : Math.Clamp(page, 1, totalPages);
        var logs = await query.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToArrayAsync(cancellationToken);
        var userIds = logs.Where(x => x.CreatedBy.HasValue).Select(x => x.CreatedBy!.Value).Distinct().ToArray();
        var users = await db.Users.AsNoTracking().Where(x => userIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => (x.FirstName + " " + x.LastName).Trim(), cancellationToken);
        return new(logs.Select(x => new StockLogDto(x.Id, x.ProductId, x.ProductName, x.Type.ToString().ToUpperInvariant(),
                x.QuantityChange, x.QuantityBefore, x.QuantityAfter, x.Reason,
                x.CreatedBy.HasValue ? users.GetValueOrDefault(x.CreatedBy.Value) : null, x.CreatedAt))
            .ToArray(), page, pageSize, count, totalPages, page < totalPages, page > 1);
    }
}
