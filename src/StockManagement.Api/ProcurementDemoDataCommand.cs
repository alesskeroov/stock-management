using Microsoft.EntityFrameworkCore;
using StockManagement.Application.DTOs.GoodsReceipts;
using StockManagement.Application.DTOs.PurchaseOrders;
using StockManagement.Domain.Entities;
using StockManagement.Infrastructure.Persistence;
using StockManagement.Infrastructure.Services;

namespace StockManagement.Api;

/// <summary>Creates repeatable sample suppliers, purchase orders, and receipts for UI verification.</summary>
public sealed class ProcurementDemoDataCommand(StockManagementDbContext db,
    PurchaseOrderService purchaseOrders, GoodsReceiptService goodsReceipts,
    OpeningBalanceCommand openingBalances)
{
    private const string Marker = "[UI-DEMO-PROCUREMENT]";

    public async Task<(int Suppliers, int PurchaseOrders, int GoodsReceipts)> ExecuteAsync(int count = 20,
        CancellationToken cancellationToken = default)
    {
        if (count is < 1 or > 50) throw new ArgumentOutOfRangeException(nameof(count), "count 1-50 aralığında olmalıdır.");

        var actorId = await db.Users.OrderBy(x => x.Id).Select(x => x.Id).FirstOrDefaultAsync(cancellationToken);
        if (actorId == Guid.Empty)
            throw new InvalidOperationException("İstifadəçi tapılmadı. Əvvəlcə Auth:BootstrapAdmin konfiqurasiyasını yoxlayıb API-ni başladın.");

        var warehouse = await db.Warehouses.OrderBy(x => x.Name).FirstOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException("Anbar tapılmadı.");
        var products = await db.Products.Where(x => x.IsActive).OrderBy(x => x.Sku).ToArrayAsync(cancellationToken);
        if (products.Length == 0) throw new InvalidOperationException("Aktiv məhsul tapılmadı.");

        var suppliersCreated = 0;
        var purchaseOrdersCreated = 0;
        var receiptsCreated = 0;
        var today = DateOnly.FromDateTime(DateTime.Today);

        for (var index = 1; index <= count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var taxId = $"DEMO-PO-UI-{index:000}";
            var supplier = await db.Suppliers.SingleOrDefaultAsync(x => x.TaxId == taxId, cancellationToken);
            if (supplier is null)
            {
                supplier = new Supplier($"Demo Təchizatçı {index:000}", $"+994 50 555 {index:0000}",
                    $"demo.supplier{index:000}@example.test", $"Bakı, Demo küçəsi {index}", taxId,
                    $"Əlaqələndirici {index:000}");
                db.Suppliers.Add(supplier);
                await db.SaveChangesAsync(cancellationToken);
                suppliersCreated++;
            }

            var note = $"{Marker} {index:000}";
            if (await db.PurchaseOrders.IgnoreQueryFilters().AnyAsync(x => x.Note == note, cancellationToken))
                continue;

            var product = products[(index - 1) % products.Length];
            var quantity = 10m + (index % 5) * 2m;
            var unitPrice = Math.Round(product.UnitPrice * (0.75m + (index % 7) * 0.08m), 2);
            var orderDate = today.AddDays(-35 + (index % 25));
            var overdue = index % 3 == 0;
            var expectedDate = overdue ? today.AddDays(-2 - (index % 12)) : today.AddDays(5 + index % 20);
            var order = await purchaseOrders.CreateAsync(new SavePurchaseOrderRequest(supplier.Id, warehouse.Id,
                orderDate, expectedDate, "AZN", note,
                [new PurchaseOrderLineRequest(product.Id, quantity, unitPrice)]), actorId, cancellationToken);
            purchaseOrdersCreated++;

            switch (index % 6)
            {
                case 0: // draft
                    break;
                case 1: // pending approval
                    await purchaseOrders.SubmitAsync(order.Id, actorId, cancellationToken);
                    break;
                case 5: // cancelled
                    await purchaseOrders.CancelAsync(order.Id, actorId, "Demo məlumat: sifariş ləğv edilib.", cancellationToken);
                    break;
                default: // approved, partially received, or received
                    await purchaseOrders.SubmitAsync(order.Id, actorId, cancellationToken);
                    await purchaseOrders.ApproveAsync(order.Id, actorId, allowSelfApproval: true, cancellationToken);
                    if (index % 6 is 3 or 4)
                    {
                        await openingBalances.ExecuteAsync(actorId, cancellationToken);
                        var itemId = order.Items.Single().Id;
                        var accepted = index % 6 == 3 ? Math.Max(1m, decimal.Floor(quantity / 2m)) : quantity;
                        var firstKey = $"demo-procurement-grn-{index:000}-1";
                        await goodsReceipts.CreateAsync(new CreateGoodsReceiptRequest(order.Id,
                            today.AddDays(-Math.Min(index % 15, 10)), $"{Marker} qəbulu {index:000}", firstKey,
                            [new GoodsReceiptItemRequest(itemId, accepted, index % 6 == 3 ? 1m : 0m,
                                $"DEMO-BATCH-{index:000}", today.AddDays(180 + index), unitPrice)]),
                            actorId, firstKey, cancellationToken);
                        receiptsCreated++;
                        if (index % 6 == 4)
                        {
                            var secondKey = $"demo-procurement-grn-{index:000}-2";
                            await goodsReceipts.CreateAsync(new CreateGoodsReceiptRequest(order.Id,
                                today.AddDays(-Math.Max(0, index % 10 - 1)), $"{Marker} tamamlayıcı qəbul {index:000}", secondKey,
                                [new GoodsReceiptItemRequest(itemId, quantity - accepted, 0m,
                                    $"DEMO-BATCH-{index:000}-2", today.AddDays(180 + index), unitPrice)]),
                                actorId, secondKey, cancellationToken);
                            receiptsCreated++;
                        }
                    }
                    break;
            }
        }

        return (suppliersCreated, purchaseOrdersCreated, receiptsCreated);
    }
}
