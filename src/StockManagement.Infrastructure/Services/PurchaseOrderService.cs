using Microsoft.EntityFrameworkCore;
using System.Data;
using StockManagement.Application.Abstractions;
using StockManagement.Application.DTOs.PurchaseOrders;
using StockManagement.Application.DTOs.GoodsReceipts;
using StockManagement.Domain.Entities;
using StockManagement.Domain.Enums;
using StockManagement.Infrastructure.Persistence;

namespace StockManagement.Infrastructure.Services;

public sealed class PurchaseOrderService(StockManagementDbContext db, DocumentNumberService numbers,
    IAuditLogService audit)
{
    public const int DefaultPageSize = 10;
    public const int MaximumPageSize = 10;

    public async Task<IReadOnlyCollection<PurchaseOrderOption>> GetSupplierFiltersAsync(CancellationToken cancellationToken = default) =>
        await db.Suppliers.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.Name)
            .Select(x => new PurchaseOrderOption(x.Id, x.Name, null)).ToArrayAsync(cancellationToken);

    public async Task<PurchaseOrderOptions> GetOptionsAsync(CancellationToken cancellationToken = default) => new(
        await db.Suppliers.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.Name)
            .Select(x => new PurchaseOrderOption(x.Id, x.Name, null)).ToArrayAsync(cancellationToken),
        await db.Warehouses.AsNoTracking().OrderBy(x => x.Name)
            .Select(x => new PurchaseOrderOption(x.Id, x.Name, null)).ToArrayAsync(cancellationToken),
        await db.Products.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.Name)
            .Select(x => new PurchaseOrderOption(x.Id, x.Name, x.Sku)).ToArrayAsync(cancellationToken));

    public async Task<PurchaseOrderPage> GetPageAsync(int page, int size, string? status, Guid? supplierId,
        DateOnly? fromDate, DateOnly? toDate, CancellationToken cancellationToken = default)
    {
        size = size < 1 ? DefaultPageSize : Math.Min(size, MaximumPageSize);
        var query = db.PurchaseOrders.AsNoTracking().Include(x => x.Supplier).Include(x => x.Warehouse)
            .Include(x => x.Items).ThenInclude(x => x.Product)
            .Include(x => x.GoodsReceipts).ThenInclude(x => x.Items).AsQueryable();
        if (!string.IsNullOrWhiteSpace(status))
        {
            var parsedStatus = Enum.GetValues<PurchaseOrderStatus>()
                .Single(x => PurchaseOrderStatusCodes.ToStorage(x).Equals(status, StringComparison.OrdinalIgnoreCase));
            query = query.Where(x => x.Status == parsedStatus);
        }
        if (supplierId.HasValue) query = query.Where(x => x.SupplierId == supplierId.Value);
        if (fromDate.HasValue) query = query.Where(x => x.OrderDate >= fromDate.Value);
        if (toDate.HasValue) query = query.Where(x => x.OrderDate <= toDate.Value);
        var count = await query.CountAsync(cancellationToken);
        var pages = (int)Math.Ceiling(count / (double)size);
        page = pages == 0 ? 1 : Math.Clamp(page, 1, pages);
        var orders = await query.OrderByDescending(x => x.OrderDate).ThenBy(x => x.PoNumber)
            .Skip((page - 1) * size).Take(size).ToListAsync(cancellationToken);
        return new(orders.Select(ToDto).ToArray(), page, size, count, pages, page < pages, page > 1);
    }

    public async Task<PurchaseOrderDto?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var order = await Query().SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        return order is null ? null : ToDto(order);
    }

    public async Task<PurchaseOrderDto> CreateAsync(SavePurchaseOrderRequest request, Guid userId,
        CancellationToken cancellationToken = default)
    {
        var lines = ValidateLines(request.Items);
        await ValidateReferences(request.SupplierId, request.WarehouseId, lines.Select(x => x.ProductId), cancellationToken);
        var order = new PurchaseOrder(await numbers.GetNextAsync("PO", request.OrderDate.Year, cancellationToken),
            request.SupplierId, request.WarehouseId, request.OrderDate, request.ExpectedDate,
            request.Currency ?? "AZN", request.Note, userId);
        order.ReplaceItems(lines);
        db.PurchaseOrders.Add(order);
        await db.SaveChangesAsync(cancellationToken);
        var dto = ToDto(await Query().SingleAsync(x => x.Id == order.Id, cancellationToken));
        await audit.WriteAsync(userId, "create", "purchase_order", order.Id, newValues: dto, cancellationToken: cancellationToken);
        return dto;
    }

    public async Task<PurchaseOrderDto> UpdateAsync(Guid id, SavePurchaseOrderRequest request, Guid userId,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken)
            : null;
        await LockPurchaseOrderAsync(id, cancellationToken);
        var order = await Query().SingleOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException("PO tapılmadı.");
        var lines = ValidateLines(request.Items);
        await ValidateReferences(request.SupplierId, request.WarehouseId, lines.Select(x => x.ProductId), cancellationToken);
        var oldValues = ToDto(order);
        order.UpdateDraft(request.SupplierId, request.WarehouseId, request.OrderDate, request.ExpectedDate,
            request.Currency ?? "AZN", request.Note);
        order.ReplaceItems(lines);
        await db.SaveChangesAsync(cancellationToken);
        var updated = ToDto(order);
        await audit.WriteAsync(userId, "update", "purchase_order", id, oldValues, updated, cancellationToken);
        if (transaction is not null) await transaction.CommitAsync(cancellationToken);
        return updated;
    }

    public Task<PurchaseOrderDto> SubmitAsync(Guid id, Guid userId, CancellationToken cancellationToken = default) =>
        ChangeStatusAsync(id, PurchaseOrderStatus.PendingApproval, userId, cancellationToken: cancellationToken);

    public Task<PurchaseOrderDto> ApproveAsync(Guid id, Guid userId, bool allowSelfApproval,
        CancellationToken cancellationToken = default) =>
        ChangeStatusAsync(id, PurchaseOrderStatus.Approved, userId, allowSelfApproval, cancellationToken: cancellationToken);

    public Task<PurchaseOrderDto> CancelAsync(Guid id, Guid userId, string? reason,
        CancellationToken cancellationToken = default) =>
        ChangeStatusAsync(id, PurchaseOrderStatus.Cancelled, userId, cancelReason: reason, cancellationToken: cancellationToken);

    private async Task<PurchaseOrderDto> ChangeStatusAsync(Guid id, PurchaseOrderStatus target, Guid userId,
        bool allowSelfApproval = false, string? cancelReason = null, CancellationToken cancellationToken = default)
    {
        await using var transaction = db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken)
            : null;
        await LockPurchaseOrderAsync(id, cancellationToken);
        var order = await Query().SingleOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException("PO tapılmadı.");
        var oldStatus = PurchaseOrderStatusCodes.ToStorage(order.Status);
        order.TransitionTo(target, userId, cancelReason, allowSelfApproval);
        await db.SaveChangesAsync(cancellationToken);
        var dto = ToDto(order);
        await audit.WriteAsync(userId, "status-change", "purchase_order", id,
            new { status = oldStatus }, new { status = PurchaseOrderStatusCodes.ToStorage(target) }, cancellationToken);
        if (transaction is not null) await transaction.CommitAsync(cancellationToken);
        return dto;
    }

    private async Task LockPurchaseOrderAsync(Guid id, CancellationToken cancellationToken)
    {
        if (!db.Database.IsSqlServer()) return;
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT [id] FROM [dbo].[purchase_orders] WITH (UPDLOCK, HOLDLOCK) WHERE [id] = {id}", cancellationToken);
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT [id] FROM [dbo].[purchase_order_items] WITH (UPDLOCK, HOLDLOCK) WHERE [purchase_order_id] = {id}", cancellationToken);
    }

    private IQueryable<PurchaseOrder> Query() => db.PurchaseOrders.Include(x => x.Supplier).Include(x => x.Warehouse)
        .Include(x => x.Items).ThenInclude(x => x.Product)
        .Include(x => x.GoodsReceipts).ThenInclude(x => x.Items);

    private async Task ValidateReferences(Guid supplierId, Guid warehouseId, IEnumerable<Guid> productIds,
        CancellationToken cancellationToken)
    {
        var supplier = await db.Suppliers.SingleOrDefaultAsync(x => x.Id == supplierId, cancellationToken);
        if (supplier is null || !supplier.IsActive) throw new ArgumentException("Aktiv təchizatçı seçilməlidir.");
        if (!await db.Warehouses.AnyAsync(x => x.Id == warehouseId, cancellationToken))
            throw new ArgumentException("Anbar tapılmadı.");
        var ids = productIds.Distinct().ToArray();
        if (await db.Products.CountAsync(x => ids.Contains(x.Id) && x.IsActive, cancellationToken) != ids.Length)
            throw new ArgumentException("Bütün sətirlərdə aktiv məhsul seçilməlidir.");
    }

    private static (Guid ProductId, decimal Quantity, decimal UnitPrice)[] ValidateLines(
        IReadOnlyCollection<PurchaseOrderLineRequest>? items)
    {
        if (items is null || items.Count == 0) throw new ArgumentException("PO-da ən azı bir sətir olmalıdır.");
        return items.Select(x => (x.ProductId, x.OrderedQty, x.UnitPrice)).ToArray();
    }

    private static PurchaseOrderDto ToDto(PurchaseOrder order) => new(order.Id, order.PoNumber,
        order.SupplierId, order.Supplier.Name, order.Supplier.Phone, order.Supplier.Email, order.Supplier.Address,
        order.Supplier.TaxId, order.WarehouseId, order.Warehouse.Name,
        PurchaseOrderStatusCodes.ToStorage(order.Status), order.OrderDate, order.ExpectedDate, order.Currency,
        order.Items.Sum(x => decimal.Round(x.OrderedQty * x.UnitPrice, 2, MidpointRounding.AwayFromZero)),
        order.Note, order.CreatedBy, order.ApprovedBy, order.ApprovedAt, order.CancelledBy, order.CancelledAt,
        order.CancelReason, order.CreatedAt, order.UpdatedAt,
        order.Items.OrderBy(x => x.Product.Name).Select(x => new PurchaseOrderItemDto(x.Id, x.ProductId,
            x.Product.Name, x.Product.Sku, x.OrderedQty, x.ReceivedQty, x.OrderedQty - x.ReceivedQty,
            x.UnitPrice, decimal.Round(x.OrderedQty * x.UnitPrice, 2, MidpointRounding.AwayFromZero))).ToArray(),
        order.GoodsReceipts.OrderBy(x => x.ReceivedDate).Select(x => new GoodsReceiptSummaryDto(x.Id, x.GrnNumber,
            x.ReceivedDate, x.Items.Sum(i => i.ReceivedQty), x.Items.Sum(i => i.RejectedQty))).ToArray());
}
