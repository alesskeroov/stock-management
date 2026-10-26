namespace StockManagement.Application.DTOs.StockDocuments;

public sealed record StockDocumentItemRequest(Guid ProductId, decimal Qty, decimal Price);

public sealed record CreateStockDocumentRequest(string Type, DateOnly Date, string? Partner,
    IReadOnlyCollection<StockDocumentItemRequest>? Items);

public sealed record StockDocumentItemDto(Guid Id, Guid ProductId, string ProductName, string Sku,
    decimal Qty, decimal Price);

public sealed record StockDocumentDto(Guid Id, string Number, string Type, DateOnly Date, string? Partner,
    string Status, DateTime CreatedAt, IReadOnlyCollection<StockDocumentItemDto> Items);

public sealed record StockDocumentPage(IReadOnlyCollection<StockDocumentDto> Items, int CurrentPage,
    int PageSize, int TotalCount, int TotalPages, bool HasNext, bool HasPrevious);
