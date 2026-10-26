using Microsoft.EntityFrameworkCore;
using StockManagement.Application.Products;
using StockManagement.Application.Security;
using StockManagement.Domain.Entities;
using StockManagement.Domain.Enums;
using StockManagement.Infrastructure.Identity;
using StockManagement.Infrastructure.Persistence;
using StockManagement.Infrastructure.Repositories;
using StockManagement.Infrastructure.Services;
using Xunit;

namespace StockManagement.Tests;

public sealed class ProcurementFoundationTests
{
    [Fact]
    public async Task StockMovementRejectsUpdateAndDelete()
    {
        await using var db = NewContext();
        var (product, warehouse, user) = await SeedReferences(db);
        var movement = new StockMovement(product.Id, warehouse.Id, StockMovementType.PurchaseIn,
            7, 12, "TEST", Guid.NewGuid(), user.Id);
        db.StockMovements.Add(movement);
        await db.SaveChangesAsync();

        db.Entry(movement).State = EntityState.Modified;
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        db.Entry(movement).State = EntityState.Unchanged;
        db.StockMovements.Remove(movement);
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task GetStockSumsPositiveAndNegativeMovements()
    {
        await using var db = NewContext();
        var (product, warehouse, user) = await SeedReferences(db);
        db.StockMovements.AddRange(
            new StockMovement(product.Id, warehouse.Id, StockMovementType.PurchaseIn, 15, 2, "TEST", Guid.NewGuid(), user.Id),
            new StockMovement(product.Id, warehouse.Id, StockMovementType.SaleOut, -4, 2, "TEST", Guid.NewGuid(), user.Id),
            new StockMovement(product.Id, warehouse.Id, StockMovementType.Adjustment, -1, 2, "TEST", Guid.NewGuid(), user.Id));
        await db.SaveChangesAsync();

        var stock = await new StockService(new EfStockMovementRepository(db)).GetStock(product.Id, warehouse.Id);
        Assert.Equal(10m, stock);
    }

    [Fact]
    public async Task OpeningBalanceCommandIsIdempotent()
    {
        await using var db = NewContext();
        var (product, warehouse, user) = await SeedReferences(db);
        db.Stocks.Add(new Stock(product.Id, warehouse.Id, 12));
        await db.SaveChangesAsync();
        var command = new OpeningBalanceCommand(db);

        Assert.Equal(1, await command.ExecuteAsync(user.Id));
        Assert.Equal(0, await command.ExecuteAsync(user.Id));
        Assert.Equal(12m, await db.StockMovements.SumAsync(x => x.Quantity));
    }

    [Fact]
    public async Task DocumentNumbersAreUniqueForParallelCallsAndRestartEachYear()
    {
        var database = Guid.NewGuid().ToString();
        var numbers = await Task.WhenAll(Enumerable.Range(0, 24).Select(async _ =>
        {
            await using var db = NewContext(database);
            return await new DocumentNumberService(db).GetNextAsync("PO", 2026);
        }));

        Assert.Equal(24, numbers.Distinct().Count());
        Assert.Contains("PO-2026-0001", numbers);
        await using var nextYearDb = NewContext(database);
        Assert.Equal("PO-2027-0001", await new DocumentNumberService(nextYearDb).GetNextAsync("PO", 2027));
    }

    [Fact]
    public async Task StockDocumentNumbersUseSeparateInAndOutSequences()
    {
        await using var db = NewContext();
        var numbers = new DocumentNumberService(db);

        Assert.Equal("DX-0001", await numbers.GetNextStockDocumentNumberAsync("in"));
        Assert.Equal("DX-0002", await numbers.GetNextStockDocumentNumberAsync("in"));
        Assert.Equal("XQ-0001", await numbers.GetNextStockDocumentNumberAsync("out"));
    }

    [Fact]
    public async Task SupplierCanBeDeactivatedAndSoftDeleted()
    {
        await using var db = NewContext();
        var supplier = new Supplier("Təchizatçı", "050", "vendor@example.az", "Bakı", "1234567891");
        db.Suppliers.Add(supplier);
        await db.SaveChangesAsync();
        supplier.Deactivate();
        await db.SaveChangesAsync();
        Assert.False(supplier.IsActive);

        supplier.MarkDeleted();
        await db.SaveChangesAsync();
        Assert.NotNull(await db.Suppliers.IgnoreQueryFilters().SingleOrDefaultAsync(x => x.Id == supplier.Id));
        Assert.Null(await db.Suppliers.SingleOrDefaultAsync(x => x.Id == supplier.Id));
    }

    [Fact]
    public void ProcurementPermissionsMatchConfiguredRoles()
    {
        Assert.Contains(Permissions.GoodsReceiptCreate, RolePermissionDefaults.For(Roles.WarehouseKeeper));
        Assert.Contains(Permissions.PurchaseOrderView, RolePermissionDefaults.For(Roles.WarehouseKeeper));
        Assert.Contains(Permissions.PurchaseOrderCreate, RolePermissionDefaults.For(Roles.ProcurementStaff));
        Assert.Contains(Permissions.PurchaseOrderView, RolePermissionDefaults.For(Roles.ProcurementStaff));
        Assert.Contains(Permissions.SupplierManage, RolePermissionDefaults.For(Roles.ProcurementStaff));
        foreach (var permission in new[] { Permissions.PurchaseOrderView, Permissions.PurchaseOrderCreate,
                     Permissions.PurchaseOrderApprove, Permissions.PurchaseOrderCancel, Permissions.GoodsReceiptCreate,
                     Permissions.GoodsReceiptReverse, Permissions.SupplierManage })
        {
            Assert.Contains(permission, RolePermissionDefaults.For(Roles.Manager));
            Assert.Contains(permission, RolePermissionDefaults.For(Roles.Admin));
        }
    }

    private static StockManagementDbContext NewContext(string? database = null) =>
        new(new DbContextOptionsBuilder<StockManagementDbContext>()
            .UseInMemoryDatabase(database ?? Guid.NewGuid().ToString()).Options);

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
