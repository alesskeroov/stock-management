namespace StockManagement.Domain.Entities;

public sealed class DocumentItem
{
    private DocumentItem() { }

    public DocumentItem(Guid documentId, Guid productId, decimal qty, decimal price)
    {
        if (documentId == Guid.Empty) throw new ArgumentException("Sənəd seçilməlidir.", nameof(documentId));
        if (productId == Guid.Empty) throw new ArgumentException("Məhsul seçilməlidir.", nameof(productId));
        if (qty <= 0) throw new ArgumentOutOfRangeException(nameof(qty), "Miqdar sıfırdan böyük olmalıdır.");
        if (price < 0) throw new ArgumentOutOfRangeException(nameof(price), "Qiymət mənfi ola bilməz.");

        Id = Guid.NewGuid();
        DocumentId = documentId;
        ProductId = productId;
        Qty = qty;
        Price = price;
    }

    public Guid Id { get; private set; }
    public Guid DocumentId { get; private set; }
    public Document Document { get; private set; } = null!;
    public Guid ProductId { get; private set; }
    public Product Product { get; private set; } = null!;
    public decimal Qty { get; private set; }
    public decimal Price { get; private set; }
}
