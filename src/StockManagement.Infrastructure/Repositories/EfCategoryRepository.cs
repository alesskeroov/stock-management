using Microsoft.EntityFrameworkCore;
using StockManagement.Application.Categories;
using StockManagement.Domain.Entities;
using StockManagement.Infrastructure.Persistence;

namespace StockManagement.Infrastructure.Repositories;

public sealed class EfCategoryRepository(StockManagementDbContext dbContext)
    : EfRepository<Category>(dbContext), ICategoryRepository
{
    public async Task<(IReadOnlyCollection<Category> Items, int TotalCount, int CurrentPage)> GetPageAsync(
        int page, int size, string? search, CancellationToken cancellationToken = default)
    {
        var query = dbContext.Categories.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var value = search.Trim();
            query = query.Where(category => category.Name.Contains(value) || category.Description.Contains(value));
        }

        query = query.OrderBy(category => category.Name).ThenBy(category => category.Id);
        var totalCount = await query.CountAsync(cancellationToken);
        var totalPages = (int)Math.Ceiling(totalCount / (double)size);
        var currentPage = totalPages == 0 ? 1 : Math.Clamp(page, 1, totalPages);
        var items = await query.Skip((currentPage - 1) * size).Take(size).ToArrayAsync(cancellationToken);
        return (items, totalCount, currentPage);
    }
}
