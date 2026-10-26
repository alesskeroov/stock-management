using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StockManagement.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGoodsReceiptReversal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "reverse_reason",
                table: "goods_receipts",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "reversed_at",
                table: "goods_receipts",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "reversed_by",
                table: "goods_receipts",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_goods_receipts_reversed_by",
                table: "goods_receipts",
                column: "reversed_by");

            migrationBuilder.AddForeignKey(
                name: "FK_goods_receipts_AspNetUsers_reversed_by",
                table: "goods_receipts",
                column: "reversed_by",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.Sql("DROP TRIGGER IF EXISTS [dbo].[TR_goods_receipts_AppendOnly];");
            migrationBuilder.Sql("""
                CREATE TRIGGER [dbo].[TR_goods_receipts_AppendOnly]
                ON [dbo].[goods_receipts]
                AFTER UPDATE, DELETE
                AS
                BEGIN
                    SET NOCOUNT ON;
                    IF EXISTS (SELECT 1 FROM deleted d LEFT JOIN inserted i ON i.id = d.id WHERE i.id IS NULL)
                        THROW 51001, 'Goods receipts cannot be deleted.', 1;
                    IF EXISTS (
                        SELECT 1 FROM inserted i INNER JOIN deleted d ON d.id = i.id
                        WHERE d.reversed_at IS NOT NULL OR i.reversed_at IS NULL OR i.reversed_by IS NULL
                           OR NULLIF(LTRIM(RTRIM(i.reverse_reason)), '') IS NULL
                           OR EXISTS (
                               SELECT i.id, i.grn_number, i.purchase_order_id, i.warehouse_id, i.received_date,
                                      i.received_by, i.note, i.idempotency_key, i.created_at
                               EXCEPT
                               SELECT d.id, d.grn_number, d.purchase_order_id, d.warehouse_id, d.received_date,
                                      d.received_by, d.note, d.idempotency_key, d.created_at))
                        THROW 51001, 'Goods receipt contents are immutable; only one reversal record is allowed.', 1;
                END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS [dbo].[TR_goods_receipts_AppendOnly];");
            migrationBuilder.Sql("""
                CREATE OR ALTER TRIGGER [dbo].[TR_goods_receipts_AppendOnly]
                ON [dbo].[goods_receipts]
                AFTER UPDATE, DELETE AS
                BEGIN
                    SET NOCOUNT ON;
                    THROW 51001, 'Goods receipts are immutable.', 1;
                END;
                """);
            migrationBuilder.DropForeignKey(
                name: "FK_goods_receipts_AspNetUsers_reversed_by",
                table: "goods_receipts");

            migrationBuilder.DropIndex(
                name: "IX_goods_receipts_reversed_by",
                table: "goods_receipts");

            migrationBuilder.DropColumn(
                name: "reverse_reason",
                table: "goods_receipts");

            migrationBuilder.DropColumn(
                name: "reversed_at",
                table: "goods_receipts");

            migrationBuilder.DropColumn(
                name: "reversed_by",
                table: "goods_receipts");
        }
    }
}
