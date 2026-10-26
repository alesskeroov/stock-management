using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using StockManagement.Infrastructure.Persistence;

#nullable disable

namespace StockManagement.Infrastructure.Persistence.Migrations;

[DbContext(typeof(StockManagementDbContext))]
[Migration("20261022144000_AddStockNotifications")]
public partial class AddStockNotifications : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<decimal>(
            name: "ReorderLevel",
            table: "Products",
            type: "decimal(18,3)",
            precision: 18,
            scale: 3,
            nullable: false,
            defaultValue: 10m);

        migrationBuilder.CreateTable(
            name: "stock_notifications",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Message = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                Type = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                IsRead = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_stock_notifications", x => x.Id);
                table.ForeignKey("FK_stock_notifications_Products_ProductId", x => x.ProductId,
                    "Products", "Id", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex("IX_stock_notifications_ProductId_IsRead", "stock_notifications",
            new[] { "ProductId", "IsRead" });
        migrationBuilder.CreateIndex("IX_stock_notifications_CreatedAt", "stock_notifications", "CreatedAt");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "stock_notifications");
        migrationBuilder.DropColumn(name: "ReorderLevel", table: "Products");
    }
}
