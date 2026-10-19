namespace StockManagement.Domain.Entities;

public sealed class Warehouse
{
    private Warehouse() { }

    public Warehouse(string name, string location)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Anbar adı boş ola bilməz.", nameof(name));
        if (string.IsNullOrWhiteSpace(location)) throw new ArgumentException("Anbar ünvanı boş ola bilməz.", nameof(location));
        Id = Guid.NewGuid();
        Name = name.Trim();
        Location = location.Trim();
    }

    public Guid Id { get; private set; } = Guid.NewGuid();
    public string Name { get; private set; } = string.Empty;
    public string Location { get; private set; } = string.Empty;
    // ManagerId is reserved for the future employee/user entity.
    public Guid? ManagerId { get; private set; }
    public ICollection<Stock> Stocks { get; private set; } = new List<Stock>();
    public ICollection<StockTransaction> StockTransactions { get; private set; } = new List<StockTransaction>();
}
