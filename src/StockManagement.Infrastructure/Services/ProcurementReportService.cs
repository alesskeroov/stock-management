using Microsoft.EntityFrameworkCore;
using StockManagement.Application.DTOs.PurchaseOrders;
using StockManagement.Domain.Enums;
using StockManagement.Infrastructure.Persistence;

namespace StockManagement.Infrastructure.Services;

public sealed class ProcurementReportService(StockManagementDbContext db)
{
    public Task<ProcurementReportPage<OpenPurchaseOrderReport>> GetOpenOrdersAsync(int page, int size, CancellationToken ct = default)
    {
        var query = db.PurchaseOrders.AsNoTracking().Where(x => x.Status == PurchaseOrderStatus.Approved || x.Status == PurchaseOrderStatus.PartiallyReceived)
            .OrderBy(x => x.ExpectedDate).ThenBy(x => x.PoNumber)
            .Select(x => new OpenPurchaseOrderReport(x.Id, x.PoNumber, x.Supplier.Name, x.Warehouse.Name,
                x.Status == PurchaseOrderStatus.Approved ? "approved" : "partially_received", x.OrderDate, x.ExpectedDate, x.TotalAmount));
        return PageAsync(query, page, size, ct);
    }

    public Task<ProcurementReportPage<OverduePurchaseOrderReport>> GetOverdueOrdersAsync(int page, int size,
        DateOnly today, CancellationToken ct = default)
    {
        var query = db.PurchaseOrders.AsNoTracking().Where(x => x.ExpectedDate.HasValue && x.ExpectedDate.Value < today &&
                x.Status != PurchaseOrderStatus.Received && x.Status != PurchaseOrderStatus.Cancelled)
            .OrderBy(x => x.ExpectedDate).ThenBy(x => x.PoNumber)
            .Select(x => new OverduePurchaseOrderReport(x.Id, x.PoNumber, x.Supplier.Name, x.ExpectedDate!.Value,
                today.DayNumber - x.ExpectedDate.Value.DayNumber, x.TotalAmount));
        return PageAsync(query, page, size, ct);
    }

    public Task<ProcurementReportPage<PurchaseOrderReceiptComparison>> GetReceiptComparisonAsync(int page, int size,
        Guid? purchaseOrderId, CancellationToken ct = default)
    {
        var query = from line in db.PurchaseOrderItems.AsNoTracking()
                    join order in db.PurchaseOrders.AsNoTracking() on line.PurchaseOrderId equals order.Id
                    where !purchaseOrderId.HasValue || order.Id == purchaseOrderId.Value
                    orderby order.PoNumber, line.Product.Name
                    select new PurchaseOrderReceiptComparison(order.Id, order.PoNumber, order.Supplier.Name,
                        line.ProductId, line.Product.Name, line.OrderedQty, line.ReceivedQty,
                        db.GoodsReceiptItems.Where(x => x.PurchaseOrderItemId == line.Id && x.GoodsReceipt.ReversedAt == null)
                            .Sum(x => (decimal?)x.RejectedQty) ?? 0m);
        return PageAsync(query, page, size, ct);
    }

    public async Task<ProcurementReportPage<SupplierReceiptAmount>> GetSupplierReceiptAmountsAsync(int page, int size,
        DateOnly? fromDate, DateOnly? toDate, CancellationToken ct = default)
    {
        var receipts = db.GoodsReceipts.AsNoTracking().AsQueryable();
        receipts = receipts.Where(x => x.ReversedAt == null);
        if (fromDate.HasValue) receipts = receipts.Where(x => x.ReceivedDate >= fromDate.Value);
        if (toDate.HasValue) receipts = receipts.Where(x => x.ReceivedDate <= toDate.Value);
        var groups = from receipt in receipts
                     join item in db.GoodsReceiptItems.AsNoTracking() on receipt.Id equals item.GoodsReceiptId
                     group new { receipt, item } by new { receipt.PurchaseOrder.SupplierId,
                         receipt.PurchaseOrder.Supplier.Name, receipt.PurchaseOrder.Currency } into grouped
                     orderby grouped.Key.Name, grouped.Key.Currency
                     select new SupplierReceiptAmount(grouped.Key.SupplierId, grouped.Key.Name,
                         grouped.Key.Currency, grouped.Sum(x => x.item.ReceivedQty * x.item.UnitCost),
                         grouped.Select(x => x.receipt.Id).Distinct().Count());
        return await PageAsync(groups, page, size, ct);
    }

    private static async Task<ProcurementReportPage<T>> PageAsync<T>(IQueryable<T> query, int page, int size, CancellationToken ct)
    {
        size = Math.Clamp(size, 1, PurchaseOrderService.MaximumPageSize);
        var count = await query.CountAsync(ct);
        var pages = (int)Math.Ceiling(count / (double)size);
        page = pages == 0 ? 1 : Math.Clamp(page, 1, pages);
        return new(await query.Skip((page - 1) * size).Take(size).ToArrayAsync(ct), page, size, count, pages, page < pages, page > 1);
    }

}
