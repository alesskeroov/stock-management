using StockManagement.Domain.Entities;
using System.Text.Json.Serialization;

namespace StockManagement.Application.DTOs.Products;

public sealed record ProductDto(Guid Id, string Name, string Sku, string Barcode, string Description,
    Guid CategoryId, decimal UnitPrice, string UnitOfMeasure, bool IsActive, decimal Quantity,
    [property: JsonPropertyName("createdAt")] DateTime CreatedAt,
    [property: JsonPropertyName("updatedAt")] DateTime UpdatedAt);
public sealed record CreateProductRequest(string Name, string Sku, string Barcode, string Description,
    Guid CategoryId, decimal UnitPrice, string UnitOfMeasure, bool IsActive = true) : IProductRequest;
public sealed record UpdateProductRequest(string Name, string Sku, string Barcode, string Description,
    Guid CategoryId, decimal UnitPrice, string UnitOfMeasure, bool IsActive) : IProductRequest;
public sealed record UpdateProductStockRequest(decimal Quantity);

public static class ProductMapping
{
    public static ProductDto ToDto(this Product product, decimal quantity = 0,
        DateTime? createdAt = null, DateTime? updatedAt = null) =>
        new(product.Id, product.Name, product.Sku, product.Barcode, product.Description,
            product.CategoryId, product.UnitPrice, product.UnitOfMeasure, product.IsActive, quantity,
            AsUtc(createdAt ?? product.EntryDate), AsUtc(updatedAt ?? product.EntryDate));

    private static DateTime AsUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };
}
