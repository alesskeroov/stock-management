namespace StockManagement.Application.DTOs.StockLogs;

public sealed record StockLogDto(Guid Id, Guid? ProductId, string ProductName, string Type,
    decimal QuantityChange, decimal QuantityBefore, decimal QuantityAfter, string? Reason,
    string? CreatedBy, DateTime CreatedAt);

public sealed record StockLogPage(IReadOnlyCollection<StockLogDto> Items, int CurrentPage, int PageSize,
    int TotalCount, int TotalPages, bool HasNext, bool HasPrevious);
