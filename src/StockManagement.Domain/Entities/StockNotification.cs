using StockManagement.Domain.Enums;

namespace StockManagement.Domain.Entities;

public sealed class StockNotification
{
    private StockNotification() { }

    public StockNotification(Guid productId, string message, StockNotificationType type)
    {
        Id = Guid.NewGuid();
        ProductId = productId;
        Message = message;
        Type = type;
        CreatedAt = DateTime.UtcNow;
    }

    public Guid Id { get; private set; }
    public Guid ProductId { get; private set; }
    public Product Product { get; private set; } = null!;
    public string Message { get; private set; } = string.Empty;
    public StockNotificationType Type { get; private set; }
    public bool IsRead { get; private set; }
    public bool IsActive { get; private set; } = true;
    public DateTime CreatedAt { get; private set; }

    public void Update(string message, StockNotificationType type)
    {
        Message = message;
        Type = type;
    }

    public void MarkRead() => IsRead = true;

    public void Resolve()
    {
        IsRead = true;
        IsActive = false;
    }
}
