using System.Data;
using System.Globalization;
using System.IO.Compression;
using System.Text;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using StockManagement.Application.DTOs.Products;
using StockManagement.Application.Products;
using StockManagement.Domain.Entities;
using StockManagement.Domain.Enums;
using StockManagement.Infrastructure.Persistence;

namespace StockManagement.Infrastructure.Services;

public sealed class ProductImportService(StockManagementDbContext db, ILogger<ProductImportService> logger)
    : IProductImportService
{
    public const int MaximumFileBytes = 5 * 1024 * 1024;
    private static readonly string[] Columns = ["sku", "name", "category", "price", "quantity", "unit"];
    private static readonly StringComparer Comparer = StringComparer.OrdinalIgnoreCase;
    private sealed record ImportRow(int Number, string Sku, string Name, string Category,
        decimal Price, decimal Quantity, string Unit);

    public Task<byte[]> CreateTemplateAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Məhsullar");
        for (var i = 0; i < Columns.Length; i++) sheet.Cell(1, i + 1).Value = Columns[i];
        sheet.Column(1).Style.NumberFormat.Format = "@";
        sheet.Cell(2, 1).Value = "SUD-001";
        sheet.Cell(2, 2).Value = "Süd";
        sheet.Cell(2, 3).Value = "Ərzaq";
        sheet.Cell(2, 4).Value = 2.50m;
        sheet.Cell(2, 5).Value = 10;
        sheet.Cell(2, 6).Value = "Ədəd";
        sheet.Cell(3, 1).Value = "SEKER-001";
        sheet.Cell(3, 2).Value = "Şəkər";
        sheet.Cell(3, 3).Value = "Şirniyyat";
        sheet.Cell(3, 4).Value = 3.20m;
        sheet.Cell(3, 5).Value = 5;
        sheet.Cell(3, 6).Value = "Kiloqram";
        sheet.Range("A1:F1").Style.Font.Bold = true;
        sheet.Range("A1:F1").Style.Fill.BackgroundColor = XLColor.FromHtml("#E8F5EE");
        sheet.Columns(1, 6).Width = 22;
        sheet.Column(2).Width = 30;
        sheet.Columns(4, 5).Style.NumberFormat.Format = "0.###";
        sheet.SheetView.FreezeRows(1);
        using var output = new MemoryStream();
        workbook.SaveAs(output);
        return Task.FromResult(output.ToArray());
    }

    public async Task<ProductImportResult> ImportAsync(Stream content, string fileName, Guid appUserId,
        Guid? warehouseId = null, CancellationToken cancellationToken = default)
    {
        if (!string.Equals(Path.GetExtension(fileName), ".xlsx", StringComparison.OrdinalIgnoreCase))
            return Fail("file", "Yalnız .xlsx faylı qəbul edilir.");

        var errors = new List<ProductImportError>();
        List<ImportRow> rows;
        try
        {
            using var buffer = new MemoryStream();
            var chunk = new byte[81920];
            int count;
            while ((count = await content.ReadAsync(chunk, cancellationToken)) > 0)
            {
                if (buffer.Length + count > MaximumFileBytes)
                    return Fail("file", "Faylın ölçüsü 5 MB-dan böyük ola bilməz.");
                await buffer.WriteAsync(chunk.AsMemory(0, count), cancellationToken);
            }
            if (buffer.Length == 0) return Fail("file", "Fayl boşdur.");
            buffer.Position = 0;
            // Bound decompressed content before handing the archive to the workbook reader.
            using (var archive = new ZipArchive(buffer, ZipArchiveMode.Read, leaveOpen: true))
            {
                long expanded = 0;
                foreach (var entry in archive.Entries)
                {
                    expanded = checked(expanded + entry.Length);
                    if (expanded > 50 * 1024 * 1024)
                        return Fail("file", "Excel faylının açılmış məzmunu 50 MB həddini keçir.");
                }
            }
            buffer.Position = 0;
            using var workbook = new XLWorkbook(buffer);
            rows = ReadRows(workbook, errors, cancellationToken);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Məhsul idxalı: Excel faylı oxunmadı.");
            return Fail("file", "Excel faylı oxunmadı. Düzgün və şifrələnməmiş .xlsx faylı seçin.");
        }

        if (rows.Count == 0 && errors.Count == 0)
            errors.Add(new(0, "file", "Faylda məhsul sətri yoxdur."));

        try
        {
            // SQL Server serializable locks protect read/add quantities and concurrent SKU creation.
            await using var transaction = db.Database.IsRelational()
                ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken)
                : await db.Database.BeginTransactionAsync(cancellationToken);
            var categories = (await db.Categories.AsNoTracking().ToListAsync(cancellationToken))
                .GroupBy(x => Normalize(x.Name), Comparer).ToDictionary(x => x.Key, x => x.ToArray(), Comparer);
            // One query for all SKUs, including deleted rows whose unique keys remain reserved.
            // Match in memory: database collation must not replace ordinal Unicode comparison.
            var products = (await db.Products.IgnoreQueryFilters().ToListAsync(cancellationToken))
                .GroupBy(x => Normalize(x.Sku), Comparer).ToDictionary(x => x.Key, x => x.ToArray(), Comparer);
            foreach (var row in rows)
            {
                if (!categories.TryGetValue(row.Category, out var matches))
                    errors.Add(new(row.Number, "category", $"Sətir {row.Number}: kateqoriya bazada mövcud deyil."));
                else if (matches.Length != 1)
                    errors.Add(new(row.Number, "category", $"Sətir {row.Number}: kateqoriya adı birmənalı deyil."));
                if (products.TryGetValue(row.Sku, out var existing) && (existing.Length != 1 || existing[0].IsDeleted))
                    errors.Add(new(row.Number, "sku", $"Sətir {row.Number}: SKU silinmiş məhsula aiddir və ya birmənalı deyil."));
            }
            var warehouses = await db.Warehouses.AsNoTracking().Select(x => x.Id).ToArrayAsync(cancellationToken);
            var selectedWarehouse = warehouseId ?? (warehouses.Length == 1 ? warehouses[0] : Guid.Empty);
            if (!warehouses.Contains(selectedWarehouse))
                errors.Add(new(0, "warehouseId", "Mövcud anbar seçin. Bir neçə anbar olduqda warehouseId göndərilməlidir."));
            if (!await db.Users.AnyAsync(x => x.Id == appUserId && x.IsActive, cancellationToken))
                errors.Add(new(0, "user", "Əməliyyatı edən aktiv istifadəçi tapılmadı."));

            var ids = rows.Where(x => products.ContainsKey(x.Sku))
                .SelectMany(x => products[x.Sku]).Select(x => x.Id).Distinct().ToArray();
            var stocks = await db.Stocks.Where(x => ids.Contains(x.ProductId)).ToListAsync(cancellationToken);
            var stockGroups = stocks.ToLookup(x => x.ProductId);
            foreach (var row in rows)
            {
                if (!products.TryGetValue(row.Sku, out var existing) || existing.Length != 1) continue;
                var quantity = stockGroups[existing[0].Id].Sum(x => x.Quantity);
                if (quantity > 999999999999999.999m - row.Quantity)
                    errors.Add(new(row.Number, "quantity", $"Sətir {row.Number}: yekun stok bazanın həddini keçir."));
            }
            if (errors.Count > 0)
            {
                logger.LogWarning("Məhsul idxalı rədd edildi: {ErrorCount} validasiya xətası.", errors.Count);
                return new(false, 0, 0, errors.OrderBy(x => x.Row).ToArray());
            }

            var notifications = await db.StockNotifications.Where(x => ids.Contains(x.ProductId) && x.IsActive)
                .OrderBy(x => x.CreatedAt).ToListAsync(cancellationToken);
            var notificationGroups = notifications.ToLookup(x => x.ProductId);
            var added = 0;
            var updated = 0;
            var logs = new StockLogService(db);
            foreach (var row in rows)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Product product;
                if (products.TryGetValue(row.Sku, out var existing))
                {
                    product = existing[0];
                    var originalSku = product.Sku;
                    product.Update(row.Name, originalSku, product.Barcode, product.Description,
                        product.CategoryId, row.Price, product.UnitOfMeasure, product.IsActive);
                    // Product.Update uppercases SKUs. Preserve the stored key only in this import path.
                    db.Entry(product).Property(x => x.Sku).CurrentValue = originalSku;
                    updated++;
                }
                else
                {
                    product = new Product(row.Name, row.Sku, "", "", categories[row.Category][0].Id,
                        row.Price, row.Unit, true);
                    db.Entry(product).Property(x => x.Sku).CurrentValue = row.Sku;
                    db.Products.Add(product);
                    added++;
                }
                var before = stockGroups[product.Id].Sum(x => x.Quantity);
                if (row.Quantity > 0)
                {
                    var stock = stockGroups[product.Id].SingleOrDefault(x => x.WarehouseId == selectedWarehouse);
                    if (stock is null) db.Stocks.Add(new Stock(product.Id, selectedWarehouse, row.Quantity));
                    else stock.Increase(row.Quantity);
                    logs.Record(product.Id, product.Name, StockLogType.IN, before, before + row.Quantity,
                        "Excel idxalı ilə stok artımı", appUserId);
                    db.StockTransactions.Add(new StockTransaction(product.Id, selectedWarehouse,
                        StockTransactionType.Adjustment, row.Quantity, $"IMP-{Guid.NewGuid():N}", row.Price,
                        "Excel idxalı ilə stok artımı", appUserId));
                }
                RefreshNotifications(product, before + row.Quantity, notificationGroups[product.Id].ToArray());
            }
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new(true, added, updated, []);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception exception)
        {
            logger.LogError(exception, "Məhsul idxalı saxlanmadı; tranzaksiya geri qaytarıldı.");
            db.ChangeTracker.Clear();
            return Fail("file", "İdxal saxlanmadı. Heç bir dəyişiklik tətbiq edilmədi. SKU uyğunluğunu yoxlayıb yenidən cəhd edin.");
        }
    }

    private static List<ImportRow> ReadRows(XLWorkbook workbook, List<ProductImportError> errors,
        CancellationToken cancellationToken)
    {
        var rows = new List<ImportRow>();
        var sheet = workbook.Worksheets.FirstOrDefault();
        if (sheet is null) { errors.Add(new(0, "file", "Faylda iş vərəqi yoxdur.")); return rows; }
        var headers = new Dictionary<string, int>(Comparer);
        foreach (var cell in sheet.Row(1).CellsUsed())
        {
            var header = Text(cell);
            if (!headers.TryAdd(header, cell.Address.ColumnNumber))
                errors.Add(new(1, header, $"Başlıq təkrarlanır: {header}."));
        }
        foreach (var column in Columns)
            if (!headers.ContainsKey(column)) errors.Add(new(1, column, $"Başlıq tapılmadı: {column}."));
        if (errors.Count > 0) return rows;
        var seen = new HashSet<string>(Comparer);
        foreach (var excelRow in sheet.RowsUsed().Where(x => x.RowNumber() > 1))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var number = excelRow.RowNumber();
            var values = Columns.ToDictionary(x => x, x => Text(excelRow.Cell(headers[x])));
            if (values.Values.All(string.IsNullOrWhiteSpace)) continue;
            foreach (var column in Columns)
            {
                if (excelRow.Cell(headers[column]).HasFormula)
                    errors.Add(new(number, column, $"Sətir {number}: formula əvəzinə dəyər daxil edin."));
            }
            foreach (var column in new[] { "sku", "name", "price", "quantity", "category" })
                if (values[column].Length == 0)
                    errors.Add(new(number, column, $"Sətir {number}: {column} sahəsi məcburidir."));
            if (values["sku"].Length > 0 && !seen.Add(values["sku"]))
                errors.Add(new(number, "sku", $"Sətir {number}: SKU faylda təkrarlanır."));
            foreach (var (column, maximum) in new[] { ("sku", 64), ("name", 200), ("category", 150), ("unit", 32) })
                if (values[column].Length > maximum)
                    errors.Add(new(number, column, $"Sətir {number}: {column} ən çox {maximum} simvol ola bilər."));
            var price = Number(values["price"], number, "price", 2, 9999999999999999.99m, errors);
            var quantity = Number(values["quantity"], number, "quantity", 3, 999999999999999.999m, errors);
            rows.Add(new(number, values["sku"], values["name"], values["category"], price, quantity,
                values["unit"].Length == 0 ? "Ədəd" : values["unit"]));
        }
        return rows;
    }

    private static decimal Number(string text, int row, string column, int scale, decimal maximum,
        List<ProductImportError> errors)
    {
        if (text.Length == 0) return 0; // Required-field error is already recorded.
        if (!decimal.TryParse(text.Replace(',', '.'), NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture, out var value) || value < 0 || value > maximum || decimal.Round(value, scale) != value)
        {
            errors.Add(new(row, column, $"Sətir {row}: {column} düzgün deyil; mənfi olmayan, ən çox {scale} onluq rəqəmli ədəd yazın."));
            return 0;
        }
        return value;
    }

    private static string Normalize(string value) => value.Normalize(NormalizationForm.FormC).Trim();
    private static string Text(IXLCell cell) => Normalize(cell.CachedValue.ToString(CultureInfo.InvariantCulture));

    private void RefreshNotifications(Product product, decimal quantity, StockNotification[] active)
    {
        var type = quantity <= 0 ? StockNotificationType.OUT_OF_STOCK
            : quantity <= 5 ? StockNotificationType.LOW_STOCK : (StockNotificationType?)null;
        if (type is null) { foreach (var item in active) item.Resolve(); return; }
        var message = $"{product.Name}: stok qalığı {quantity.ToString(CultureInfo.InvariantCulture)}.";
        if (active.Length == 0) db.StockNotifications.Add(new StockNotification(product.Id, message, type.Value));
        else
        {
            active[0].Update(message, type.Value);
            foreach (var item in active.Skip(1)) item.Resolve();
        }
    }

    private ProductImportResult Fail(string column, string message)
    {
        logger.LogWarning("Məhsul idxalı rədd edildi: {Column}, {Message}", column, message);
        return ProductImportResult.Failure(new ProductImportError(0, column, message));
    }
}
