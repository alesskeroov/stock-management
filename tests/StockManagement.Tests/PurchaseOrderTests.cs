using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using StockManagement.Api.Controllers;
using StockManagement.Application.DTOs.PurchaseOrders;
using StockManagement.Domain.Entities;
using StockManagement.Domain.Enums;
using StockManagement.Domain.Services;
using StockManagement.Infrastructure.Identity;
using StockManagement.Infrastructure.Persistence;
using StockManagement.Infrastructure.Services;
using StockManagement.Infrastructure.Repositories;
using StockManagement.Application.Suppliers;
using Xunit;

namespace StockManagement.Tests;

public sealed class PurchaseOrderTests
{
    [Fact]
    public async Task CreateAndEditDraftRecalculateTotalsWithoutChangingStock()
    {
        await using var db = NewContext();
        var (user, supplier, warehouse, product) = await SeedAsync(db);
        db.Stocks.Add(new Stock(product.Id, warehouse.Id, 17));
        await db.SaveChangesAsync();
        var service = Service(db);
        var request = Request(supplier, warehouse, product, quantity: 2, price: 7.5m, sentLineTotal: 1, sentTotal: 1);

        var order = await service.CreateAsync(request, user.Id);
        Assert.Equal("draft", order.Status);
        Assert.Equal("PO-2026-0001", order.PoNumber);
        Assert.Equal(15m, order.TotalAmount);
        Assert.Equal(15m, Assert.Single(order.Items).LineTotal);

        db.ChangeTracker.Clear();

        var edited = await service.UpdateAsync(order.Id,
            Request(supplier, warehouse, product, quantity: 4, price: 3, sentLineTotal: 999, sentTotal: 999), user.Id);
        Assert.Equal(12m, edited.TotalAmount);
        db.ChangeTracker.Clear();
        var saved = await service.GetAsync(order.Id);
        Assert.NotNull(saved);
        Assert.Equal(12m, saved.TotalAmount);
        Assert.Equal(4m, Assert.Single(saved.Items).OrderedQty);
        Assert.Equal(3m, Assert.Single(saved.Items).UnitPrice);
        Assert.Single(await db.PurchaseOrderItems.Where(x => x.PurchaseOrderId == order.Id).ToArrayAsync());
        Assert.Equal(17m, await db.Stocks.Select(x => x.Quantity).SingleAsync());
        Assert.Empty(await db.StockMovements.ToArrayAsync());
    }

    [Fact]
    public async Task CorrectDraftPendingApprovalApprovedFlowAndSelfApprovalOverride()
    {
        await using var db = NewContext();
        var (user, supplier, warehouse, product) = await SeedAsync(db);
        var other = new ApplicationUser { UserName = "approver", FirstName = "Approver", LastName = "User" };
        db.Users.Add(other);
        await db.SaveChangesAsync();
        var service = Service(db);
        var order = await service.CreateAsync(Request(supplier, warehouse, product, 1, 2, 2, 2), user.Id);
        Assert.Equal("pending_approval", (await service.SubmitAsync(order.Id, user.Id)).Status);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ApproveAsync(order.Id, user.Id, false));
        Assert.Equal("approved", (await service.ApproveAsync(order.Id, user.Id, true)).Status);

        var logs = await db.AuditLogs.Where(x => x.EntityId == order.Id).ToArrayAsync();
        Assert.Equal(3, logs.Length);
        Assert.Contains(logs, x => x.Action == "status-change" && x.OldValues!.Contains("draft") && x.NewValues!.Contains("pending_approval"));
    }

    [Fact]
    public async Task InvalidLinesAndForbiddenTransitionsAreRejected()
    {
        await using var db = NewContext();
        var (user, supplier, warehouse, product) = await SeedAsync(db);
        var service = Service(db);
        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateAsync(
            new SavePurchaseOrderRequest(supplier.Id, warehouse.Id, new DateOnly(2026, 1, 1), null, "AZN", null, [], 0), user.Id));
        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateAsync(
            Request(supplier, warehouse, product, -1, 2, 2, 2), user.Id));
        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateAsync(
            Request(supplier, warehouse, product, 1, -2, 2, 2), user.Id));
        var duplicate = new PurchaseOrderLineRequest[]
        {
            new(product.Id, 1, 1), new(product.Id, 2, 1)
        };
        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateAsync(
            new SavePurchaseOrderRequest(supplier.Id, warehouse.Id, new DateOnly(2026, 1, 1), null, "AZN", null, duplicate), user.Id));

        var order = await service.CreateAsync(Request(supplier, warehouse, product, 1, 2, 2, 2), user.Id);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ApproveAsync(order.Id, user.Id, true));
        await service.CancelAsync(order.Id, user.Id, "Test cancel");
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ApproveAsync(order.Id, user.Id, true));
    }

    [Fact]
    public async Task ApprovedOrderIsImmutableAndCancellationRequiresReasonAndNoReceipts()
    {
        await using var db = NewContext();
        var (user, supplier, warehouse, product) = await SeedAsync(db);
        var service = Service(db);
        var order = await service.CreateAsync(Request(supplier, warehouse, product, 1, 2, 2, 2), user.Id);
        await service.SubmitAsync(order.Id, user.Id);
        await service.ApproveAsync(order.Id, user.Id, true);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.UpdateAsync(order.Id,
            Request(supplier, warehouse, product, 5, 2, 2, 2), user.Id));
        await Assert.ThrowsAsync<ArgumentException>(() => service.CancelAsync(order.Id, user.Id, " "));
        Assert.Equal("cancelled", (await service.CancelAsync(order.Id, user.Id, "Changed plans")).Status);
    }

    [Fact]
    public void StateMachineRejectsUnlistedTransitions()
    {
        Assert.Throws<InvalidOperationException>(() => PurchaseOrderStateMachine.EnsureTransition(PurchaseOrderStatus.Draft, PurchaseOrderStatus.Approved));
        Assert.Throws<InvalidOperationException>(() => PurchaseOrderStateMachine.EnsureTransition(PurchaseOrderStatus.Cancelled, PurchaseOrderStatus.Approved));
    }

    [Fact]
    public async Task SupplierWithPurchaseOrderCanBeDeactivatedButNotSoftDeleted()
    {
        await using var db = NewContext();
        var (user, supplier, warehouse, product) = await SeedAsync(db);
        await Service(db).CreateAsync(Request(supplier, warehouse, product, 1, 2, 2, 2), user.Id);
        var service = new SupplierService(new EfSupplierRepository(db), new AuditLogService(db));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.DeleteAsync(supplier.Id, user.Id));
        var deactivated = await service.SetActiveAsync(supplier.Id, false, user.Id);
        Assert.False(deactivated!.IsActive);
    }

    [Fact]
    public void PurchaseOrderRoutesDeclarePermissionPolicies()
    {
        Assert.Equal("po.view", typeof(PurchaseOrdersController).GetMethod(nameof(PurchaseOrdersController.GetPage))!
            .GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>().Single().Policy);
        Assert.Equal("po.approve", typeof(PurchaseOrdersController).GetMethod(nameof(PurchaseOrdersController.Approve))!
            .GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>().Single().Policy);
        Assert.Equal("po.cancel", typeof(PurchaseOrdersController).GetMethod(nameof(PurchaseOrdersController.Cancel))!
            .GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>().Single().Policy);
    }

    private static PurchaseOrderService Service(StockManagementDbContext db) =>
        new(db, new DocumentNumberService(db), new AuditLogService(db));

    private static SavePurchaseOrderRequest Request(Supplier supplier, Warehouse warehouse, Product product,
        decimal quantity, decimal price, decimal sentLineTotal, decimal sentTotal) => new(
        supplier.Id, warehouse.Id, new DateOnly(2026, 5, 10), new DateOnly(2026, 6, 1), "AZN", "Test",
        [new PurchaseOrderLineRequest(product.Id, quantity, price, sentLineTotal)], sentTotal);

    private static StockManagementDbContext NewContext() => new(new DbContextOptionsBuilder<StockManagementDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static async Task<(ApplicationUser User, Supplier Supplier, Warehouse Warehouse, Product Product)> SeedAsync(StockManagementDbContext db)
    {
        var user = new ApplicationUser { UserName = "creator", FirstName = "Test", LastName = "Creator" };
        var category = new Category("Test", "Test category");
        var product = new Product("Product", "PO-TEST-001", "", "", category.Id, 1, "Piece", true);
        var warehouse = new Warehouse("Main", "Baku");
        var supplier = new Supplier("Supplier", "050", "supplier@example.az", "Baku", "1234567890");
        db.AddRange(user, category, product, warehouse, supplier);
        await db.SaveChangesAsync();
        return (user, supplier, warehouse, product);
    }
}
