using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StockManagement.Domain.Enums;
using StockManagement.Infrastructure.Persistence;

namespace StockManagement.Api.Controllers;

/// <summary>Read-only inventory data exposed to the Python chatbot service.</summary>
[ApiController]
[Route("api/chatbot-data")]
[AllowAnonymous]
public sealed class ChatbotDataController(StockManagementDbContext db) : ControllerBase
{
    [HttpGet("product-stock")]
    public async Task<IActionResult> GetProductStock([FromQuery] string name, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(name))
            return BadRequest(new { error = "Məhsul adı tələb olunur." });

        var product = await db.Products.AsNoTracking()
            .Where(p => p.IsActive && p.Name.Contains(name.Trim()))
            .Select(p => new
            {
                p.Id,
                p.Name,
                p.Sku,
                p.UnitPrice,
                p.UnitOfMeasure,
                Stock = db.Stocks.Where(s => s.ProductId == p.Id).Sum(s => (decimal?)s.Quantity) ?? 0m
            })
            .FirstOrDefaultAsync(cancellationToken);

        return product is null ? NotFound(new { error = "Məhsul tapılmadı." }) : Ok(product);
    }

    [HttpGet("top-selling")]
    public async Task<IActionResult> GetTopSelling(CancellationToken cancellationToken)
    {
        var sales = db.StockTransactions.AsNoTracking()
            .Where(t => t.TransactionType == StockTransactionType.Sale)
            .GroupBy(t => t.ProductId)
            .Select(g => new { ProductId = g.Key, SoldQuantity = g.Sum(t => Math.Abs(t.Quantity)) });

        var products = await (from sale in sales
                              join product in db.Products.AsNoTracking() on sale.ProductId equals product.Id
                              where product.IsActive
                              orderby sale.SoldQuantity descending, product.UnitPrice descending
                              select new
                              {
                                  product.Id,
                                  product.Name,
                                  product.UnitPrice,
                                  product.UnitOfMeasure,
                                  sale.SoldQuantity
                              })
            .Take(5)
            .ToListAsync(cancellationToken);

        return Ok(products);
    }

    [HttpGet("categories")]
    public async Task<IActionResult> GetCategories(CancellationToken cancellationToken)
    {
        var categories = await db.Categories.AsNoTracking()
            .OrderBy(c => c.Name)
            .Select(c => new { c.Id, c.Name, c.Description, c.ParentCategoryId })
            .ToListAsync(cancellationToken);

        return Ok(categories);
    }
}
