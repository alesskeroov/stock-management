using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StockManagement.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddStockLogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "stock_logs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    product_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    product_name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    type = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
                    quantity_change = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    quantity_before = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    quantity_after = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    created_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stock_logs", x => x.id);
                    table.ForeignKey(
                        name: "FK_stock_logs_AspNetUsers_created_by",
                        column: x => x.created_by,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_stock_logs_Products_product_id",
                        column: x => x.product_id,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_stock_logs_created_at",
                table: "stock_logs",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_stock_logs_created_by",
                table: "stock_logs",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "IX_stock_logs_product_created_at",
                table: "stock_logs",
                columns: new[] { "product_id", "created_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "stock_logs");
        }
    }
}
