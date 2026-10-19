using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace StockManagement.Infrastructure.Persistence;

public sealed class StockManagementDbContextFactory : IDesignTimeDbContextFactory<StockManagementDbContext>
{
    public StockManagementDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<StockManagementDbContext>()
            .UseSqlServer("Server=Revan;Database=StockManagmentDb;Trusted_Connection=True;TrustServerCertificate=True;")
            .Options;
        return new StockManagementDbContext(options);
    }
}
