using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Authorization;
using StockManagement.Api.Controllers;
using StockManagement.Application.Abstractions;
using StockManagement.Application.DTOs.GoodsReceipts;
using StockManagement.Application.DTOs.PurchaseOrders;
using StockManagement.Application.Products;
using StockManagement.Domain.Entities;
using StockManagement.Domain.Enums;
using StockManagement.Infrastructure.Identity;
using StockManagement.Infrastructure.Persistence;
using StockManagement.Infrastructure.Repositories;
using StockManagement.Infrastructure.Services;
using Xunit;

namespace StockManagement.Tests;

public sealed class GoodsReceiptTests
{
    [Fact]
    public async Task PartialThenFinalReceiptUpdatesStatusStockAndWeightedAverageCost()
    {
        await using var db = NewContext();
        var (user, supplier, warehouse, product) = await SeedAsync(db, 0);
        var (orderId, poItemId) = await CreateApprovedOrder(db, user, supplier, warehouse, product, 10);
        var service = GrnService(db);

        var first = await service.CreateAsync(Request(orderId, poItemId, 4, 1, 8, "first-key"), user.Id);
        Assert.Equal(4m, first.Items.Single().ReceivedQty);
        Assert.Equal(1m, first.Items.Single().RejectedQty);
        Assert.Equal(PurchaseOrderStatus.PartiallyReceived, (await db.PurchaseOrders.SingleAsync(x => x.Id == orderId)).Status);
        Assert.Equal(4m, await new StockService(new EfStockMovementRepository(db)).GetStock(product.Id, warehouse.Id));
        Assert.Equal(8m, (await db.Products.SingleAsync(x => x.Id == product.Id)).AverageCost);

        await service.CreateAsync(Request(orderId, poItemId, 6, 0, 12, "second-key"), user.Id);
        Assert.Equal(PurchaseOrderStatus.Received, (await db.PurchaseOrders.SingleAsync(x => x.Id == orderId)).Status);
        Assert.Equal(10m, await new StockService(new EfStockMovementRepository(db)).GetStock(product.Id, warehouse.Id));
        Assert.Equal(10.4m, (await db.Products.SingleAsync(x => x.Id == product.Id)).AverageCost);
        Assert.Equal(10m, await db.StockMovements.SumAsync(x => x.Quantity));
        Assert.Equal(10m, (await db.Stocks.SingleAsync(x => x.ProductId == product.Id)).Quantity);
        await Assert.ThrowsAsync<GoodsReceiptConflictException>(() => service.CreateAsync(
            Request(orderId, poItemId, 1, 0, 12, "after-received"), user.Id));
    }

    [Fact]
    public async Task IdempotencyReturnsExistingReceiptWithoutRepeatingMovement()
    {
        await using var db = NewContext();
        var (user, supplier, warehouse, product) = await SeedAsync(db, 5);
        var (orderId, poItemId) = await CreateApprovedOrder(db, user, supplier, warehouse, product, 5);
        var service = GrnService(db);
        var request = Request(orderId, poItemId, 5, 0, 4, "retry-key");

        var first = await service.CreateAsync(request, user.Id);
        var retry = await service.CreateAsync(request, user.Id);
        Assert.Equal(first.Id, retry.Id);
        Assert.Equal(1, await db.GoodsReceipts.CountAsync());
        Assert.Equal(1, await db.StockMovements.CountAsync(x => x.Type == StockMovementType.PurchaseIn));
        Assert.Equal(10m, await db.StockMovements.SumAsync(x => x.Quantity));
        Assert.Equal(10m, (await db.Stocks.SingleAsync()).Quantity);
        var savedReceipt = await db.GoodsReceipts.SingleAsync();
        db.Entry(savedReceipt).State = EntityState.Modified;
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        db.Entry(savedReceipt).State = EntityState.Unchanged;
        db.GoodsReceipts.Remove(savedReceipt);
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task ConcurrentReceiptsForSamePoCannotExceedOrderedQuantity()
    {
        var database = Guid.NewGuid().ToString();
        await using var db = NewContext(database);
        var (user, supplier, warehouse, product) = await SeedAsync(db, 0);
        var (orderId, poItemId) = await CreateApprovedOrder(db, user, supplier, warehouse, product, 10);

        var attempts = await Task.WhenAll(Enumerable.Range(0, 2).Select(async index =>
        {
            await using var parallelDb = NewContext(database);
            try
            {
                await GrnService(parallelDb).CreateAsync(Request(orderId, poItemId, 6, 0, 2, $"parallel-{index}"), user.Id);
                return true;
            }
            catch (GoodsReceiptValidationException) { return false; }
        }));

        Assert.Single(attempts, succeeded => succeeded);
        await using var checkDb = NewContext(database);
        Assert.Equal(6m, await checkDb.StockMovements.SumAsync(x => x.Quantity));
        Assert.Equal(6m, (await checkDb.PurchaseOrderItems.SingleAsync(x => x.Id == poItemId)).ReceivedQty);
    }

    [Fact]
    public async Task OverReceiptIsRejectedByDefaultAndAllowedWithinConfiguredTolerance()
    {
        await using var db = NewContext();
        var (user, supplier, warehouse, product) = await SeedAsync(db, 100);
        var (orderId, poItemId) = await CreateApprovedOrder(db, user, supplier, warehouse, product, 100);
        var request = Request(orderId, poItemId, 101, 0, 2, "too-much");
        await Assert.ThrowsAsync<GoodsReceiptValidationException>(() => GrnService(db).CreateAsync(request, user.Id));
        Assert.Empty(await db.GoodsReceipts.ToArrayAsync());

        var tolerant = GrnService(db, 5);
        var receipt = await tolerant.CreateAsync(Request(orderId, poItemId, 105, 0, 2, "within-tolerance"), user.Id);
        Assert.Equal(105m, receipt.Items.Single().ReceivedQty);
        Assert.Equal(105m, await db.StockMovements.Where(x => x.Type == StockMovementType.PurchaseIn).SumAsync(x => x.Quantity));
        Assert.Equal(205m, await db.StockMovements.SumAsync(x => x.Quantity));
        Assert.Equal(0m, Assert.Single((await PoService(db).GetAsync(orderId))!.Items).RemainingQty);
        var toleranceConfig = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Procurement:OverReceiptTolerancePercent"] = "5"
        }).Build();
        var poWithConfig = new PurchaseOrderService(db, new DocumentNumberService(db), new AuditLogService(db), toleranceConfig);
        Assert.Equal(5m, (await poWithConfig.GetAsync(orderId))!.OverReceiptTolerancePercent);
    }

    [Fact]
    public async Task RejectedOnlyReceiptDoesNotIncreaseStockOrChangePoReceiptQuantity()
    {
        await using var db = NewContext();
        var (user, supplier, warehouse, product) = await SeedAsync(db, 5);
        var (orderId, poItemId) = await CreateApprovedOrder(db, user, supplier, warehouse, product, 5);
        var receipt = await GrnService(db).CreateAsync(Request(orderId, poItemId, 0, 2, 3, "rejected-only"), user.Id);
        Assert.Equal(0m, receipt.Items.Single().ReceivedQty);
        Assert.Equal(2m, receipt.Items.Single().RejectedQty);
        Assert.Empty(await db.StockMovements.Where(x => x.Type == StockMovementType.PurchaseIn).ToArrayAsync());
        Assert.Equal(5m, await db.StockMovements.SumAsync(x => x.Quantity));
        Assert.Equal(5m, (await db.Stocks.SingleAsync(x => x.ProductId == product.Id)).Quantity);
        Assert.Equal(0m, (await db.PurchaseOrderItems.SingleAsync(x => x.Id == poItemId)).ReceivedQty);
        Assert.Equal(PurchaseOrderStatus.Approved, (await db.PurchaseOrders.SingleAsync(x => x.Id == orderId)).Status);
    }

    [Fact]
    public async Task DraftAndCancelledOrdersCannotReceiveAndAuditFailureLeavesNoBusinessWrites()
    {
        await using var db = NewContext();
        var (user, supplier, warehouse, product) = await SeedAsync(db, 5);
        var draft = await CreateDraftOrder(db, user, supplier, warehouse, product, 5);
        await Assert.ThrowsAsync<GoodsReceiptConflictException>(() => GrnService(db).CreateAsync(
            Request(draft.OrderId, draft.ItemId, 1, 0, 2, null), user.Id));
        var poService = PoService(db);
        await poService.CancelAsync(draft.OrderId, user.Id, "Test cancel");
        await Assert.ThrowsAsync<GoodsReceiptConflictException>(() => GrnService(db).CreateAsync(
            Request(draft.OrderId, draft.ItemId, 1, 0, 2, null), user.Id));

        var approved = await CreateApprovedOrder(db, user, supplier, warehouse, product, 5);
        var failing = GrnService(db, audit: new FailingAuditLogService());
        await Assert.ThrowsAsync<InvalidOperationException>(() => failing.CreateAsync(
            Request(approved.OrderId, approved.ItemId, 3, 0, 2, null), user.Id));
        db.ChangeTracker.Clear();
        Assert.Empty(await db.GoodsReceipts.ToArrayAsync());
        Assert.Empty(await db.StockMovements.Where(x => x.Type == StockMovementType.PurchaseIn).ToArrayAsync());
        Assert.Equal(5m, await db.StockMovements.SumAsync(x => x.Quantity));
        Assert.Equal(0m, (await db.PurchaseOrderItems.SingleAsync(x => x.Id == approved.ItemId)).ReceivedQty);
        Assert.Equal(5m, (await db.Stocks.SingleAsync(x => x.ProductId == product.Id)).Quantity);
        Assert.Equal(PurchaseOrderStatus.Approved, (await db.PurchaseOrders.SingleAsync(x => x.Id == approved.OrderId)).Status);
    }

    [Fact]
    public async Task ReceiptRejectsNegativeValuesAndEmptyLines()
    {
        await using var db = NewContext();
        var (user, supplier, warehouse, product) = await SeedAsync(db, 5);
        var (orderId, poItemId) = await CreateApprovedOrder(db, user, supplier, warehouse, product, 5);
        await Assert.ThrowsAsync<GoodsReceiptValidationException>(() => GrnService(db).CreateAsync(
            new CreateGoodsReceiptRequest(orderId, new DateOnly(2026, 5, 15), null, null, []), user.Id));
        await Assert.ThrowsAsync<GoodsReceiptValidationException>(() => GrnService(db).CreateAsync(
            Request(orderId, poItemId, -1, 0, 2, null), user.Id));
        await Assert.ThrowsAsync<GoodsReceiptValidationException>(() => GrnService(db).CreateAsync(
            Request(orderId, poItemId, 1, -1, 2, null), user.Id));
    }

    [Fact]
    public async Task ReverseReceiptAddsNegativeMovementAndReopensOrderOnlyOnce()
    {
        await using var db = NewContext();
        var (user, supplier, warehouse, product) = await SeedAsync(db, 0);
        var (orderId, itemId) = await CreateApprovedOrder(db, user, supplier, warehouse, product, 4);
        var service = GrnService(db);
        var receipt = await service.CreateAsync(Request(orderId, itemId, 4, 1, 7, "reverse-test"), user.Id);

        var reversed = await service.ReverseAsync(receipt.Id, user.Id, "Səhv qəbul", CancellationToken.None);
        Assert.NotNull(reversed.ReversedAt);
        Assert.Equal("Səhv qəbul", reversed.ReverseReason);
        Assert.Equal(0m, await new StockService(new EfStockMovementRepository(db)).GetStock(product.Id, warehouse.Id));
        Assert.Equal(0m, (await db.Stocks.SingleAsync(x => x.ProductId == product.Id)).Quantity);
        Assert.Equal(0m, (await db.PurchaseOrderItems.SingleAsync(x => x.Id == itemId)).ReceivedQty);
        Assert.Equal(PurchaseOrderStatus.Approved, (await db.PurchaseOrders.SingleAsync(x => x.Id == orderId)).Status);
        Assert.Equal(2, await db.StockMovements.CountAsync());
        Assert.NotNull(Assert.Single((await PoService(db).GetAsync(orderId))!.GoodsReceipts).ReversedAt);
        Assert.Equal(-4m, await db.StockMovements.Where(x => x.Type == StockMovementType.Reversal).SumAsync(x => x.Quantity));
        await Assert.ThrowsAsync<GoodsReceiptConflictException>(() => service.ReverseAsync(receipt.Id, user.Id, "Təkrar", CancellationToken.None));
    }

    [Fact]
    public async Task ReverseReceiptFailsWhenReceivedStockHasAlreadyBeenConsumed()
    {
        await using var db = NewContext();
        var (user, supplier, warehouse, product) = await SeedAsync(db, 0);
        var (orderId, itemId) = await CreateApprovedOrder(db, user, supplier, warehouse, product, 4);
        var service = GrnService(db);
        var receipt = await service.CreateAsync(Request(orderId, itemId, 4, 0, 7, "used-stock"), user.Id);
        db.StockMovements.Add(new StockMovement(product.Id, warehouse.Id, StockMovementType.SaleOut,
            -1, 7, "TEST-SALE", Guid.NewGuid(), user.Id));
        (await db.Stocks.SingleAsync(x => x.ProductId == product.Id)).SetQuantity(3);
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<GoodsReceiptConflictException>(() => service.ReverseAsync(receipt.Id, user.Id, "Qaytar", CancellationToken.None));
        Assert.Null((await db.GoodsReceipts.SingleAsync(x => x.Id == receipt.Id)).ReversedAt);
        Assert.Equal(4m, (await db.PurchaseOrderItems.SingleAsync(x => x.Id == itemId)).ReceivedQty);
        Assert.Equal(3m, (await db.Stocks.SingleAsync(x => x.ProductId == product.Id)).Quantity);
    }

    [Fact]
    public async Task ReverseReceiptRollsBackWhenAuditFails()
    {
        await using var db = NewContext();
        var (user, supplier, warehouse, product) = await SeedAsync(db, 0);
        var (orderId, itemId) = await CreateApprovedOrder(db, user, supplier, warehouse, product, 4);
        var receipt = await GrnService(db).CreateAsync(Request(orderId, itemId, 4, 0, 7, "rollback-reverse"), user.Id);
        var service = GrnService(db, audit: new FailingAuditLogService());
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ReverseAsync(receipt.Id, user.Id, "Audit failure", CancellationToken.None));
        db.ChangeTracker.Clear();
        Assert.Null((await db.GoodsReceipts.SingleAsync(x => x.Id == receipt.Id)).ReversedAt);
        Assert.Equal(4m, (await db.Stocks.SingleAsync(x => x.ProductId == product.Id)).Quantity);
        Assert.Equal(0m, await db.StockMovements.Where(x => x.Type == StockMovementType.Reversal).SumAsync(x => x.Quantity));
        Assert.Equal(4m, (await db.PurchaseOrderItems.SingleAsync(x => x.Id == itemId)).ReceivedQty);
    }

    [Fact]
    public void ReverseRouteRequiresGrnReversePermission()
    {
        var attribute = typeof(GoodsReceiptsController).GetMethod(nameof(GoodsReceiptsController.Reverse))!
            .GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>().Single();
        Assert.Equal("grn.reverse", attribute.Policy);
    }

    [Fact]
    public async Task ProcurementReportsFilterOpenOverdueAndReceiptComparisons()
    {
        await using var db = NewContext();
        var (user, supplier, warehouse, product) = await SeedAsync(db, 0);
        var today = DateOnly.FromDateTime(DateTime.Today);
        var po = await PoService(db).CreateAsync(new SavePurchaseOrderRequest(supplier.Id, warehouse.Id,
            today.AddDays(-10), today.AddDays(-3), "AZN", null,
            [new PurchaseOrderLineRequest(product.Id, 10, 2)]), user.Id);
        await PoService(db).SubmitAsync(po.Id, user.Id);
        await PoService(db).ApproveAsync(po.Id, user.Id, true);
        var itemId = po.Items.Single().Id;
        await GrnService(db).CreateAsync(Request(po.Id, itemId, 3, 2, 4, "reports-test"), user.Id);

        var reports = new ProcurementReportService(db);
        var overdue = await reports.GetOverdueOrdersAsync(1, 10, today);
        var open = await reports.GetOpenOrdersAsync(1, 10);
        var comparison = await reports.GetReceiptComparisonAsync(1, 10, po.Id);
        var supplierAmount = await reports.GetSupplierReceiptAmountsAsync(1, 10, new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 1));
        Assert.Single(overdue.Items);
        Assert.Equal(3, overdue.Items.Single().DaysOverdue);
        Assert.Equal("partially_received", Assert.Single(open.Items).Status);
        Assert.Equal(3m, Assert.Single(comparison.Items).ReceivedQty);
        Assert.Equal(2m, comparison.Items.Single().RejectedQty);
        Assert.Equal(12m, Assert.Single(supplierAmount.Items).ReceivedAmount);
    }

    [Fact]
    public void GoodsReceiptCreateRouteRequiresGrnCreatePermission()
    {
        var attribute = typeof(GoodsReceiptsController).GetMethod(nameof(GoodsReceiptsController.Create))!
            .GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>().Single();
        Assert.Equal("grn.create", attribute.Policy);
    }

    private static GoodsReceiptService GrnService(StockManagementDbContext db, decimal tolerance = 0,
        IAuditLogService? audit = null)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Procurement:OverReceiptTolerancePercent"] = tolerance.ToString(System.Globalization.CultureInfo.InvariantCulture)
        }).Build();
        return new GoodsReceiptService(db, new StockService(new EfStockMovementRepository(db)),
            new DocumentNumberService(db), audit ?? new AuditLogService(db), configuration,
            new StockNotificationService(db));
    }

    [LocalSqlServerFact]
    public async Task SqlServerReceiptPostingIsAtomicIdempotentAndSerializesParallelUsers()
    {
        // Only a newly named test database is created/deleted. The configured database is never used.
        var databaseName = $"StockroomGrnTests_{Guid.NewGuid():N}";
        var connection = new SqlConnectionStringBuilder(Environment.GetEnvironmentVariable("STOCKMANAGEMENT_TEST_SQL"))
        {
            InitialCatalog = databaseName,
            Pooling = false
        };
        StockManagementDbContext Context() => new(new DbContextOptionsBuilder<StockManagementDbContext>()
            .UseSqlServer(connection.ConnectionString).Options);
        await using var db = Context();
        try
        {
            await db.Database.MigrateAsync();
            var (user, supplier, warehouse, product) = await SeedAsync(db, 0);
            var otherUser = new ApplicationUser { UserName = "parallel-user", FirstName = "İkinci", LastName = "İstifadəçi" };
            db.Users.Add(otherUser);
            await db.SaveChangesAsync();
            var order = await CreateApprovedOrder(db, user, supplier, warehouse, product, 100);
            Assert.Equal(0m, (await db.Stocks.SingleAsync()).Quantity);
            Assert.Empty(await db.StockMovements.ToArrayAsync());
            var failedPo = new PurchaseOrderService(db, new DocumentNumberService(db), new FailingAuditLogService());
            await Assert.ThrowsAsync<InvalidOperationException>(() => failedPo.CreateAsync(new SavePurchaseOrderRequest(
                supplier.Id, warehouse.Id, new DateOnly(2026, 1, 1), null, "AZN", null,
                [new PurchaseOrderLineRequest(product.Id, 1, 2)]), user.Id));
            db.ChangeTracker.Clear();
            Assert.Equal(1, await db.PurchaseOrders.CountAsync());
            var firstRequest = Request(order.OrderId, order.ItemId, 60, 0, 2, "sql-first");
            var first = await GrnService(db).CreateAsync(firstRequest, user.Id);
            Assert.Equal(60m, (await db.Stocks.SingleAsync()).Quantity);
            Assert.Equal("partially_received", (await PoService(db).GetAsync(order.OrderId))!.Status);
            var second = await GrnService(db).CreateAsync(Request(order.OrderId, order.ItemId, 40, 0, 2, "sql-last"), user.Id);
            Assert.Equal("received", (await PoService(db).GetAsync(order.OrderId))!.Status);
            Assert.Equal(100m, (await db.Stocks.SingleAsync()).Quantity);
            Assert.Equal(first.Id, (await GrnService(db).CreateAsync(firstRequest, user.Id)).Id);
            await Assert.ThrowsAsync<GoodsReceiptConflictException>(() => GrnService(db).CreateAsync(
                Request(order.OrderId, order.ItemId, 10, 0, 2, "sql-extra"), user.Id));
            await GrnService(db).ReverseAsync(first.Id, user.Id, "Birinci qəbulu geri qaytar");
            await Assert.ThrowsAsync<GoodsReceiptConflictException>(() => GrnService(db).ReverseAsync(first.Id, user.Id, "Təkrar"));
            await GrnService(db).ReverseAsync(second.Id, user.Id, "İkinci qəbulu geri qaytar");
            Assert.Equal(0m, (await db.Stocks.SingleAsync()).Quantity);

            var parallelOrder = await CreateApprovedOrder(db, user, supplier, warehouse, product, 100);
            var failing = new GoodsReceiptService(db, new StockService(new EfStockMovementRepository(db)),
                new DocumentNumberService(db), new AuditLogService(db), new ConfigurationBuilder().Build(),
                new FailingNotificationService());
            await Assert.ThrowsAsync<InvalidOperationException>(() => failing.CreateAsync(
                Request(parallelOrder.OrderId, parallelOrder.ItemId, 20, 0, 2, "sql-rollback"), user.Id));
            db.ChangeTracker.Clear();
            Assert.Equal(2, await db.GoodsReceipts.CountAsync());
            Assert.Equal(0m, await db.StockMovements.SumAsync(x => x.Quantity));
            Assert.Equal(0m, (await db.Stocks.SingleAsync()).Quantity);
            Assert.Equal(0m, (await db.PurchaseOrderItems.SingleAsync(x => x.Id == parallelOrder.ItemId)).ReceivedQty);
            Assert.Equal(PurchaseOrderStatus.Approved, (await db.PurchaseOrders.SingleAsync(x => x.Id == parallelOrder.OrderId)).Status);

            var attempts = await Task.WhenAll(new[] { user.Id, otherUser.Id }.Select(async actor =>
            {
                await using var context = Context();
                try
                {
                    await GrnService(context).CreateAsync(Request(parallelOrder.OrderId, parallelOrder.ItemId, 60, 0, 2, $"sql-parallel-{actor}"), actor);
                    return true;
                }
                catch (GoodsReceiptValidationException) { return false; }
            }));
            Assert.Single(attempts, x => x);
            db.ChangeTracker.Clear();
            Assert.Equal(60m, (await db.Stocks.SingleAsync()).Quantity);
            Assert.Equal(60m, (await db.PurchaseOrderItems.SingleAsync(x => x.Id == parallelOrder.ItemId)).ReceivedQty);

            var retryRequest = Request(parallelOrder.OrderId, parallelOrder.ItemId, 40, 0, 2, "sql-parallel-retry");
            var retries = await Task.WhenAll(new[] { user.Id, otherUser.Id }.Select(async actor =>
            {
                await using var context = Context();
                return await GrnService(context).CreateAsync(retryRequest, actor);
            }));
            Assert.Equal(retries[0].Id, retries[1].Id);
            db.ChangeTracker.Clear();
            Assert.Equal(100m, (await db.Stocks.SingleAsync()).Quantity);
            Assert.Equal(100m, await db.StockMovements.SumAsync(x => x.Quantity));
            Assert.Equal(4, await db.GoodsReceipts.CountAsync());
            var reversals = await Task.WhenAll(new[] { user.Id, otherUser.Id }.Select(async actor =>
            {
                await using var context = Context();
                try { await GrnService(context).ReverseAsync(retries[0].Id, actor, "Paralel geri qaytarma"); return true; }
                catch (GoodsReceiptConflictException) { return false; }
            }));
            Assert.Single(reversals, x => x);
            db.ChangeTracker.Clear();
            Assert.Equal(60m, (await db.Stocks.SingleAsync()).Quantity);
            Assert.Equal(60m, (await db.PurchaseOrderItems.SingleAsync(x => x.Id == parallelOrder.ItemId)).ReceivedQty);
            Assert.Equal(PurchaseOrderStatus.PartiallyReceived,
                (await db.PurchaseOrders.SingleAsync(x => x.Id == parallelOrder.OrderId)).Status);

            var separateOrderA = await CreateApprovedOrder(db, user, supplier, warehouse, product, 10);
            var separateOrderB = await CreateApprovedOrder(db, otherUser, supplier, warehouse, product, 10);
            var independentReceipts = await Task.WhenAll(new[]
            {
                (Order: separateOrderA, Actor: user.Id, Cost: 4m),
                (Order: separateOrderB, Actor: otherUser.Id, Cost: 8m)
            }.Select(async attempt =>
            {
                await using var context = Context();
                return await GrnService(context).CreateAsync(Request(attempt.Order.OrderId, attempt.Order.ItemId,
                    10, 0, attempt.Cost, $"sql-independent-{attempt.Order.OrderId}"), attempt.Actor);
            }));
            db.ChangeTracker.Clear();
            Assert.Equal(80m, (await db.Stocks.SingleAsync()).Quantity);
            Assert.Equal(3m, (await db.Products.SingleAsync()).AverageCost);
            var reasons = independentReceipts.Select(x => $"Mal qəbulu: {x.GrnNumber}").ToArray();
            var logBefore = await db.StockLogs.Where(x => reasons.Contains(x.Reason!)).OrderBy(x => x.QuantityBefore)
                .Select(x => x.QuantityBefore).ToArrayAsync();
            Assert.Equal(new[] { 60m, 70m }, logBefore);
        }
        finally
        {
            Assert.StartsWith("StockroomGrnTests_", databaseName);
            Assert.Equal(databaseName, db.Database.GetDbConnection().Database);
            await db.Database.EnsureDeletedAsync();
        }
    }

    private sealed class LocalSqlServerFactAttribute : FactAttribute
    {
        public LocalSqlServerFactAttribute()
        {
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("STOCKMANAGEMENT_TEST_SQL")))
                Skip = "SQL inteqrasiya testi üçün STOCKMANAGEMENT_TEST_SQL tələb olunur; yalnız yeni müvəqqəti test bazasına yazılır.";
        }
    }

    private sealed class FailingNotificationService : IStockNotificationService
    {
        public Task RefreshForProductAsync(Guid productId, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Injected failure after business data was saved inside the transaction.");
        public Task<IReadOnlyCollection<StockManagement.Application.DTOs.Notifications.StockNotificationDto>> GetAllAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<int> GetUnreadCountAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> MarkReadAsync(Guid id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task MarkAllReadAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    [Fact]
    public async Task HundredUnitOrderReceivesSixtyThenFortyWithoutOverReceiptOrDuplicatePosting()
    {
        await using var db = NewContext();
        var (user, supplier, warehouse, product) = await SeedAsync(db, 0);
        var (orderId, itemId) = await CreateApprovedOrder(db, user, supplier, warehouse, product, 100);
        Assert.Equal(0m, (await db.Stocks.SingleAsync()).Quantity);
        Assert.Empty(await db.StockMovements.ToArrayAsync());

        var service = GrnService(db);
        var firstRequest = Request(orderId, itemId, 60, 0, 2, "hundred-first");
        var first = await service.CreateAsync(firstRequest, user.Id);
        Assert.Equal(60m, (await db.Stocks.SingleAsync()).Quantity);
        Assert.Equal("partially_received", (await PoService(db).GetAsync(orderId))!.Status);
        Assert.Equal(40m, Assert.Single((await PoService(db).GetAsync(orderId))!.Items).RemainingQty);
        await service.CreateAsync(Request(orderId, itemId, 40, 0, 2, "hundred-last"), user.Id);
        Assert.Equal("received", (await PoService(db).GetAsync(orderId))!.Status);
        Assert.Equal(0m, Assert.Single((await PoService(db).GetAsync(orderId))!.Items).RemainingQty);
        Assert.Equal(first.Id, (await service.CreateAsync(firstRequest, user.Id)).Id);
        await Assert.ThrowsAsync<GoodsReceiptConflictException>(() => service.CreateAsync(
            Request(orderId, itemId, 10, 0, 2, "hundred-extra"), user.Id));
        db.ChangeTracker.Clear();
        Assert.Equal(100m, (await db.Stocks.SingleAsync()).Quantity);
        Assert.Equal(100m, await db.StockMovements.SumAsync(x => x.Quantity));
        Assert.Equal(2, await db.GoodsReceipts.CountAsync());
    }

    [Fact]
    public async Task SameIdempotencyKeyWithChangedQuantityOrOrderIsRejected()
    {
        await using var db = NewContext();
        var (user, supplier, warehouse, product) = await SeedAsync(db, 0);
        var firstOrder = await CreateApprovedOrder(db, user, supplier, warehouse, product, 10);
        var secondOrder = await CreateApprovedOrder(db, user, supplier, warehouse, product, 10);
        var service = GrnService(db);
        await service.CreateAsync(Request(firstOrder.OrderId, firstOrder.ItemId, 4, 0, 2, "same-key"), user.Id);
        await Assert.ThrowsAsync<GoodsReceiptConflictException>(() => service.CreateAsync(
            Request(firstOrder.OrderId, firstOrder.ItemId, 5, 0, 2, "same-key"), user.Id));
        await Assert.ThrowsAsync<GoodsReceiptConflictException>(() => service.CreateAsync(
            Request(secondOrder.OrderId, secondOrder.ItemId, 4, 0, 2, "same-key"), user.Id));
        Assert.Equal(4m, (await db.Stocks.SingleAsync()).Quantity);
        Assert.Equal(1, await db.GoodsReceipts.CountAsync());
        await Assert.ThrowsAsync<GoodsReceiptValidationException>(() => service.CreateAsync(
            Request(firstOrder.OrderId, firstOrder.ItemId, 1, 0, 2, "body-key"), user.Id, "header-key"));
    }

    [Fact]
    public async Task AverageCostUsesAllWarehousesAndReversalRestoresRemainingValue()
    {
        await using var db = NewContext();
        var (user, supplier, warehouse, product) = await SeedAsync(db, 100);
        var other = new Warehouse("İkinci anbar", "Gəncə");
        db.AddRange(other, new Stock(product.Id, other.Id, 200),
            new StockMovement(product.Id, other.Id, StockMovementType.OpeningBalance, 200, 5, "TEST", null, user.Id));
        await db.SaveChangesAsync();
        var order = await CreateApprovedOrder(db, user, supplier, warehouse, product, 100);
        var service = GrnService(db);
        var receipt = await service.CreateAsync(Request(order.OrderId, order.ItemId, 100, 0, 11, "global-cost"), user.Id);
        Assert.Equal(6.5m, product.AverageCost);
        await service.ReverseAsync(receipt.Id, user.Id, "Səhv qəbul");
        db.ChangeTracker.Clear();
        Assert.Equal(5m, (await db.Products.SingleAsync()).AverageCost);
        Assert.Equal(300m, await db.Stocks.SumAsync(x => x.Quantity));
    }

    [Theory]
    [InlineData("0.0001", "0", "2")]
    [InlineData("1", "0.0001", "2")]
    [InlineData("1", "0", "2.001")]
    public async Task ValuesThatWouldBeRoundedBySqlAreRejected(string received, string rejected, string cost)
    {
        await using var db = NewContext();
        var (user, supplier, warehouse, product) = await SeedAsync(db, 0);
        var order = await CreateApprovedOrder(db, user, supplier, warehouse, product, 10);
        var culture = System.Globalization.CultureInfo.InvariantCulture;
        await Assert.ThrowsAsync<GoodsReceiptValidationException>(() => GrnService(db).CreateAsync(
            Request(order.OrderId, order.ItemId, decimal.Parse(received, culture), decimal.Parse(rejected, culture),
                decimal.Parse(cost, culture), "precision-test"), user.Id));
        Assert.Empty(await db.GoodsReceipts.ToArrayAsync());
        Assert.Equal(0m, (await db.Stocks.SingleAsync()).Quantity);
    }

    [Fact]
    public async Task ReversingRejectedOnlyReceiptDoesNotReopenCompletedOrder()
    {
        await using var db = NewContext();
        var (user, supplier, warehouse, product) = await SeedAsync(db, 0);
        var order = await CreateApprovedOrder(db, user, supplier, warehouse, product, 100);
        var service = GrnService(db);
        var rejected = await service.CreateAsync(Request(order.OrderId, order.ItemId, 0, 2, 2, "rejected-before-full"), user.Id);
        await service.CreateAsync(Request(order.OrderId, order.ItemId, 100, 0, 2, "accepted-full"), user.Id);
        await service.ReverseAsync(rejected.Id, user.Id, "Qüsurlu qəbul qeydi səhv idi");
        Assert.Equal("received", (await PoService(db).GetAsync(order.OrderId))!.Status);
        Assert.Equal(100m, (await db.Stocks.SingleAsync()).Quantity);
    }

    [Fact]
    public async Task ReversalStillRemovesStockWhenProductWasSoftDeleted()
    {
        await using var db = NewContext();
        var (user, supplier, warehouse, product) = await SeedAsync(db, 0);
        var order = await CreateApprovedOrder(db, user, supplier, warehouse, product, 4);
        var service = GrnService(db);
        var receipt = await service.CreateAsync(Request(order.OrderId, order.ItemId, 4, 0, 2, "deleted-product"), user.Id);
        (await db.Products.SingleAsync()).MarkDeleted();
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        await service.ReverseAsync(receipt.Id, user.Id, "Səhv qəbul");
        Assert.Equal(0m, (await db.Stocks.SingleAsync()).Quantity);
        Assert.Single((await service.GetAsync(receipt.Id))!.Items);
    }

    [Fact]
    public async Task HiddenDeletedProductLineDoesNotMakeAnOrderLookFullyReceived()
    {
        await using var db = NewContext();
        var (user, supplier, warehouse, product) = await SeedAsync(db, 0);
        var otherProduct = new Product("İkinci məhsul", "GRN-SECOND", "", "", product.CategoryId, 1, "Piece", true);
        db.Products.Add(otherProduct);
        await db.SaveChangesAsync();
        var service = PoService(db);
        var order = await service.CreateAsync(new SavePurchaseOrderRequest(supplier.Id, warehouse.Id,
            new DateOnly(2026, 1, 1), null, "AZN", null,
            [new PurchaseOrderLineRequest(product.Id, 4, 2), new PurchaseOrderLineRequest(otherProduct.Id, 4, 2)]), user.Id);
        await service.SubmitAsync(order.Id, user.Id);
        await service.ApproveAsync(order.Id, user.Id, true);
        otherProduct.MarkDeleted();
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        await GrnService(db).CreateAsync(Request(order.Id, order.Items.Single(x => x.ProductId == product.Id).Id,
            4, 0, 2, "visible-product-only"), user.Id);
        Assert.Equal(PurchaseOrderStatus.PartiallyReceived, (await db.PurchaseOrders.SingleAsync()).Status);
        Assert.Equal(2, (await service.GetAsync(order.Id))!.Items.Count);
    }

    [Fact]
    public async Task ReversalThatLeavesToleranceOverReceiptDoesNotReopenCompletedOrder()
    {
        await using var db = NewContext();
        var (user, supplier, warehouse, product) = await SeedAsync(db, 0);
        var order = await CreateApprovedOrder(db, user, supplier, warehouse, product, 100);
        var service = GrnService(db, 5);
        var first = await service.CreateAsync(Request(order.OrderId, order.ItemId, 3, 0, 2, "tolerance-first"), user.Id);
        await service.CreateAsync(Request(order.OrderId, order.ItemId, 102, 0, 2, "tolerance-last"), user.Id);
        await service.ReverseAsync(first.Id, user.Id, "Kiçik qəbul geri qaytarılır");
        Assert.Equal("received", (await PoService(db).GetAsync(order.OrderId))!.Status);
        Assert.Equal(102m, (await db.Stocks.SingleAsync()).Quantity);
        Assert.Equal(0m, Assert.Single((await PoService(db).GetAsync(order.OrderId))!.Items).RemainingQty);
    }

    private static CreateGoodsReceiptRequest Request(Guid orderId, Guid itemId, decimal received, decimal rejected,
        decimal unitCost, string? key) => new(orderId, new DateOnly(2026, 7, 1), null, key,
        [new GoodsReceiptItemRequest(itemId, received, rejected, null, null, unitCost)]);

    private static PurchaseOrderService PoService(StockManagementDbContext db) =>
        new(db, new DocumentNumberService(db), new AuditLogService(db));

    private static async Task<(Guid OrderId, Guid ItemId)> CreateApprovedOrder(StockManagementDbContext db,
        ApplicationUser user, Supplier supplier, Warehouse warehouse, Product product, decimal quantity)
    {
        var created = await CreateDraftOrder(db, user, supplier, warehouse, product, quantity);
        var service = PoService(db);
        await service.SubmitAsync(created.OrderId, user.Id);
        await service.ApproveAsync(created.OrderId, user.Id, true);
        return created;
    }

    private static async Task<(Guid OrderId, Guid ItemId)> CreateDraftOrder(StockManagementDbContext db,
        ApplicationUser user, Supplier supplier, Warehouse warehouse, Product product, decimal quantity)
    {
        var po = await PoService(db).CreateAsync(new SavePurchaseOrderRequest(supplier.Id, warehouse.Id,
            new DateOnly(2026, 1, 1), null, "AZN", null,
            [new PurchaseOrderLineRequest(product.Id, quantity, 2)]), user.Id);
        return (po.Id, po.Items.Single().Id);
    }

    private static async Task<(ApplicationUser User, Supplier Supplier, Warehouse Warehouse, Product Product)> SeedAsync(
        StockManagementDbContext db, decimal initialStock)
    {
        var user = new ApplicationUser { UserName = "grn-user", FirstName = "GRN", LastName = "User" };
        var category = new Category("Test", "Test category");
        var product = new Product("Product", "GRN-TEST-001", "", "", category.Id, 1, "Piece", true);
        var warehouse = new Warehouse("Warehouse", "Baku");
        var supplier = new Supplier("Supplier", "050", "grn-supplier@example.az", "Baku", "1234509876");
        db.AddRange(user, category, product, warehouse, supplier, new Stock(product.Id, warehouse.Id, initialStock));
        await db.SaveChangesAsync();
        if (initialStock > 0)
            db.StockMovements.Add(new StockMovement(product.Id, warehouse.Id, StockMovementType.OpeningBalance,
                initialStock, 5, "TEST", Guid.NewGuid(), user.Id));
        await db.SaveChangesAsync();
        product.SetAverageCost(5);
        await db.SaveChangesAsync();
        return (user, supplier, warehouse, product);
    }

    private static StockManagementDbContext NewContext(string? name = null) => new(new DbContextOptionsBuilder<StockManagementDbContext>()
        .UseInMemoryDatabase(name ?? Guid.NewGuid().ToString()).Options);

    private sealed class FailingAuditLogService : IAuditLogService
    {
        public Task WriteAsync(Guid userId, string action, string entityType, Guid entityId,
            object? oldValues = null, object? newValues = null, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Injected audit failure.");
    }
}
