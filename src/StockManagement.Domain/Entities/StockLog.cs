using StockManagement.Domain.Enums;

namespace StockManagement.Domain.Entities;

/// <summary>An append-only snapshot of a stock quantity change.</summary>
public sealed class StockLog
{
    private StockLog() { }

    public StockLog(Guid? productId, string productName, StockLogType type, decimal quantityChange,
        decimal quantityBefore, decimal quantityAfter, string? reason, Guid? createdBy, DateTime? createdAt = null)
    {
        if (string.IsNullOrWhiteSpace(productName)) throw new ArgumentException("Məhsul adı tələb olunur.", nameof(productName));
        if (quantityAfter - quantityBefore != quantityChange) throw new ArgumentException("Stok dəyişməsi əvvəl və son qalıqla uyğun olmalıdır.", nameof(quantityChange));
        if (createdBy == Guid.Empty) throw new ArgumentException("İstifadəçi ID-si boş GUID ola bilməz.", nameof(createdBy));
        if (reason?.Length > 1000) throw new ArgumentOutOfRangeException(nameof(reason));

        Id = Guid.NewGuid();
        ProductId = productId;
        ProductName = productName.Trim();
        Type = type;
        QuantityChange = quantityChange;
        QuantityBefore = quantityBefore;
        QuantityAfter = quantityAfter;
        Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        CreatedBy = createdBy;
        CreatedAt = createdAt ?? DateTime.UtcNow;
    }

    public Guid Id { get; private set; }
    public Guid? ProductId { get; private set; }
    public string ProductName { get; private set; } = string.Empty;
    public StockLogType Type { get; private set; }
    public decimal QuantityChange { get; private set; }
    public decimal QuantityBefore { get; private set; }
    public decimal QuantityAfter { get; private set; }
    public string? Reason { get; private set; }
    public Guid? CreatedBy { get; private set; }
    public DateTime CreatedAt { get; private set; }
}
