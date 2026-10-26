using StockManagement.Domain.Entities;

namespace StockManagement.Application.DTOs.Categories;

public sealed record CategoryDto(Guid Id, string Name, string Description, Guid? ParentCategoryId);
public sealed record CategoryPage(IReadOnlyCollection<CategoryDto> Items, int CurrentPage, int PageSize,
    int TotalCount, int TotalPages, bool HasNext, bool HasPrevious);
public sealed record SaveCategoryRequest(string Name, string Description, Guid? ParentCategoryId = null);

public static class CategoryMapping
{
    public static CategoryDto ToDto(this Category category) =>
        new(category.Id, category.Name, category.Description, category.ParentCategoryId);
}
