using System.Text;
using System.Text.Json;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using StockManagement.Api.Controllers;
using StockManagement.Domain.Entities;
using StockManagement.Infrastructure.Identity;
using StockManagement.Infrastructure.Persistence;
using StockManagement.Infrastructure.Services;
using Xunit;

namespace StockManagement.Tests;

public sealed class ProductImportTests
{
    [Fact]
    public async Task ValidFileAddsProductsAndAddsToExistingStock()
    {
        await using var db = Database();
        var (category, warehouse, user) = await Seed(db);
        var product = new Product("Köhnə ad", "TEA-001", "86901", "Təsvir", category.Id, 2, "Paket", false);
        db.AddRange(product, new Stock(product.Id, warehouse.Id, 10));
        await db.SaveChangesAsync();
        using var file = Workbook(["tea-001", "Çay", "şirniyyat", "12,50", "2.5", "Ədəd"],
            ["NEW-001", "Şəkər", "Şirniyyat", "3.20", "5", "Kiloqram"]);

        var result = await Service(db).ImportAsync(file, "mal.xlsx", user.Id);

        Assert.True(result.Success);
        Assert.Equal(1, result.AddedCount);
        Assert.Equal(1, result.UpdatedCount);
        db.ChangeTracker.Clear();
        var saved = await db.Products.SingleAsync(x => x.Id == product.Id);
        Assert.Equal("Çay", saved.Name);
        Assert.Equal(12.50m, saved.UnitPrice);
        Assert.Equal("TEA-001", saved.Sku);
        Assert.Equal("86901", saved.Barcode);
        Assert.Equal("Təsvir", saved.Description);
        Assert.Equal("Paket", saved.UnitOfMeasure);
        Assert.False(saved.IsActive);
        Assert.Equal(12.5m, (await db.Stocks.SingleAsync(x => x.ProductId == product.Id)).Quantity);
        Assert.Equal(2, await db.StockTransactions.CountAsync());
        Assert.Equal(2, await db.StockLogs.CountAsync());
    }

    [Theory]
    [InlineData("", "Məhsul", "1", "1", "sku")]
    [InlineData("SKU", "", "1", "1", "name")]
    [InlineData("SKU", "Məhsul", "", "1", "price")]
    [InlineData("SKU", "Məhsul", "1", "", "quantity")]
    [InlineData("SKU", "Məhsul", "-1", "1", "price")]
    [InlineData("SKU", "Məhsul", "1", "-1", "quantity")]
    [InlineData("SKU", "Məhsul", "1,2.3", "1", "price")]
    [InlineData("SKU", "Məhsul", "1.234", "1", "price")]
    public async Task InvalidRowPreventsAllWrites(string sku, string name, string price, string quantity, string column)
    {
        await using var db = Database();
        var (_, _, user) = await Seed(db);
        using var file = Workbook(["VALID", "Çay", "Şirniyyat", "1", "1", "Ədəd"],
            [sku, name, "Şirniyyat", price, quantity, "Ədəd"]);

        var result = await Service(db).ImportAsync(file, "mal.xlsx", user.Id);

        Assert.False(result.Success);
        Assert.Contains(result.Errors, x => x.Row == 3 && x.Column == column);
        Assert.Equal(0, result.AddedCount);
        Assert.Empty(await db.Products.ToListAsync());
        Assert.Empty(await db.Stocks.ToListAsync());
        Assert.Empty(await db.StockLogs.ToListAsync());
    }

    [Fact]
    public async Task DuplicateSkuIsDetectedWithOrdinalIgnoreCase()
    {
        await using var db = Database();
        var (_, _, user) = await Seed(db);
        using var file = Workbook([" çay-1 ", "Çay", "Şirniyyat", "1", "1", "Ədəd"],
            ["ÇAY-1", "Çay", "Şirniyyat", "2", "2", "Ədəd"]);
        var result = await Service(db).ImportAsync(file, "mal.xlsx", user.Id);
        Assert.Contains(result.Errors, x => x.Row == 3 && x.Column == "sku");
        Assert.Empty(await db.Products.ToListAsync());
    }

    [Fact]
    public async Task AzerbaijaniTextIsNormalizedAndDottedAndDotlessKeysRemainDistinct()
    {
        await using var db = Database();
        var (_, _, user) = await Seed(db);
        using var file = Workbook(["i", " Şəkər ", "şirniyyat", "1", "1", "Ədəd"],
            ["İ", "Çay", "Şirniyyat", "1", "1", "Ədəd"],
            ["ı", "İpək".Normalize(NormalizationForm.FormD), "Şirniyyat", "1", "1", "Ədəd"],
            ["ığ", "ığ", "Şirniyyat", "1", "1", "Ədəd"]);

        var result = await Service(db).ImportAsync(file, "mal.xlsx", user.Id);

        Assert.True(result.Success);
        db.ChangeTracker.Clear();
        var products = (await db.Products.ToListAsync()).ToDictionary(x => x.Sku, StringComparer.OrdinalIgnoreCase);
        Assert.Equal("Şəkər", products["I"].Name);
        Assert.Equal("Çay", products["İ"].Name);
        Assert.Equal("İpək", products["ı"].Name);
        Assert.Equal("ığ", products["ığ"].Name);
        Assert.All(products.Values, x => Assert.True(x.Name.IsNormalized(NormalizationForm.FormC)));
    }

    [Fact]
    public async Task MissingCategoryAndOtherRowErrorsAreReturnedTogether()
    {
        await using var db = Database();
        var (_, _, user) = await Seed(db);
        using var file = Workbook(["A", "", "Yoxdur", "-1", "-2", "Ədəd"]);
        var result = await Service(db).ImportAsync(file, "mal.xlsx", user.Id);
        Assert.Equal(4, result.Errors.Count);
        Assert.Contains(result.Errors, x => x.Column == "category");
        Assert.Empty(await db.Products.ToListAsync());
    }

    [Fact]
    public async Task HeadersAreTrimmedCaseInsensitiveAndCanBeReordered()
    {
        await using var db = Database();
        var (_, _, user) = await Seed(db);
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Məhsullar");
        string[] headers = [" NAME ", "SKU", "QUANTITY", "PRICE", "CATEGORY", "UNIT"];
        string[] values = ["Süd", "00123", "1,5", "2.50", "Şirniyyat", "Litr"];
        for (var i = 0; i < headers.Length; i++)
        {
            sheet.Cell(1, i + 1).Value = headers[i];
            sheet.Cell(2, i + 1).Value = values[i];
        }
        using var file = Save(workbook);
        var result = await Service(db).ImportAsync(file, "MAL.XLSX", user.Id);
        Assert.True(result.Success);
        Assert.Equal("00123", (await db.Products.SingleAsync()).Sku);
    }

    [Fact]
    public async Task BlankAndIncorrectHeadersAreRejected()
    {
        await using var db = Database();
        var service = Service(db);
        using var empty = new MemoryStream();
        Assert.False((await service.ImportAsync(empty, "mal.xlsx", Guid.NewGuid())).Success);
        using var workbook = new XLWorkbook();
        workbook.AddWorksheet("Məhsullar").Cell(1, 1).Value = "Yanlış";
        using var file = Save(workbook);
        var result = await service.ImportAsync(file, "mal.xlsx", Guid.NewGuid());
        Assert.Contains(result.Errors, x => x.Row == 1 && x.Column == "sku");
    }

    [Fact]
    public async Task WrongExtensionOversizedAndCorruptFilesAreRejected()
    {
        await using var db = Database();
        var service = Service(db);
        using var empty = new MemoryStream();
        Assert.False((await service.ImportAsync(empty, "mal.csv", Guid.NewGuid())).Success);
        using var large = new MemoryStream(new byte[ProductImportService.MaximumFileBytes + 1]);
        var tooLarge = await service.ImportAsync(large, "mal.xlsx", Guid.NewGuid());
        Assert.Contains(tooLarge.Errors, x => x.Message.Contains("5 MB", StringComparison.Ordinal));
        using var corrupt = new MemoryStream(Encoding.UTF8.GetBytes("Excel deyil"));
        Assert.False((await service.ImportAsync(corrupt, "mal.xlsx", Guid.NewGuid())).Success);
    }

    [Fact]
    public async Task MultipleWarehousesRequireAnExplicitChoice()
    {
        await using var db = Database();
        var (_, warehouse, user) = await Seed(db);
        db.Warehouses.Add(new Warehouse("İkinci", "Gəncə"));
        await db.SaveChangesAsync();
        using var file = Workbook(["A", "Çay", "Şirniyyat", "1", "1", "Ədəd"]);
        var result = await Service(db).ImportAsync(file, "mal.xlsx", user.Id);
        Assert.Contains(result.Errors, x => x.Column == "warehouseId");
        file.Position = 0;
        Assert.True((await Service(db).ImportAsync(file, "mal.xlsx", user.Id, warehouse.Id)).Success);
    }

    [Fact]
    public async Task TemplateRoundTripsUnicodeExamples()
    {
        await using var db = Database();
        using var file = new MemoryStream(await Service(db).CreateTemplateAsync());
        using var workbook = new XLWorkbook(file);
        Assert.Equal("sku", workbook.Worksheet(1).Cell(1, 1).GetString());
        Assert.Equal("Süd", workbook.Worksheet(1).Cell(2, 2).GetString());
        Assert.Equal("Şəkər", workbook.Worksheet(1).Cell(3, 2).GetString());
    }

    [Fact]
    public async Task ControllerReturnsReadableUnicodeJsonAndExpectedResponseShape()
    {
        await using var db = Database();
        var controller = new ProductImportController(Service(db), NullLogger<ProductImportController>.Instance);
        var result = Assert.IsType<JsonResult>(await controller.Import(null, null, default));
        Assert.Equal("application/json; charset=utf-8", result.ContentType);
        var json = JsonSerializer.Serialize(result.Value, Assert.IsType<JsonSerializerOptions>(result.SerializerSettings));
        Assert.Contains("seçilməyib", json);
        Assert.Contains("\"success\":false", json);
        Assert.Contains("\"addedCount\":0", json);
        Assert.Contains("\"row\":0", json);
    }

    [Fact]
    public async Task CancelledImportDoesNotWrite()
    {
        await using var db = Database();
        using var file = Workbook(["A", "Çay", "Şirniyyat", "1", "1", "Ədəd"]);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            Service(db).ImportAsync(file, "mal.xlsx", Guid.NewGuid(), cancellationToken: new CancellationToken(true)));
        Assert.Empty(await db.Products.ToListAsync());
    }

    [Fact]
    public async Task CategoryMatchingDoesNotMergeDottedAndDotlessI()
    {
        await using var db = Database();
        var (_, _, user) = await Seed(db);
        var categories = new[] { new Category("i", ""), new Category("İ", ""), new Category("ı", "") };
        db.Categories.AddRange(categories);
        await db.SaveChangesAsync();
        using var file = Workbook(["A", "Şəkər", "I", "1", "1", "Ədəd"],
            ["B", "Çay", "İ", "1", "1", "Ədəd"], ["C", "İpək", "ı", "1", "1", "Ədəd"]);
        Assert.True((await Service(db).ImportAsync(file, "mal.xlsx", user.Id)).Success);
        var products = await db.Products.OrderBy(x => x.Sku).ToArrayAsync();
        Assert.Equal(categories.Select(x => x.Id), products.Select(x => x.CategoryId));
    }

    [Fact]
    public async Task InvalidFileDoesNotChangeExistingProductOrStock()
    {
        await using var db = Database();
        var (category, warehouse, user) = await Seed(db);
        var product = new Product("Əvvəlki", "A", "", "", category.Id, 5, "Ədəd", true);
        db.AddRange(product, new Stock(product.Id, warehouse.Id, 7));
        await db.SaveChangesAsync();
        using var file = Workbook(["A", "Yeni", "Şirniyyat", "10", "3", "Ədəd"],
            ["B", "Səhv", "Şirniyyat", "-1", "2", "Ədəd"]);
        Assert.False((await Service(db).ImportAsync(file, "mal.xlsx", user.Id)).Success);
        db.ChangeTracker.Clear();
        Assert.Equal("Əvvəlki", (await db.Products.SingleAsync()).Name);
        Assert.Equal(5m, (await db.Products.SingleAsync()).UnitPrice);
        Assert.Equal(7m, (await db.Stocks.SingleAsync()).Quantity);
    }

    [Fact]
    public async Task NumericCellsZeroQuantityAndBlankUnitAreSupported()
    {
        await using var db = Database();
        var (_, _, user) = await Seed(db);
        using var input = Workbook(["A", "Süd", "Şirniyyat", "0", "0", ""]);
        using var workbook = new XLWorkbook(input);
        workbook.Worksheet(1).Cell(2, 4).Value = 12.5;
        workbook.Worksheet(1).Cell(2, 5).Value = 0;
        using var file = Save(workbook);
        Assert.True((await Service(db).ImportAsync(file, "mal.xlsx", user.Id)).Success);
        var product = await db.Products.SingleAsync();
        Assert.Equal(12.5m, product.UnitPrice);
        Assert.Equal("Ədəd", product.UnitOfMeasure);
        Assert.Empty(await db.StockTransactions.ToListAsync());
        Assert.Single(await db.StockNotifications.ToListAsync());
    }

    [Fact]
    public async Task FormulasAreRejectedWithoutWriting()
    {
        await using var db = Database();
        var (_, _, user) = await Seed(db);
        using var input = Workbook(["A", "Süd", "Şirniyyat", "1", "1", "Ədəd"]);
        using var workbook = new XLWorkbook(input);
        workbook.Worksheet(1).Cell(2, 4).FormulaA1 = "1+1";
        using var file = Save(workbook);
        var result = await Service(db).ImportAsync(file, "mal.xlsx", user.Id);
        Assert.Contains(result.Errors, x => x.Row == 2 && x.Column == "price");
        Assert.Empty(await db.Products.ToListAsync());
    }

    private static StockManagementDbContext Database() => new(new DbContextOptionsBuilder<StockManagementDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString())
        .ConfigureWarnings(x => x.Ignore(InMemoryEventId.TransactionIgnoredWarning)).Options);

    private static ProductImportService Service(StockManagementDbContext db) => new(db, NullLogger<ProductImportService>.Instance);

    private static async Task<(Category, Warehouse, ApplicationUser)> Seed(StockManagementDbContext db)
    {
        var category = new Category("Şirniyyat", "");
        var warehouse = new Warehouse("Əsas", "Bakı");
        var user = new ApplicationUser { UserName = "import-user", FirstName = "Əli", LastName = "Əliyev" };
        db.AddRange(category, warehouse, user);
        await db.SaveChangesAsync();
        return (category, warehouse, user);
    }

    private static MemoryStream Workbook(params string[][] rows)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Məhsullar");
        string[] headers = ["sku", "name", "category", "price", "quantity", "unit"];
        for (var i = 0; i < headers.Length; i++) sheet.Cell(1, i + 1).Value = headers[i];
        for (var row = 0; row < rows.Length; row++)
            for (var column = 0; column < rows[row].Length; column++)
                sheet.Cell(row + 2, column + 1).Value = rows[row][column];
        return Save(workbook);
    }

    private static MemoryStream Save(XLWorkbook workbook)
    {
        var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;
        return stream;
    }
}
