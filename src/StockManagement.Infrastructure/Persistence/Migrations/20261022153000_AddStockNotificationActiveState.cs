using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using StockManagement.Infrastructure.Persistence;

#nullable disable

namespace StockManagement.Infrastructure.Persistence.Migrations;

[DbContext(typeof(StockManagementDbContext))]
[Migration("20261022153000_AddStockNotificationActiveState")]
public partial class AddStockNotificationActiveState : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.AddColumn<bool>(
            name: "IsActive",
            table: "stock_notifications",
            type: "bit",
            nullable: false,
            defaultValue: true);

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropColumn(name: "IsActive", table: "stock_notifications");
}
