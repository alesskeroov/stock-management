namespace StockManagement.Domain.Entities;

public sealed class AuditLog
{
    private AuditLog() { }

    public AuditLog(Guid userId, string action, string entityType, Guid entityId,
        string? oldValues, string? newValues)
    {
        if (userId == Guid.Empty) throw new ArgumentException("İstifadəçi tələb olunur.", nameof(userId));
        Id = Guid.NewGuid();
        UserId = userId;
        Action = action;
        EntityType = entityType;
        EntityId = entityId;
        OldValues = oldValues;
        NewValues = newValues;
        CreatedAt = DateTime.UtcNow;
    }

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string Action { get; private set; } = string.Empty;
    public string EntityType { get; private set; } = string.Empty;
    public Guid EntityId { get; private set; }
    public string? OldValues { get; private set; }
    public string? NewValues { get; private set; }
    public DateTime CreatedAt { get; private set; }
}
