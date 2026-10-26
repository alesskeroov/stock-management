namespace StockManagement.Application.Abstractions;

public interface IAuditLogService
{
    Task WriteAsync(Guid userId, string action, string entityType, Guid entityId,
        object? oldValues = null, object? newValues = null, CancellationToken cancellationToken = default);
}
