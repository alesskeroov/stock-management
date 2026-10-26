namespace StockManagement.Domain.Entities;

public sealed class Supplier : ISoftDeletable
{
    private Supplier() { }

    public Supplier(string name, string phone = "", string email = "", string address = "",
        string taxId = "", string contactPerson = "")
    {
        Id = Guid.NewGuid();
        Update(name, phone, email, address, taxId, contactPerson);
        IsActive = true;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = CreatedAt;
    }

    public Guid Id { get; private set; } = Guid.NewGuid();
    public string Name { get; private set; } = string.Empty;
    public string ContactPerson { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public string Phone { get; private set; } = string.Empty;
    public string Address { get; private set; } = string.Empty;
    public string TaxId { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public bool IsDeleted { get; private set; }
    public DateTime? DeletedAt { get; private set; }

    public void Update(string name, string phone, string email, string address, string taxId, string contactPerson = "")
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Təchizatçı adı boş ola bilməz.", nameof(name));
        Name = name.Trim();
        Phone = phone?.Trim() ?? string.Empty;
        Email = email?.Trim() ?? string.Empty;
        Address = address?.Trim() ?? string.Empty;
        TaxId = taxId?.Trim() ?? string.Empty;
        ContactPerson = contactPerson?.Trim() ?? string.Empty;
        if (CreatedAt != default) UpdatedAt = DateTime.UtcNow;
    }

    public void Deactivate()
    {
        IsActive = false;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Activate()
    {
        if (IsDeleted) throw new InvalidOperationException("Silinmiş təchizatçı aktiv edilə bilməz.");
        IsActive = true;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkDeleted(DateTime? deletedAt = null)
    {
        if (IsDeleted) return;
        IsDeleted = true;
        IsActive = false;
        DeletedAt = deletedAt ?? DateTime.UtcNow;
        UpdatedAt = DeletedAt.Value;
    }
}
