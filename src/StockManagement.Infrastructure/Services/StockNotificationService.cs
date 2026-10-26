using Microsoft.EntityFrameworkCore;
using StockManagement.Application.Abstractions;
using StockManagement.Application.DTOs.Notifications;
using StockManagement.Domain.Entities;
using StockManagement.Domain.Enums;
using StockManagement.Infrastructure.Persistence;

namespace StockManagement.Infrastructure.Services;

public sealed class StockNotificationService(StockManagementDbContext db) : IStockNotificationService
{
    public async Task RefreshForProductAsync(Guid productId, CancellationToken cancellationToken = default)
    {
        var product = await db.Products.SingleOrDefaultAsync(x => x.Id == productId, cancellationToken);
        if (product is null) return;

        var quantity = await db.Stocks.Where(x => x.ProductId == productId)
            .SumAsync(x => (decimal?)x.Quantity, cancellationToken) ?? 0m;
        var type = quantity <= 0 ? StockNotificationType.OUT_OF_STOCK
            : quantity <= 5m ? StockNotificationType.LOW_STOCK
            : (StockNotificationType?)null;
        var active = await db.StockNotifications.Where(x => x.ProductId == productId && x.IsActive)
            .OrderBy(x => x.CreatedAt).ToListAsync(cancellationToken);

        if (type is null)
        {
            foreach (var notification in active) notification.Resolve();
        }
        else if (active.Count > 0)
        {
            active[0].Update(BuildMessage(product.Name, quantity), type.Value);
            foreach (var duplicate in active.Skip(1)) duplicate.Resolve();
        }
        else
        {
            db.StockNotifications.Add(new StockNotification(productId, BuildMessage(product.Name, quantity), type.Value));
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<StockNotificationDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await RefreshAllAsync(cancellationToken);
        var notifications = await db.StockNotifications.AsNoTracking().Where(x => x.IsActive).Include(x => x.Product)
            .OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id).ToArrayAsync(cancellationToken);
        var productIds = notifications.Select(x => x.ProductId).Distinct().ToArray();
        var quantities = await db.Stocks.AsNoTracking().Where(x => productIds.Contains(x.ProductId))
            .GroupBy(x => x.ProductId).Select(x => new { ProductId = x.Key, Quantity = x.Sum(y => y.Quantity) })
            .ToDictionaryAsync(x => x.ProductId, x => x.Quantity, cancellationToken);
        return notifications.Select(x => new StockNotificationDto(x.Id, x.ProductId, x.Product.Name,
            quantities.GetValueOrDefault(x.ProductId), x.Message, x.Type, x.IsRead, x.CreatedAt)).ToArray();
    }

    public async Task<int> GetUnreadCountAsync(CancellationToken cancellationToken = default)
    {
        await RefreshAllAsync(cancellationToken);
        return await db.StockNotifications.CountAsync(x => !x.IsRead, cancellationToken);
    }

    private async Task RefreshAllAsync(CancellationToken cancellationToken)
    {
        var productIds = await db.Products.AsNoTracking().Select(x => x.Id).ToArrayAsync(cancellationToken);
        foreach (var productId in productIds)
            await RefreshForProductAsync(productId, cancellationToken);
    }

    public async Task<bool> MarkReadAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var notification = await db.StockNotifications.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (notification is null) return false;
        notification.MarkRead();
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task MarkAllReadAsync(CancellationToken cancellationToken = default)
    {
        var unread = await db.StockNotifications.Where(x => !x.IsRead).ToListAsync(cancellationToken);
        foreach (var notification in unread) notification.MarkRead();
        await db.SaveChangesAsync(cancellationToken);
    }

    private static string BuildMessage(string productName, decimal quantity) =>
        $"{productName}: stokda {quantity:0.###} qalıb.";
}
