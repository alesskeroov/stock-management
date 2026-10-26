using StockManagement.Application.Abstractions.Repositories;
using StockManagement.Application.DTOs.Categories;
using StockManagement.Domain.Entities;

namespace StockManagement.Application.Categories;

public sealed class CategoryService(
    IGenericRepository<Category> categories,
    ICategoryRepository categoryPages,
    IGenericRepository<Product> products)
{
    public const int DefaultPageSize = 10;
    public const int MaximumPageSize = 10;

    public async Task<IReadOnlyCollection<CategoryDto>> GetAllAsync(CancellationToken cancellationToken = default) =>
        (await categories.GetAllAsync(cancellationToken))
        .OrderBy(x => x.Name)
        .Select(x => x.ToDto())
        .ToArray();

    public async Task<CategoryPage> GetPageAsync(int page, int size, string? search,
        CancellationToken cancellationToken = default)
    {
        size = size < 1 ? DefaultPageSize : Math.Min(size, MaximumPageSize);
        var (items, totalCount, currentPage) = await categoryPages.GetPageAsync(page, size, search, cancellationToken);
        var totalPages = (int)Math.Ceiling(totalCount / (double)size);
        return new(items.Select(x => x.ToDto()).ToArray(), currentPage, size, totalCount, totalPages,
            currentPage < totalPages, currentPage > 1);
    }

    public async Task<CategoryDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        (await categories.GetByIdAsync(id, cancellationToken))?.ToDto();

    public async Task<CategoryDto> CreateAsync(SaveCategoryRequest request,
        CancellationToken cancellationToken = default)
    {
        await EnsureParentExistsAsync(request.ParentCategoryId, cancellationToken);
        var category = new Category(request.Name, request.Description, request.ParentCategoryId);
        await categories.AddAsync(category, cancellationToken);
        return category.ToDto();
    }

    public async Task<CategoryDto?> UpdateAsync(Guid id, SaveCategoryRequest request,
        CancellationToken cancellationToken = default)
    {
        var category = await categories.GetByIdAsync(id, cancellationToken);
        if (category is null) return null;

        await EnsureParentExistsAsync(request.ParentCategoryId, cancellationToken);
        await EnsureNoHierarchyCycleAsync(id, request.ParentCategoryId, cancellationToken);
        category.Update(request.Name, request.Description, request.ParentCategoryId);
        await categories.UpdateAsync(category, cancellationToken);
        return category.ToDto();
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var category = await categories.GetByIdAsync(id, cancellationToken);
        if (category is null) return false;

        var allCategories = await categories.GetAllAsync(cancellationToken);
        if (allCategories.Any(x => x.ParentCategoryId == id))
            throw new InvalidOperationException("Alt kateqoriyaları olan kateqoriyanı silmək mümkün deyil.");

        var allProducts = await products.GetAllAsync(cancellationToken);
        if (allProducts.Any(x => x.CategoryId == id))
            throw new InvalidOperationException("Məhsulları olan kateqoriyanı silmək mümkün deyil.");

        await categories.DeleteAsync(category, cancellationToken);
        return true;
    }

    private async Task EnsureParentExistsAsync(Guid? parentCategoryId, CancellationToken cancellationToken)
    {
        if (parentCategoryId is not null &&
            await categories.GetByIdAsync(parentCategoryId.Value, cancellationToken) is null)
            throw new KeyNotFoundException($"'{parentCategoryId}' ID-li üst kateqoriya tapılmadı.");
    }

    private async Task EnsureNoHierarchyCycleAsync(Guid categoryId, Guid? parentCategoryId,
        CancellationToken cancellationToken)
    {
        if (parentCategoryId is null) return;

        var parentLookup = (await categories.GetAllAsync(cancellationToken))
            .ToDictionary(x => x.Id, x => x.ParentCategoryId);
        var currentId = parentCategoryId;
        while (currentId is not null)
        {
            if (currentId == categoryId)
                throw new InvalidOperationException("Alt kateqoriyanı özünün altına keçirmək olmaz.");
            currentId = parentLookup.GetValueOrDefault(currentId.Value);
        }
    }
}
