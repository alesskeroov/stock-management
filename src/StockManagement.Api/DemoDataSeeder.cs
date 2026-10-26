using Microsoft.EntityFrameworkCore;
using StockManagement.Domain.Entities;
using StockManagement.Domain.Enums;
using StockManagement.Infrastructure.Persistence;
using StockManagement.Infrastructure.Services;

namespace StockManagement.Api;

public static class DemoDataSeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<StockManagementDbContext>();
        var stockLogs = scope.ServiceProvider.GetRequiredService<StockLogService>();

        var categoryNames = await db.Categories.Select(x => x.Name).ToListAsync();
        var missingCategories = new List<Category>();
        if (!categoryNames.Contains("Elektronika")) missingCategories.Add(new("Elektronika", "Elektronika və aksesuarlar"));
        if (!categoryNames.Contains("Ofis ləvazimatları")) missingCategories.Add(new("Ofis ləvazimatları", "Gündəlik ofis məhsulları"));
        if (!categoryNames.Contains("Məişət")) missingCategories.Add(new("Məişət", "Ev və məişət üçün məhsullar"));
        if (missingCategories.Count > 0)
        {
            db.Categories.AddRange(missingCategories);
            await db.SaveChangesAsync();
        }

        if (!await db.Products.AnyAsync())
        {
            var categories = await db.Categories.ToDictionaryAsync(x => x.Name, x => x.Id);
            var electronicsId = categories["Elektronika"];
            var officeId = categories["Ofis ləvazimatları"];
            var homeId = categories["Məişət"];

            db.Products.AddRange(
                new Product("Simsiz siçan", "ELE-MOU-001", "869000100001", "Erqonomik, 2.4 GHz simsiz siçan", electronicsId, 29.90m, "ədəd", true, RandomEntryDate()),
                new Product("Mexaniki klaviatura", "ELE-KEY-002", "869000100002", "RGB işıqlı mexaniki klaviatura", electronicsId, 119.00m, "ədəd", true, RandomEntryDate()),
                new Product("USB-C kabel", "ELE-CAB-003", "869000100003", "1 metr sürətli şarj kabeli", electronicsId, 12.50m, "ədəd", true, RandomEntryDate()),
                new Product("LED monitor 24\"", "ELE-MON-004", "869000100004", "Full HD IPS monitor", electronicsId, 329.00m, "ədəd", true, RandomEntryDate()),
                new Product("A4 kağız", "OFF-PAP-001", "869000200001", "80 qramlıq ağ A4 kağız", officeId, 8.40m, "paket", true, RandomEntryDate()),
                new Product("Qara qələm", "OFF-PEN-002", "869000200002", "Mavi mürəkkəbli qara korpuslu qələm", officeId, 0.80m, "ədəd", true, RandomEntryDate()),
                new Product("Spiral dəftər", "OFF-NOT-003", "869000200003", "A5 formatlı 80 vərəqli dəftər", officeId, 3.20m, "ədəd", true, RandomEntryDate()),
                new Product("Masa lampası", "HOM-LAM-001", "869000300001", "Tənzimlənən LED masa lampası", homeId, 45.00m, "ədəd", true, RandomEntryDate()),
                new Product("Saxlama qutusu", "HOM-BOX-002", "869000300002", "Şəffaf plastik saxlama qutusu", homeId, 14.75m, "ədəd", false, RandomEntryDate())
            );

            await db.SaveChangesAsync();
        }

        if (!await db.Stocks.AnyAsync())
        {
            var warehouse = await db.Warehouses.OrderBy(x => x.Name).FirstOrDefaultAsync();
            if (warehouse is null)
            {
                warehouse = new Warehouse("Demo Anbarı", "Bakı, Azərbaycan");
                db.Warehouses.Add(warehouse);
                await db.SaveChangesAsync();
            }

            var products = await db.Products.OrderBy(x => x.Sku).ToListAsync();
            var random = Random.Shared;
            var stocks = products.Select((product, index) => new Stock(
                product.Id,
                warehouse.Id,
                index % 4 == 0 ? random.Next(0, 5) : random.Next(5, 41),
                criticalLowLevel: 5)).ToArray();
            foreach (var stock in stocks)
                stockLogs.Record(stock.ProductId, products.Single(x => x.Id == stock.ProductId).Name,
                    StockLogType.IN, 0, stock.Quantity, "Demo başlanğıc stoku", null);
            db.Stocks.AddRange(stocks);
            await db.SaveChangesAsync();
        }

        const string demoPartnerPrefix = "Demo tərəfdaş — ";
        if (!await db.Documents.AnyAsync(x => x.Partner != null && x.Partner.StartsWith(demoPartnerPrefix)))
        {
            var products = await db.Products.Where(x => x.IsActive).OrderBy(x => x.Sku).Take(4).ToArrayAsync();
            if (products.Length > 0)
            {
                var numbers = scope.ServiceProvider.GetRequiredService<DocumentNumberService>();
                var today = DateOnly.FromDateTime(DateTime.Today);
                var examples = new (string Type, int DaysAgo, string Partner, int ProductIndex, decimal Qty)[]
                {
                    ("in", 12, $"{demoPartnerPrefix}Bakı təchizat", 0, 24m),
                    ("out", 8, $"{demoPartnerPrefix}Mərkəzi mağaza", Math.Min(1, products.Length - 1), 6m),
                    ("in", 3, $"{demoPartnerPrefix}Elektronika", Math.Min(2, products.Length - 1), 12m),
                    ("out", 1, $"{demoPartnerPrefix}Filial 02", Math.Min(3, products.Length - 1), 3m),
                };

                foreach (var example in examples)
                {
                    var product = products[example.ProductIndex];
                    var number = await numbers.GetNextStockDocumentNumberAsync(example.Type);
                    var document = new Document(number, example.Type, today.AddDays(-example.DaysAgo), example.Partner);
                    document.Items.Add(new DocumentItem(document.Id, product.Id, example.Qty, product.UnitPrice));
                    db.Documents.Add(document);
                }

                await db.SaveChangesAsync();
            }
        }
    }

    private static DateTime RandomEntryDate() =>
        DateTime.UtcNow.AddSeconds(-Random.Shared.Next(0, 15_552_000));
}
