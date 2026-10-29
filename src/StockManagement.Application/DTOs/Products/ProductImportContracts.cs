namespace StockManagement.Application.DTOs.Products;

public sealed record ProductImportError(int Row, string Column, string Message);

public sealed record ProductImportResult(bool Success, int AddedCount, int UpdatedCount,
    IReadOnlyList<ProductImportError> Errors)
{
    public static ProductImportResult Failure(params ProductImportError[] errors) => new(false, 0, 0, errors);
}
