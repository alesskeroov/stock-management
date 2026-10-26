namespace StockManagement.Domain.Entities;

public sealed class Document
{
    private Document() { }

    public Document(string number, string type, DateOnly date, string? partner = null)
    {
        if (string.IsNullOrWhiteSpace(number)) throw new ArgumentException("Sənəd nömrəsi tələb olunur.", nameof(number));
        type = type.Trim().ToLowerInvariant();
        if (type is not ("in" or "out")) throw new ArgumentException("Sənəd növü in və ya out olmalıdır.", nameof(type));

        Id = Guid.NewGuid();
        Number = number.Trim();
        Type = type;
        Date = date;
        Partner = partner?.Trim();
        Status = "draft";
        CreatedAt = DateTime.UtcNow;
        Items = new List<DocumentItem>();
    }

    public Guid Id { get; private set; }
    public string Number { get; private set; } = string.Empty;
    public string Type { get; private set; } = string.Empty;
    public DateOnly Date { get; private set; }
    public string? Partner { get; private set; }
    public string Status { get; private set; } = "draft";
    public DateTime CreatedAt { get; private set; }
    public ICollection<DocumentItem> Items { get; private set; } = new List<DocumentItem>();
}
