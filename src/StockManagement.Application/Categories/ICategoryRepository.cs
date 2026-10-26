using StockManagement.Domain.Entities;

namespace StockManagement.Application.Categories;

public interface ICategoryRepository
{
    Task<(IReadOnlyCollection<Category> Items, int TotalCount, int CurrentPage)> GetPageAsync(int page, int size,
        string? search, CancellationToken cancellationToken = default);
}
