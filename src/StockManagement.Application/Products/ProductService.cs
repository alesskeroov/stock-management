using StockManagement.Application.Abstractions.Repositories;
using StockManagement.Application.DTOs.Products;
using StockManagement.Application.Products.Repositories;
using StockManagement.Domain.Entities;

namespace StockManagement.Application.Products;

public sealed class ProductService(
    IProductRepository repository,
    IGenericRepository<Category> categoryRepository)
{
    public async Task<IReadOnlyCollection<ProductDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var products = await repository.GetAllAsync(cancellationToken);
        var quantities = await repository.GetStockQuantitiesAsync(cancellationToken);
        var dates = await repository.GetProductActivityDatesAsync(cancellationToken);
        return products.OrderBy(x => x.Name)
            .Select(product =>
            {
                var activity = dates.GetValueOrDefault(product.Id);
                return product.ToDto(quantities.GetValueOrDefault(product.Id), activity.CreatedAt, activity.UpdatedAt);
            })
            .ToArray();
    }

    public async Task<ProductDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var product = await repository.GetByIdAsync(id, cancellationToken);
        if (product is null) return null;
        var activity = (await repository.GetProductActivityDatesAsync(cancellationToken)).GetValueOrDefault(id);
        return product.ToDto(await repository.GetStockQuantityAsync(id, cancellationToken), activity.CreatedAt, activity.UpdatedAt);
    }

    public async Task<ProductDto?> SetStockQuantityAsync(Guid id, decimal quantity, Guid appUserId,
        CancellationToken cancellationToken = default)
    {
        if (!await repository.SetStockQuantityAsync(id, quantity, appUserId, cancellationToken)) return null;
        return await GetByIdAsync(id, cancellationToken);
    }

    public async Task<ProductDto> CreateAsync(CreateProductRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureCategoryExistsAsync(request.CategoryId, cancellationToken);
        await EnsureSkuAvailableAsync(request.Sku, cancellationToken: cancellationToken);
        await EnsureBarcodeAvailableAsync(request.Barcode, cancellationToken: cancellationToken);
        var product = new Product(request.Name, request.Sku, request.Barcode, request.Description,
            request.CategoryId, request.UnitPrice, request.UnitOfMeasure, request.IsActive);
        await repository.AddAsync(product, cancellationToken);
        return product.ToDto();
    }

    public async Task<ProductDto?> UpdateAsync(Guid id, UpdateProductRequest request,
        CancellationToken cancellationToken = default)
    {
        var product = await repository.GetByIdAsync(id, cancellationToken);
        if (product is null) return null;
        await EnsureCategoryExistsAsync(request.CategoryId, cancellationToken);
        await EnsureSkuAvailableAsync(request.Sku, id, cancellationToken);
        await EnsureBarcodeAvailableAsync(request.Barcode, id, cancellationToken);
        product.Update(request.Name, request.Sku, request.Barcode, request.Description,
            request.CategoryId, request.UnitPrice, request.UnitOfMeasure, request.IsActive);
        await repository.UpdateAsync(product, cancellationToken);
        return product.ToDto();
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var product = await repository.GetByIdAsync(id, cancellationToken);
        if (product is null) return false;
        await repository.DeleteAsync(product, cancellationToken);
        return true;
    }

    private async Task EnsureCategoryExistsAsync(Guid categoryId, CancellationToken cancellationToken)
    {
        if (await categoryRepository.GetByIdAsync(categoryId, cancellationToken) is null)
            throw new KeyNotFoundException($"'{categoryId}' ID-li kateqoriya tapılmadı.");
    }

    private async Task EnsureSkuAvailableAsync(string sku, Guid? exceptId = null,
        CancellationToken cancellationToken = default)
    {
        var existing = await repository.GetBySkuAsync(sku, cancellationToken);
        if (existing is not null && existing.Id != exceptId)
            throw new InvalidOperationException($"'{sku}' SKU artıq mövcuddur.");
    }

    private async Task EnsureBarcodeAvailableAsync(string barcode, Guid? exceptId = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(barcode)) return;
        var existing = await repository.GetByBarcodeAsync(barcode, cancellationToken);
        if (existing is not null && existing.Id != exceptId)
            throw new InvalidOperationException($"'{barcode}' barkodu artıq istifadə olunur.");
    }
}
