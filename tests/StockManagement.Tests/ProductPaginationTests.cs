using Microsoft.EntityFrameworkCore;
using StockManagement.Domain.Entities;
using StockManagement.Infrastructure.Persistence;
using StockManagement.Infrastructure.Repositories;
using StockManagement.Application.Products;
using Xunit;

namespace StockManagement.Tests;

public sealed class ProductPaginationTests
{
    [Fact]
    public async Task FirstPageReturnsTenProductsInStableNameOrder()
    {
        await using var db = CreateDatabase(25);
        var (items, total) = await new EfProductRepository(db).GetPageAsync(1, 10);

        Assert.Equal(25, total);
        Assert.Equal(10, items.Count);
        Assert.Equal("Product 01", items.First().Name);
    }

    [Fact]
    public async Task SecondPageReturnsRemainingProducts()
    {
        await using var db = CreateDatabase(11);
        var (items, _) = await new EfProductRepository(db).GetPageAsync(2, 10);

        Assert.Single(items);
        Assert.Equal("Product 11", items.Single().Name);
    }

    [Fact]
    public async Task OutOfRangePageUsesLastPage()
    {
        await using var db = CreateDatabase(11);
        var (items, _) = await new EfProductRepository(db).GetPageAsync(99, 10);

        Assert.Single(items);
        Assert.Equal("Product 11", items.Single().Name);
    }

    [Fact]
    public void InvalidPageAndSizeUseSafeDefaultsAndSizeIsCapped()
    {
        Assert.Equal(1, ProductService.NormalizePage(-2));
        Assert.Equal(1, ProductService.NormalizePage(0));
        Assert.Equal(10, ProductService.NormalizePageSize(0));
        Assert.Equal(10, ProductService.NormalizePageSize(-1));
        Assert.Equal(50, ProductService.NormalizePageSize(100));
    }

    [Fact]
    public async Task EmptyCatalogReturnsNoProducts()
    {
        await using var db = CreateDatabase(0);
        var (items, total) = await new EfProductRepository(db).GetPageAsync(1, 10);

        Assert.Empty(items);
        Assert.Equal(0, total);
    }

    private static StockManagementDbContext CreateDatabase(int count)
    {
        var options = new DbContextOptionsBuilder<StockManagementDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        var db = new StockManagementDbContext(options);
        var categoryId = Guid.NewGuid();
        for (var i = 1; i <= count; i++)
            db.Products.Add(new Product($"Product {i:00}", $"SKU-{i:00}", "", "", categoryId, 1, "Piece", true));
        db.SaveChanges();
        return db;
    }
}
