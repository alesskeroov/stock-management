using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using StockManagement.Infrastructure.Persistence;

#nullable disable

namespace StockManagement.Infrastructure.Persistence.Migrations;

[DbContext(typeof(StockManagementDbContext))]
[Migration("20261016150000_AddProductEntryDate")]
public sealed class AddProductEntryDate : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTime>(
            name: "EntryDate",
            table: "Products",
            type: "datetime2",
            nullable: false,
            defaultValueSql: "SYSUTCDATETIME()");

        // Assign stable, varied demo dates to existing products within the last six months.
        migrationBuilder.Sql("""
            UPDATE [Products]
            SET [EntryDate] = DATEADD(SECOND, -ABS(CHECKSUM(NEWID()) % 15552000), SYSUTCDATETIME());
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropColumn(name: "EntryDate", table: "Products");
}
