using System.Data;
using System.Collections.Concurrent;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using StockManagement.Application.Abstractions;
using StockManagement.Application.DTOs.GoodsReceipts;
using StockManagement.Application.Products;
using StockManagement.Domain.Entities;
using StockManagement.Domain.Enums;
using StockManagement.Infrastructure.Persistence;

namespace StockManagement.Infrastructure.Services;

public sealed class GoodsReceiptValidationException(string message) : Exception(message);
public sealed class GoodsReceiptConflictException(string message) : Exception(message);
public sealed class GoodsReceiptNotReversibleException(string message) : Exception(message);

public sealed class GoodsReceiptService(StockManagementDbContext db, StockService stockService,
    DocumentNumberService numbers, IAuditLogService audit, IConfiguration configuration,
    IStockNotificationService stockNotifications, StockLogService stockLogs)
{
    public GoodsReceiptService(StockManagementDbContext db, StockService stockService,
        DocumentNumberService numbers, IAuditLogService audit, IConfiguration configuration,
        IStockNotificationService stockNotifications)
        : this(db, stockService, numbers, audit, configuration, stockNotifications, new StockLogService(db)) { }

    private static readonly ConcurrentDictionary<Guid, SemaphoreSlim> PurchaseOrderLocks = new();
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> IdempotencyLocks = new(StringComparer.Ordinal);
    public const int DefaultPageSize = 10;
    public const int MaximumPageSize = 10;

    public async Task<GoodsReceiptDto> CreateAsync(CreateGoodsReceiptRequest request, Guid receivedBy,
        string? headerIdempotencyKey = null, CancellationToken cancellationToken = default)
    {
        var rawKey = string.IsNullOrWhiteSpace(headerIdempotencyKey) ? request.IdempotencyKey : headerIdempotencyKey;
        var key = string.IsNullOrWhiteSpace(rawKey) ? null : rawKey.Trim();
        if (key?.Length > 128) throw new GoodsReceiptValidationException("Idempotency key ən çox 128 simvol ola bilər.");
        ValidateRequest(request);
        SemaphoreSlim? orderGate = null;
        var isSqlServer = db.Database.IsSqlServer();
        if (!isSqlServer)
        {
            orderGate = PurchaseOrderLocks.GetOrAdd(request.PurchaseOrderId, _ => new SemaphoreSlim(1, 1));
            await orderGate.WaitAsync(cancellationToken);
        }
        SemaphoreSlim? idempotencyGate = null;
        var idempotencyLockAcquired = false;

        try
        {
            if (key is not null && !isSqlServer)
            {
                idempotencyGate = IdempotencyLocks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
                await idempotencyGate.WaitAsync(cancellationToken);
                idempotencyLockAcquired = true;
            }
            try
            {
            await using var transaction = db.Database.IsRelational()
                ? await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken)
                : null;

            await LockPurchaseOrderAsync(request.PurchaseOrderId, cancellationToken);

            if (key is not null)
            {
                var prior = await FindByIdempotencyKeyAsync(key, cancellationToken);
                if (prior is not null)
                {
                    if (transaction is not null) await transaction.RollbackAsync(cancellationToken);
                    return await ToDtoAsync(prior, cancellationToken);
                }
            }

            var order = await db.PurchaseOrders.Include(x => x.Warehouse).Include(x => x.Items).ThenInclude(x => x.Product)
                .SingleOrDefaultAsync(x => x.Id == request.PurchaseOrderId, cancellationToken)
                ?? throw new KeyNotFoundException("Satınalma sifarişi tapılmadı.");
            if (order.Status is not (PurchaseOrderStatus.Approved or PurchaseOrderStatus.PartiallyReceived))
                throw new GoodsReceiptConflictException($"{order.PoNumber} üçün GRN yalnız approved və ya partially_received statusunda yaradıla bilər.");

            var lines = request.Items!;
            if (lines.Select(x => x.PurchaseOrderItemId).Distinct().Count() != lines.Count)
                throw new GoodsReceiptValidationException("Eyni PO sətri GRN-də təkrar göstərilə bilməz.");
            var itemsById = order.Items.ToDictionary(x => x.Id);
            foreach (var line in lines)
            {
                if (!itemsById.TryGetValue(line.PurchaseOrderItemId, out var poItem))
                    throw new GoodsReceiptValidationException("GRN sətri bu PO-ya aid deyil.");
                var tolerance = configuration.GetValue<decimal?>("Procurement:OverReceiptTolerancePercent") ?? 0m;
                if (tolerance < 0) throw new InvalidOperationException("Procurement:OverReceiptTolerancePercent mənfi ola bilməz.");
                var remaining = poItem.OrderedQty - poItem.ReceivedQty;
                var allowed = remaining + poItem.OrderedQty * tolerance / 100m;
                if (line.ReceivedQty > allowed)
                    throw new GoodsReceiptValidationException($"{poItem.Product.Name} məhsulu üçün qalıq {remaining:0.###}-dır, siz {line.ReceivedQty:0.###} qəbul etməyə çalışdınız. Toleransla icazə verilən maksimum {allowed:0.###}-dır.");
            }

            var productIds = lines.Where(x => x.ReceivedQty > 0).Select(x => itemsById[x.PurchaseOrderItemId].ProductId)
                .Distinct().OrderBy(x => x).ToArray();
            foreach (var productId in productIds) await LockProductAndStockAsync(productId, order.WarehouseId, cancellationToken);

            var products = await db.Products.Where(x => productIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, cancellationToken);
            foreach (var productId in productIds)
                await db.Entry(products[productId]).ReloadAsync(cancellationToken);
            var stocks = await db.Stocks.Where(x => productIds.Contains(x.ProductId) && x.WarehouseId == order.WarehouseId)
                .ToDictionaryAsync(x => x.ProductId, cancellationToken);
            var totalStocks = await db.Stocks.Where(x => productIds.Contains(x.ProductId)).GroupBy(x => x.ProductId)
                .Select(group => new { ProductId = group.Key, Quantity = group.Sum(x => x.Quantity) })
                .ToDictionaryAsync(x => x.ProductId, x => x.Quantity, cancellationToken);
            foreach (var productId in productIds)
            {
                var ledgerStock = await stockService.GetStock(productId, order.WarehouseId, cancellationToken);
                var cachedStock = stocks.TryGetValue(productId, out var existingStock) ? existingStock.Quantity : 0m;
                if (ledgerStock != cachedStock)
                    throw new GoodsReceiptConflictException($"{products[productId].Name} üçün cache stok ({cachedStock:0.###}) ilə stock_movements cəmi ({ledgerStock:0.###}) uyğun deyil. Əvvəl reconciliation/opening balance işlədin.");
            }

            var oldStatus = PurchaseOrderStatusCodes.ToStorage(order.Status);
            var grnNumber = await numbers.GetNextAsync("GRN", request.ReceivedDate.Year, cancellationToken);
            var receipt = new GoodsReceipt(grnNumber,
                order.Id, order.WarehouseId, request.ReceivedDate, receivedBy, request.Note, key);
            foreach (var line in lines)
            {
                var poItem = itemsById[line.PurchaseOrderItemId];
                var receiptItem = new GoodsReceiptItem(receipt.Id, poItem.Id, poItem.ProductId, line.ReceivedQty,
                    line.RejectedQty, line.BatchNo, line.ExpiryDate, line.UnitCost);
                receipt.Items.Add(receiptItem);

                if (line.ReceivedQty <= 0) continue;
                poItem.RecordReceived(line.ReceivedQty);
                var product = products[poItem.ProductId];
                var currentStock = await stockService.GetStock(product.Id, order.WarehouseId, cancellationToken);
                var quantityBefore = totalStocks.GetValueOrDefault(product.Id);
                var newAverageCost = currentStock <= 0
                    ? line.UnitCost
                    : ((currentStock * product.AverageCost) + (line.ReceivedQty * line.UnitCost)) / (currentStock + line.ReceivedQty);
                product.SetAverageCost(newAverageCost);
                if (stocks.TryGetValue(product.Id, out var stock)) stock.Increase(line.ReceivedQty);
                else
                {
                    stock = new Stock(product.Id, order.WarehouseId, line.ReceivedQty);
                    db.Stocks.Add(stock);
                    stocks[product.Id] = stock;
                }
                stockLogs.Record(product.Id, product.Name, StockLogType.IN, quantityBefore,
                    quantityBefore + line.ReceivedQty, $"Mal qəbulu: {grnNumber}", receivedBy);
                totalStocks[product.Id] = quantityBefore + line.ReceivedQty;
                db.StockMovements.Add(new StockMovement(product.Id, order.WarehouseId,
                    StockMovementType.PurchaseIn, line.ReceivedQty, line.UnitCost, "GRN", receipt.Id, receivedBy));
            }

            var allReceived = order.Items.All(x => x.ReceivedQty >= x.OrderedQty);
            var anyReceived = order.Items.Any(x => x.ReceivedQty > 0);
            if (allReceived)
            {
                if (order.Status == PurchaseOrderStatus.Approved) order.MarkPartiallyReceived();
                if (order.Status == PurchaseOrderStatus.PartiallyReceived) order.MarkReceived();
            }
            else if (anyReceived && order.Status == PurchaseOrderStatus.Approved)
                order.MarkPartiallyReceived();

            db.GoodsReceipts.Add(receipt);
            var newStatus = PurchaseOrderStatusCodes.ToStorage(order.Status);
            await audit.WriteAsync(receivedBy, "goods-receipt-created", "goods_receipt", receipt.Id,
                new { purchaseOrderId = order.Id, poStatus = oldStatus },
                new { grnNumber = receipt.GrnNumber, purchaseOrderId = order.Id, poStatus = newStatus }, cancellationToken);
            var saved = await ReceiptQuery().SingleAsync(x => x.Id == receipt.Id, cancellationToken);
            var result = await ToDtoAsync(saved, cancellationToken);
            foreach (var productId in productIds)
                await stockNotifications.RefreshForProductAsync(productId, cancellationToken);
            if (transaction is not null) await transaction.CommitAsync(cancellationToken);
            return result;
            }
            catch (DbUpdateException exception) when (key is not null && IsUniqueViolation(exception))
            {
                db.ChangeTracker.Clear();
                var existing = await FindByIdempotencyKeyAsync(key, cancellationToken);
                if (existing is not null) return await ToDtoAsync(existing, cancellationToken);
                throw;
            }
        }
        finally
        {
            if (idempotencyLockAcquired) idempotencyGate!.Release();
            orderGate?.Release();
        }
    }

    public async Task<GoodsReceiptDto?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var receipt = await ReceiptQuery().SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        return receipt is null ? null : await ToDtoAsync(receipt, cancellationToken);
    }

    public async Task<GoodsReceiptDto> ReverseAsync(Guid id, Guid userId, string? reason,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty || string.IsNullOrWhiteSpace(reason))
            throw new GoodsReceiptValidationException("Geri qaytarma səbəbi məcburidir.");
        if (reason.Trim().Length > 1000)
            throw new GoodsReceiptValidationException("Geri qaytarma səbəbi ən çox 1000 simvol ola bilər.");
        await using var transaction = db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken)
            : null;
        var header = await db.GoodsReceipts.AsNoTracking().Where(x => x.Id == id)
            .Select(x => new { x.PurchaseOrderId }).SingleOrDefaultAsync(cancellationToken)
            ?? throw new KeyNotFoundException("GRN tapılmadı.");
        await LockPurchaseOrderAsync(header.PurchaseOrderId, cancellationToken);
        var receipt = await ReceiptQuery().Include(x => x.PurchaseOrder).ThenInclude(x => x.Items)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException("GRN tapılmadı.");
        if (receipt.ReversedAt.HasValue) throw new GoodsReceiptConflictException("Bu GRN artıq geri qaytarılıb.");
        var order = receipt.PurchaseOrder;
        var movements = await db.StockMovements.Include(x => x.Product).Where(x => x.ReferenceType == "GRN" && x.ReferenceId == id &&
            x.Type == StockMovementType.PurchaseIn).OrderBy(x => x.ProductId).ToListAsync(cancellationToken);
        var movementProductIds = movements.Select(movement => movement.ProductId).Distinct().ToArray();
        var totalStocks = await db.Stocks.Where(x => movementProductIds.Contains(x.ProductId))
            .GroupBy(x => x.ProductId).Select(group => new { ProductId = group.Key, Quantity = group.Sum(x => x.Quantity) })
            .ToDictionaryAsync(x => x.ProductId, x => x.Quantity, cancellationToken);
        var oldStatus = PurchaseOrderStatusCodes.ToStorage(order.Status);
        foreach (var movement in movements)
        {
            await LockProductAndStockAsync(movement.ProductId, receipt.WarehouseId, cancellationToken);
            var current = await stockService.GetStock(movement.ProductId, receipt.WarehouseId, cancellationToken);
            var cache = await db.Stocks.SingleOrDefaultAsync(x => x.ProductId == movement.ProductId && x.WarehouseId == receipt.WarehouseId, cancellationToken);
            if (current != (cache?.Quantity ?? 0m))
                throw new GoodsReceiptConflictException($"{movement.Product.Name} məhsulu üçün cache stokla stock_movements cəmi uyğun deyil.");
            if (current < movement.Quantity)
                throw new GoodsReceiptConflictException($"{movement.Product.Name} üzrə GRN-i geri qaytarmaq mümkün deyil: {movement.Quantity:0.###} ədəd geri alınmalıdır, mövcud stok isə {current:0.###}-dır. Məhsulun bir hissəsi artıq istifadə olunub.");
            var quantityBefore = totalStocks.GetValueOrDefault(movement.ProductId);
            if (cache is not null) cache.SetQuantity(cache.Quantity - movement.Quantity);
            stockLogs.Record(movement.ProductId, movement.Product.Name, StockLogType.OUT, quantityBefore,
                quantityBefore - movement.Quantity, $"Mal qəbulu geri qaytarıldı: {receipt.GrnNumber}", userId);
            totalStocks[movement.ProductId] = quantityBefore - movement.Quantity;
            db.StockMovements.Add(new StockMovement(movement.ProductId, movement.WarehouseId,
                StockMovementType.Reversal, -movement.Quantity, movement.UnitCost, "GRN_REVERSAL", movement.Id, userId));
            var item = receipt.Items.Single(x => x.ProductId == movement.ProductId && x.ReceivedQty == movement.Quantity);
            var poItem = order.Items.Single(x => x.Id == item.PurchaseOrderItemId);
            poItem.ReverseReceived(item.ReceivedQty);
        }
        if (order.Status is PurchaseOrderStatus.Received or PurchaseOrderStatus.PartiallyReceived)
            order.ReopenAfterReceiptReversal();
        receipt.MarkReversed(userId, reason);
        await audit.WriteAsync(userId, "goods-receipt-reversed", "goods_receipt", id,
            new { poStatus = oldStatus, reversed = false },
            new { poStatus = PurchaseOrderStatusCodes.ToStorage(order.Status), reversed = true, reason = reason.Trim() }, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        foreach (var productId in movements.Select(x => x.ProductId).Distinct())
            await stockNotifications.RefreshForProductAsync(productId, cancellationToken);
        if (transaction is not null) await transaction.CommitAsync(cancellationToken);
        return await ToDtoAsync(await ReceiptQuery().AsNoTracking().SingleAsync(x => x.Id == id, cancellationToken), cancellationToken);
    }

    public async Task<GoodsReceiptPage> GetPageAsync(int page, int size, Guid? purchaseOrderId,
        DateOnly? fromDate, DateOnly? toDate, CancellationToken cancellationToken = default)
    {
        size = size < 1 ? DefaultPageSize : Math.Min(size, MaximumPageSize);
        var query = ReceiptQuery().AsNoTracking();
        if (purchaseOrderId.HasValue) query = query.Where(x => x.PurchaseOrderId == purchaseOrderId.Value);
        if (fromDate.HasValue) query = query.Where(x => x.ReceivedDate >= fromDate.Value);
        if (toDate.HasValue) query = query.Where(x => x.ReceivedDate <= toDate.Value);
        var count = await query.CountAsync(cancellationToken);
        var totalPages = (int)Math.Ceiling(count / (double)size);
        page = totalPages == 0 ? 1 : Math.Clamp(page, 1, totalPages);
        var rows = await query.OrderByDescending(x => x.ReceivedDate).ThenBy(x => x.GrnNumber)
            .Skip((page - 1) * size).Take(size).ToArrayAsync(cancellationToken);
        var receiverIds = rows.Select(x => x.ReceivedBy).Distinct().ToArray();
        var receiverNames = await db.Users.Where(x => receiverIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.FirstName + " " + x.LastName, cancellationToken);
        return new(rows.Select(x => ToDto(x, receiverNames.GetValueOrDefault(x.ReceivedBy, x.ReceivedBy.ToString()))).ToArray(),
            page, size, count, totalPages, page < totalPages, page > 1);
    }

    private IQueryable<GoodsReceipt> ReceiptQuery() => db.GoodsReceipts.Include(x => x.PurchaseOrder)
        .Include(x => x.Warehouse).Include(x => x.Items).ThenInclude(x => x.Product);

    private Task<GoodsReceipt?> FindByIdempotencyKeyAsync(string key, CancellationToken cancellationToken) =>
        ReceiptQuery().AsNoTracking().SingleOrDefaultAsync(x => x.IdempotencyKey == key, cancellationToken);

    private async Task LockPurchaseOrderAsync(Guid purchaseOrderId, CancellationToken cancellationToken)
    {
        if (!db.Database.IsSqlServer()) return;
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT [id] FROM [dbo].[purchase_orders] WITH (UPDLOCK, HOLDLOCK) WHERE [id] = {purchaseOrderId}", cancellationToken);
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT [id] FROM [dbo].[purchase_order_items] WITH (UPDLOCK, HOLDLOCK) WHERE [purchase_order_id] = {purchaseOrderId}", cancellationToken);
    }

    private async Task LockProductAndStockAsync(Guid productId, Guid warehouseId, CancellationToken cancellationToken)
    {
        if (!db.Database.IsSqlServer()) return;
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT [Id] FROM [dbo].[Products] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {productId}", cancellationToken);
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT [Id] FROM [dbo].[Stocks] WITH (UPDLOCK, HOLDLOCK) WHERE [ProductId] = {productId} AND [WarehouseId] = {warehouseId}", cancellationToken);
    }

    private static void ValidateRequest(CreateGoodsReceiptRequest request)
    {
        if (request.PurchaseOrderId == Guid.Empty) throw new GoodsReceiptValidationException("PO seçilməlidir.");
        if (request.Items is null || request.Items.Count == 0) throw new GoodsReceiptValidationException("GRN ən azı bir sətir daşımalıdır.");
        if (request.Items.Any(x => x.ReceivedQty < 0 || x.RejectedQty < 0 || x.UnitCost < 0 || x.ReceivedQty + x.RejectedQty <= 0))
            throw new GoodsReceiptValidationException("Qəbul və qüsurlu miqdar mənfi ola bilməz, vahid maya dəyəri də mənfi ola bilməz; sətirdə ən azı bir miqdar müsbət olmalıdır.");
    }

    private static bool IsUniqueViolation(DbUpdateException exception) =>
        exception.GetBaseException() is SqlException { Number: 2601 or 2627 };

    private async Task<GoodsReceiptDto> ToDtoAsync(GoodsReceipt receipt, CancellationToken cancellationToken)
    {
        var receiverName = await db.Users.Where(x => x.Id == receipt.ReceivedBy)
            .Select(x => x.FirstName + " " + x.LastName).SingleOrDefaultAsync(cancellationToken) ?? receipt.ReceivedBy.ToString();
        return ToDto(receipt, receiverName);
    }

    private static GoodsReceiptDto ToDto(GoodsReceipt receipt, string receiverName) => new(receipt.Id, receipt.GrnNumber,
        receipt.PurchaseOrderId, receipt.PurchaseOrder.PoNumber, receipt.WarehouseId, receipt.Warehouse.Name,
        receipt.ReceivedDate, receipt.ReceivedBy, receiverName, receipt.Note, receipt.IdempotencyKey, receipt.CreatedAt,
        receipt.Items.OrderBy(x => x.Product.Name).Select(x => new GoodsReceiptItemDto(x.Id,
            x.PurchaseOrderItemId, x.ProductId, x.Product.Name, x.ReceivedQty, x.RejectedQty,
            x.BatchNo, x.ExpiryDate, x.UnitCost)).ToArray(), receipt.ReversedAt, receipt.ReversedBy, receipt.ReverseReason);
}
