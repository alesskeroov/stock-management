namespace StockManagement.Domain.Entities;

public sealed class DocumentSequence
{
    private DocumentSequence() { }

    public DocumentSequence(string type, int year, int lastNumber)
    {
        Type = type;
        Year = year;
        LastNumber = lastNumber;
    }

    public string Type { get; private set; } = string.Empty;
    public int Year { get; private set; }
    public int LastNumber { get; private set; }

    public int Advance() => ++LastNumber;
}
