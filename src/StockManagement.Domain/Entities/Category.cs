namespace StockManagement.Domain.Entities;

public sealed class Category
{
    private Category() { }

    public Category(string name, string description, Guid? parentCategoryId = null)
    {
        Id = Guid.NewGuid();
        Update(name, description, parentCategoryId);
    }

    public Guid Id { get; private set; } = Guid.NewGuid();
    public string Name { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public Guid? ParentCategoryId { get; private set; }
    public Category? ParentCategory { get; private set; }
    public ICollection<Category> Subcategories { get; private set; } = new List<Category>();
    public ICollection<Product> Products { get; private set; } = new List<Product>();

    public void Update(string name, string description, Guid? parentCategoryId)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Kateqoriyanın adı boş ola bilməz.", nameof(name));
        if (parentCategoryId == Id)
            throw new InvalidOperationException("Kateqoriya özünün üst kateqoriyası ola bilməz.");

        Name = name.Trim();
        Description = description?.Trim() ?? string.Empty;
        ParentCategoryId = parentCategoryId;
    }
}
