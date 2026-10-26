using Microsoft.EntityFrameworkCore;
using StockManagement.Application.DTOs.StockDocuments;
using StockManagement.Domain.Entities;
using StockManagement.Infrastructure.Persistence;

namespace StockManagement.Infrastructure.Services;

public sealed class StockDocumentService(StockManagementDbContext db, DocumentNumberService numbers)
{
    public const int DefaultPageSize = 10;
    public const int MaximumPageSize = 10;

    public async Task<StockDocumentPage> GetPageAsync(int page, int size, string? type,
        CancellationToken cancellationToken = default)
    {
        var query = db.Documents.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(type)) query = query.Where(x => x.Type == type);

        var count = await query.CountAsync(cancellationToken);
        var totalPages = (int)Math.Ceiling(count / (double)size);
        page = totalPages == 0 ? 1 : Math.Clamp(page, 1, totalPages);
        var documents = await query.OrderByDescending(x => x.Date).ThenByDescending(x => x.CreatedAt)
            .Skip((page - 1) * size).Take(size)
            .Select(x => new { x.Id, x.Number, x.Type, x.Date, x.Partner, x.Status, x.CreatedAt })
            .ToArrayAsync(cancellationToken);
        var ids = documents.Select(x => x.Id).ToArray();
        var lines = await db.DocumentItems.AsNoTracking().Where(x => ids.Contains(x.DocumentId))
            .OrderBy(x => x.Id)
            .Select(x => new { x.DocumentId, x.Id, x.ProductId, ProductName = x.Product.Name, x.Product.Sku, x.Qty, x.Price })
            .ToArrayAsync(cancellationToken);
        var itemsByDocument = lines.GroupBy(x => x.DocumentId).ToDictionary(x => x.Key,
            x => (IReadOnlyCollection<StockDocumentItemDto>)x.Select(line => new StockDocumentItemDto(
                line.Id, line.ProductId, line.ProductName, line.Sku, line.Qty, line.Price)).ToArray());
        var rows = documents.Select(x => new StockDocumentDto(x.Id, x.Number, x.Type, x.Date, x.Partner,
            x.Status, x.CreatedAt, itemsByDocument.GetValueOrDefault(x.Id, []))).ToArray();
        return new(rows, page, size, count, totalPages, page < totalPages, page > 1);
    }

    public async Task<StockDocumentDto> CreateAsync(CreateStockDocumentRequest request,
        CancellationToken cancellationToken = default)
    {
        var type = request.Type.Trim().ToLowerInvariant();
        if (type is not ("in" or "out")) throw new ArgumentException("Sənəd növü in və ya out olmalıdır.");
        var lines = request.Items?.ToArray() ?? [];
        if (lines.Length == 0) throw new ArgumentException("Sənədə ən azı bir məhsul sətri əlavə edin.");
        if (lines.Any(x => x.ProductId == Guid.Empty || x.Qty <= 0 || x.Price < 0))
            throw new ArgumentException("Məhsul seçilməli, miqdar sıfırdan böyük və qiymət mənfi olmamalıdır.");

        var productIds = lines.Select(x => x.ProductId).Distinct().ToArray();
        var products = await db.Products.Where(x => productIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, cancellationToken);
        if (products.Count != productIds.Length) throw new KeyNotFoundException("Məhsullardan biri tapılmadı.");

        var number = await numbers.GetNextStockDocumentNumberAsync(type, cancellationToken);
        var document = new Document(number, type, request.Date, request.Partner);
        foreach (var line in lines)
            document.Items.Add(new DocumentItem(document.Id, line.ProductId, line.Qty, line.Price));
        db.Documents.Add(document);
        await db.SaveChangesAsync(cancellationToken);

        return new(document.Id, document.Number, document.Type, document.Date, document.Partner,
            document.Status, document.CreatedAt, lines.Select((line, index) => new StockDocumentItemDto(
                document.Items.ElementAt(index).Id, line.ProductId, products[line.ProductId].Name,
                products[line.ProductId].Sku, line.Qty, line.Price)).ToArray());
    }
}
