using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StockManagement.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGoodsReceipts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "average_cost",
                table: "Products",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "goods_receipts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    grn_number = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    purchase_order_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    warehouse_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    received_date = table.Column<DateOnly>(type: "date", nullable: false),
                    received_by = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    note = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    idempotency_key = table.Column<string>(type: "varchar(128)", unicode: false, maxLength: 128, nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_goods_receipts", x => x.id);
                    table.ForeignKey(
                        name: "FK_goods_receipts_AspNetUsers_received_by",
                        column: x => x.received_by,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_goods_receipts_Warehouses_warehouse_id",
                        column: x => x.warehouse_id,
                        principalTable: "Warehouses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_goods_receipts_purchase_orders_purchase_order_id",
                        column: x => x.purchase_order_id,
                        principalTable: "purchase_orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "goods_receipt_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    goods_receipt_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    purchase_order_item_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    product_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    received_qty = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    rejected_qty = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    batch_no = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    expiry_date = table.Column<DateOnly>(type: "date", nullable: true),
                    unit_cost = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_goods_receipt_items", x => x.id);
                    table.CheckConstraint("CK_goods_receipt_items_any_qty", "[received_qty] + [rejected_qty] > 0");
                    table.CheckConstraint("CK_goods_receipt_items_received_qty", "[received_qty] >= 0");
                    table.CheckConstraint("CK_goods_receipt_items_rejected_qty", "[rejected_qty] >= 0");
                    table.CheckConstraint("CK_goods_receipt_items_unit_cost", "[unit_cost] >= 0");
                    table.ForeignKey(
                        name: "FK_goods_receipt_items_Products_product_id",
                        column: x => x.product_id,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_goods_receipt_items_goods_receipts_goods_receipt_id",
                        column: x => x.goods_receipt_id,
                        principalTable: "goods_receipts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_goods_receipt_items_purchase_order_items_purchase_order_item_id",
                        column: x => x.purchase_order_item_id,
                        principalTable: "purchase_order_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_goods_receipt_items_goods_receipt_id_purchase_order_item_id",
                table: "goods_receipt_items",
                columns: new[] { "goods_receipt_id", "purchase_order_item_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_goods_receipt_items_product_id",
                table: "goods_receipt_items",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "IX_goods_receipt_items_purchase_order_item_id",
                table: "goods_receipt_items",
                column: "purchase_order_item_id");

            migrationBuilder.CreateIndex(
                name: "IX_goods_receipts_grn_number",
                table: "goods_receipts",
                column: "grn_number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_goods_receipts_idempotency_key",
                table: "goods_receipts",
                column: "idempotency_key",
                unique: true,
                filter: "[idempotency_key] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_goods_receipts_purchase_order_id_received_date",
                table: "goods_receipts",
                columns: new[] { "purchase_order_id", "received_date" });

            migrationBuilder.CreateIndex(
                name: "IX_goods_receipts_received_by",
                table: "goods_receipts",
                column: "received_by");

            migrationBuilder.CreateIndex(
                name: "IX_goods_receipts_warehouse_id",
                table: "goods_receipts",
                column: "warehouse_id");

            migrationBuilder.Sql("""
                CREATE OR ALTER TRIGGER [dbo].[TR_goods_receipts_AppendOnly]
                ON [dbo].[goods_receipts]
                AFTER UPDATE, DELETE
                AS
                BEGIN
                    SET NOCOUNT ON;
                    THROW 51001, 'Goods receipts are immutable; post a reversal in a future workflow.', 1;
                END;
                """);
            migrationBuilder.Sql("""
                CREATE OR ALTER TRIGGER [dbo].[TR_goods_receipt_items_AppendOnly]
                ON [dbo].[goods_receipt_items]
                AFTER UPDATE, DELETE
                AS
                BEGIN
                    SET NOCOUNT ON;
                    THROW 51002, 'Goods receipt items are immutable.', 1;
                END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS [dbo].[TR_goods_receipt_items_AppendOnly];");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS [dbo].[TR_goods_receipts_AppendOnly];");
            migrationBuilder.DropTable(
                name: "goods_receipt_items");

            migrationBuilder.DropTable(
                name: "goods_receipts");

            migrationBuilder.DropColumn(
                name: "average_cost",
                table: "Products");
        }
    }
}
