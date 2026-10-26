namespace StockManagement.Domain.Entities;

public sealed class Product : ISoftDeletable
{
    private Product() { }

    public Product(string name, string sku, string barcode, string description,
        Guid categoryId, decimal unitPrice, string unitOfMeasure, bool isActive, DateTime? entryDate = null)
    {
        Id = Guid.NewGuid();
        EntryDate = entryDate?.ToUniversalTime() ?? DateTime.UtcNow;
        Update(name, sku, barcode, description, categoryId, unitPrice, unitOfMeasure, isActive);
    }

    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Sku { get; private set; } = string.Empty;
    public string Barcode { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public Guid CategoryId { get; private set; }
    public decimal UnitPrice { get; private set; }
    public decimal Stock { get; private set; }
    public decimal AverageCost { get; private set; }
    public decimal ReorderLevel { get; private set; } = 10m;
    public string UnitOfMeasure { get; private set; } = "Piece";
    public bool IsActive { get; private set; } = true;
    public DateTime EntryDate { get; private set; } = DateTime.UtcNow;
    public bool IsDeleted { get; private set; }
    public DateTime? DeletedAt { get; private set; }
    public Category Category { get; private set; } = null!;

    public void MarkDeleted(DateTime? deletedAt = null)
    {
        if (IsDeleted) return;
        IsDeleted = true;
        DeletedAt = deletedAt ?? DateTime.UtcNow;
    }

    public void SetAverageCost(decimal averageCost)
    {
        if (averageCost < 0) throw new ArgumentOutOfRangeException(nameof(averageCost));
        AverageCost = decimal.Round(averageCost, 2, MidpointRounding.AwayFromZero);
    }

    public void SetReorderLevel(decimal reorderLevel)
    {
        if (reorderLevel < 0) throw new ArgumentOutOfRangeException(nameof(reorderLevel));
        ReorderLevel = reorderLevel;
    }

    public void Update(string name, string sku, string barcode, string description,
        Guid categoryId, decimal unitPrice, string unitOfMeasure, bool isActive)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Məhsulun adı boş ola bilməz.", nameof(name));
        if (string.IsNullOrWhiteSpace(sku)) throw new ArgumentException("SKU boş ola bilməz.", nameof(sku));
        if (unitPrice < 0) throw new ArgumentOutOfRangeException(nameof(unitPrice), "Qiymət mənfi ola bilməz.");
        if (categoryId == Guid.Empty) throw new ArgumentException("Kateqoriya seçilməlidir.", nameof(categoryId));
        if (string.IsNullOrWhiteSpace(unitOfMeasure)) throw new ArgumentException("Ölçü vahidi boş ola bilməz.", nameof(unitOfMeasure));
        Name = name.Trim();
        Sku = sku.Trim().ToUpperInvariant();
        Barcode = barcode?.Trim() ?? string.Empty;
        Description = description?.Trim() ?? string.Empty;
        CategoryId = categoryId;
        UnitPrice = unitPrice;
        UnitOfMeasure = unitOfMeasure.Trim();
        IsActive = isActive;
    }
}
