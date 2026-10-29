using Microsoft.EntityFrameworkCore;
using StockManagement.Domain.Entities;
using StockManagement.Infrastructure.Persistence;
using StockManagement.Infrastructure.Repositories;
using Xunit;

namespace StockManagement.Tests;

public sealed class ProductEditingTests
{
    [Theory]
    [InlineData("")]
    [InlineData("869000100002")]
    public async Task ProductCanBeUpdatedAfterUniquenessChecks(string barcode)
    {
        var options = new DbContextOptionsBuilder<StockManagementDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var db = new StockManagementDbContext(options);
        var original = new Product("Keyboard", "KEY-002", barcode, "", Guid.NewGuid(), 119, "Piece", true);
        db.Products.Add(original);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        var repository = new EfProductRepository(db);

        var product = (await repository.GetByIdAsync(original.Id))!;
        Assert.Equal(product.Id, (await repository.GetBySkuAsync(product.Sku))!.Id);
        if (barcode.Length > 0)
            Assert.Equal(product.Id, (await repository.GetByBarcodeAsync(barcode))!.Id);
        Assert.Empty(db.ChangeTracker.Entries<Product>());

        product.SetReorderLevel(15);
        await repository.UpdateAsync(product);
        db.ChangeTracker.Clear();

        Assert.Equal(15m, (await repository.GetByIdAsync(original.Id))!.ReorderLevel);
    }
}
