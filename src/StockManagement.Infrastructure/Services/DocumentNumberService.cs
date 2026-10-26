using System.Collections.Concurrent;
using System.Data;
using Microsoft.EntityFrameworkCore;
using StockManagement.Domain.Entities;
using StockManagement.Infrastructure.Persistence;

namespace StockManagement.Infrastructure.Services;

public sealed class DocumentNumberService(StockManagementDbContext db)
{
    private static readonly ConcurrentDictionary<(string Type, int Year), SemaphoreSlim> LocalLocks = new();
    private static readonly HashSet<string> SupportedTypes = ["PO", "GRN", "DX", "XQ"];

    public Task<string> GetNextStockDocumentNumberAsync(string documentType, CancellationToken cancellationToken = default)
    {
        var type = documentType.Trim().ToLowerInvariant() switch
        {
            "in" => "DX",
            "out" => "XQ",
            _ => throw new ArgumentException("Sənəd növü in və ya out olmalıdır.", nameof(documentType))
        };
        return GetNextAsync(type, 0, cancellationToken);
    }

    public Task<string> GetNextAsync(string type, CancellationToken cancellationToken = default) =>
        GetNextAsync(type, DateTime.UtcNow.Year, cancellationToken);

    public async Task<string> GetNextAsync(string type, int year, CancellationToken cancellationToken = default)
    {
        type = type.Trim().ToUpperInvariant();
        if (!SupportedTypes.Contains(type)) throw new ArgumentException("Sənəd növü PO və ya GRN olmalıdır.", nameof(type));
        if (type is "DX" or "XQ" ? year != 0 : year is < 2000 or > 9999)
            throw new ArgumentOutOfRangeException(nameof(year));

        var gate = LocalLocks.GetOrAdd((type, year), _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        try
        {
            await using var transaction = db.Database.IsRelational() && db.Database.CurrentTransaction is null
                ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken)
                : null;
            int number;
            if (db.Database.IsSqlServer())
            {
                await db.Database.ExecuteSqlInterpolatedAsync($"""
                    UPDATE [dbo].[document_sequences] WITH (UPDLOCK, HOLDLOCK)
                    SET [last_number] = [last_number] + 1
                    WHERE [type] = {type} AND [year] = {year};
                    IF @@ROWCOUNT = 0
                        INSERT INTO [dbo].[document_sequences] ([type], [year], [last_number])
                        VALUES ({type}, {year}, 1);
                    """, cancellationToken);
                number = await db.DocumentSequences.AsNoTracking()
                    .Where(x => x.Type == type && x.Year == year)
                    .Select(x => x.LastNumber).SingleAsync(cancellationToken);
            }
            else
            {
                var sequence = await db.DocumentSequences.SingleOrDefaultAsync(
                    x => x.Type == type && x.Year == year, cancellationToken);
                if (sequence is null)
                {
                    sequence = new DocumentSequence(type, year, 1);
                    db.DocumentSequences.Add(sequence);
                    number = 1;
                }
                else number = sequence.Advance();
                await db.SaveChangesAsync(cancellationToken);
            }

            if (transaction is not null) await transaction.CommitAsync(cancellationToken);
            return type is "DX" or "XQ" ? $"{type}-{number:0000}" : $"{type}-{year}-{number:0000}";
        }
        finally { gate.Release(); }
    }
}
