using StockManagement.Application.DTOs.Products;

namespace StockManagement.Application.Products;

public interface IProductImportService
{
    Task<byte[]> CreateTemplateAsync(CancellationToken cancellationToken = default);
    Task<ProductImportResult> ImportAsync(Stream content, string fileName, Guid appUserId,
        Guid? warehouseId = null, CancellationToken cancellationToken = default);
}
