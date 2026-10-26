using System.Text.Json;
using StockManagement.Application.Abstractions;
using StockManagement.Domain.Entities;
using StockManagement.Infrastructure.Persistence;

namespace StockManagement.Infrastructure.Services;

public sealed class AuditLogService(StockManagementDbContext db) : IAuditLogService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task WriteAsync(Guid userId, string action, string entityType, Guid entityId,
        object? oldValues = null, object? newValues = null, CancellationToken cancellationToken = default)
    {
        db.AuditLogs.Add(new AuditLog(userId, action, entityType, entityId,
            oldValues is null ? null : JsonSerializer.Serialize(oldValues, JsonOptions),
            newValues is null ? null : JsonSerializer.Serialize(newValues, JsonOptions)));
        await db.SaveChangesAsync(cancellationToken);
    }
}
