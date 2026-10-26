using Microsoft.EntityFrameworkCore;
using StockManagement.Domain.Entities;
using StockManagement.Domain.Enums;
using StockManagement.Infrastructure.Persistence;
using StockManagement.Infrastructure.Services;
using Xunit;

namespace StockManagement.Tests;

public sealed class StockNotificationTests
{
    [Fact]
    public async Task CreatesLowStockNotificationAtFiveOrBelow()
    {
        await using var db = NewContext();
        var (product, stock) = await SeedProduct(db, 11);
        stock.SetQuantity(5);
        await db.SaveChangesAsync();

        var service = new StockNotificationService(db);
        await service.RefreshForProductAsync(product.Id);

        var notification = await db.StockNotifications.SingleAsync();
        Assert.Equal(StockNotificationType.LOW_STOCK, notification.Type);
        Assert.False(notification.IsRead);
    }

    [Fact]
    public async Task DoesNotListProductsWithMoreThanFiveInStock()
    {
        await using var db = NewContext();
        var (product, _) = await SeedProduct(db, 6);

        var notifications = await new StockNotificationService(db).GetAllAsync();

        Assert.Empty(notifications);
        Assert.Equal(0, await new StockNotificationService(db).GetUnreadCountAsync());
    }

    [Fact]
    public async Task CreatesOutOfStockNotificationAtZero()
    {
        await using var db = NewContext();
        var (product, stock) = await SeedProduct(db, 1);
        stock.SetQuantity(0);
        await db.SaveChangesAsync();

        await new StockNotificationService(db).RefreshForProductAsync(product.Id);

        Assert.Equal(StockNotificationType.OUT_OF_STOCK,
            (await db.StockNotifications.SingleAsync()).Type);
    }

    [Fact]
    public async Task UpdatesExistingUnreadNotificationInsteadOfDuplicating()
    {
        await using var db = NewContext();
        var (product, stock) = await SeedProduct(db, 5);
        var service = new StockNotificationService(db);
        await service.RefreshForProductAsync(product.Id);

        stock.SetQuantity(0);
        await db.SaveChangesAsync();
        await service.RefreshForProductAsync(product.Id);

        Assert.Equal(1, await db.StockNotifications.CountAsync());
        Assert.Equal(StockNotificationType.OUT_OF_STOCK, (await db.StockNotifications.SingleAsync()).Type);
    }

    [Fact]
    public async Task MarksUnreadNotificationReadWhenStockRecovers()
    {
        await using var db = NewContext();
        var (product, stock) = await SeedProduct(db, 4);
        var service = new StockNotificationService(db);
        await service.RefreshForProductAsync(product.Id);

        stock.SetQuantity(11);
        await db.SaveChangesAsync();
        await service.RefreshForProductAsync(product.Id);

        Assert.Equal(0, await service.GetUnreadCountAsync());
        Assert.True((await db.StockNotifications.SingleAsync()).IsRead);
    }

    [Fact]
    public async Task SupportsMarkingOneAndAllNotificationsRead()
    {
        await using var db = NewContext();
        var (first, _) = await SeedProduct(db, 0, "FIRST");
        var (second, _) = await SeedProduct(db, 0, "SECOND");
        var service = new StockNotificationService(db);
        await service.RefreshForProductAsync(first.Id);
        await service.RefreshForProductAsync(second.Id);
        var firstNotificationId = await db.StockNotifications.Where(x => x.ProductId == first.Id).Select(x => x.Id).SingleAsync();

        Assert.True(await service.MarkReadAsync(firstNotificationId));
        Assert.Equal(1, await service.GetUnreadCountAsync());
        await service.MarkAllReadAsync();
        Assert.Equal(0, await service.GetUnreadCountAsync());
    }

    [Fact]
    public async Task ReadLowStockAlertStaysReadUntilStockRecoversAndFallsAgain()
    {
        await using var db = NewContext();
        var (product, stock) = await SeedProduct(db, 0);
        var service = new StockNotificationService(db);
        await service.RefreshForProductAsync(product.Id);
        var first = await db.StockNotifications.SingleAsync();

        Assert.True(await service.MarkReadAsync(first.Id));
        Assert.Equal(0, await service.GetUnreadCountAsync());
        Assert.Equal(1, await db.StockNotifications.CountAsync());

        stock.SetQuantity(11);
        await db.SaveChangesAsync();
        await service.RefreshForProductAsync(product.Id);
        stock.SetQuantity(0);
        await db.SaveChangesAsync();
        await service.RefreshForProductAsync(product.Id);

        Assert.Equal(2, await db.StockNotifications.CountAsync());
        Assert.Equal(1, await service.GetUnreadCountAsync());
    }

    private static StockManagementDbContext NewContext() => new(
        new DbContextOptionsBuilder<StockManagementDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static async Task<(Product Product, Stock Stock)> SeedProduct(StockManagementDbContext db,
        decimal quantity, string sku = "LOW-001")
    {
        var category = new Category("Test", "");
        var warehouse = new Warehouse("Test warehouse", "Test");
        var product = new Product("Test product", sku, "", "", category.Id, 1, "Piece", true);
        var stock = new Stock(product.Id, warehouse.Id, quantity);
        db.AddRange(category, warehouse, product, stock);
        await db.SaveChangesAsync();
        return (product, stock);
    }
}
