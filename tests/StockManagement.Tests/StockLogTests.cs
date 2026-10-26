using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using StockManagement.Domain.Entities;
using StockManagement.Domain.Enums;
using StockManagement.Api.Controllers;
using StockManagement.Infrastructure.Identity;
using StockManagement.Infrastructure.Persistence;
using StockManagement.Infrastructure.Repositories;
using StockManagement.Infrastructure.Services;
using Xunit;

namespace StockManagement.Tests;

public sealed class StockLogTests
{
    [Fact]
    public async Task ReceiptSaleAndCorrectionWriteBeforeAfterSnapshots()
    {
        await using var db = NewContext();
        var (product, warehouse, user) = await SeedReferences(db);
        var logs = new StockLogService(db);
        var inventory = new EfInventoryRepository(db, logs);

        await inventory.ReceiveAsync(product.Id, warehouse.Id, 20, 2, 3, null, null, user.Id);
        await inventory.IssueFifoAsync(product.Id, warehouse.Id, 3, "SALE-1", user.Id);
        Assert.True(await new EfProductRepository(db, logs).SetStockQuantityAsync(product.Id, 14, user.Id));

        var rows = await db.StockLogs.ToDictionaryAsync(x => x.Type);
        AssertLog(rows[StockLogType.IN], StockLogType.IN, 20, 0, 20, product.Name);
        AssertLog(rows[StockLogType.OUT], StockLogType.OUT, -3, 20, 17, product.Name);
        AssertLog(rows[StockLogType.ADJUST], StockLogType.ADJUST, -3, 17, 14, product.Name);
    }

    [Fact]
    public async Task LogsCanBePagedFilteredAndRemainAfterProductDeletion()
    {
        await using var db = NewContext();
        var (product, _, _) = await SeedReferences(db);
        var logs = new StockLogService(db);
        var now = DateTime.UtcNow;
        for (var index = 0; index < 12; index++)
        {
            var before = index;
            db.StockLogs.Add(new StockLog(product.Id, product.Name,
                index % 2 == 0 ? StockLogType.IN : StockLogType.OUT, index % 2 == 0 ? 1 : -1,
                before, before + (index % 2 == 0 ? 1 : -1), "test", null,
                index == 0 ? now.AddDays(-2) : now.AddMinutes(index)));
        }
        await db.SaveChangesAsync();

        var firstPage = await logs.GetPageAsync(1, 10, product.Name, null, null, null);
        var filtered = await logs.GetPageAsync(1, 10, product.Name, "IN", null, null);
        var dateFiltered = await logs.GetPageAsync(1, 10, product.Name, null,
            DateOnly.FromDateTime(now), DateOnly.FromDateTime(now));
        Assert.Equal(10, firstPage.Items.Count);
        Assert.Equal(12, firstPage.TotalCount);
        Assert.Equal(6, filtered.TotalCount);
        Assert.Equal(11, dateFiltered.TotalCount);
        Assert.All(filtered.Items, item => Assert.Equal("IN", item.Type));

        await new EfProductRepository(db, logs).DeleteAsync(product);
        Assert.Equal(12, await db.StockLogs.CountAsync());
        Assert.All(await db.StockLogs.ToArrayAsync(), item => Assert.Equal(product.Name, item.ProductName));
    }

    [Fact]
    public async Task UnfilteredLogPageIncludesPersistedStockLog()
    {
        await using var db = NewContext();
        var (product, _, _) = await SeedReferences(db);
        db.StockLogs.Add(new StockLog(product.Id, product.Name, StockLogType.IN, 5, 0, 5, null, null));
        await db.SaveChangesAsync();

        var page = await new StockLogService(db).GetPageAsync(1, 10, null, null, null, null);

        var log = Assert.Single(page.Items);
        Assert.Equal(product.Name, log.ProductName);
        Assert.Equal("IN", log.Type);
        Assert.Equal(1, page.TotalCount);
    }

    [Fact]
    public async Task StockLogsRejectUpdatesAndDeletes()
    {
        await using var db = NewContext();
        var (product, _, _) = await SeedReferences(db);
        var log = new StockLog(product.Id, product.Name, StockLogType.IN, 2, 0, 2, null, null);
        db.StockLogs.Add(log);
        await db.SaveChangesAsync();

        db.Entry(log).State = EntityState.Modified;
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        db.Entry(log).State = EntityState.Unchanged;
        db.StockLogs.Remove(log);
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public void StockLogRoutesRequireAdminRole()
    {
        var authorization = typeof(StockLogsController).GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(authorization);
        Assert.Equal("Admin", authorization.Roles);
    }

    private static void AssertLog(StockLog log, StockLogType type, decimal change,
        decimal before, decimal after, string productName)
    {
        Assert.Equal(type, log.Type);
        Assert.Equal(change, log.QuantityChange);
        Assert.Equal(before, log.QuantityBefore);
        Assert.Equal(after, log.QuantityAfter);
        Assert.Equal(productName, log.ProductName);
    }

    private static StockManagementDbContext NewContext() => new(new DbContextOptionsBuilder<StockManagementDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString())
        .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning)).Options);

    private static async Task<(Product Product, Warehouse Warehouse, ApplicationUser User)> SeedReferences(StockManagementDbContext db)
    {
        var category = new Category("Test", "Test");
        var product = new Product("Test product", "TEST-001", "", "", category.Id, 12, "Piece", true);
        var warehouse = new Warehouse("Test warehouse", "Bakı");
        var user = new ApplicationUser { UserName = "test-user", FirstName = "Test", LastName = "User" };
        db.AddRange(category, product, warehouse, user);
        await db.SaveChangesAsync();
        return (product, warehouse, user);
    }
}
