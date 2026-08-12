using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SalesApi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RenameColumnsToSnakeCase : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_sale_items_sales_SaleId",
                table: "sale_items");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "sales",
                newName: "id");

            migrationBuilder.RenameColumn(
                name: "UpdatedAt",
                table: "sales",
                newName: "updated_at");

            migrationBuilder.RenameColumn(
                name: "TotalAmount",
                table: "sales",
                newName: "total_amount");

            migrationBuilder.RenameColumn(
                name: "SaleNumber",
                table: "sales",
                newName: "sale_number");

            migrationBuilder.RenameColumn(
                name: "SaleDate",
                table: "sales",
                newName: "sale_date");

            migrationBuilder.RenameColumn(
                name: "IsCancelled",
                table: "sales",
                newName: "is_cancelled");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "sales",
                newName: "created_at");

            migrationBuilder.RenameIndex(
                name: "IX_sales_SaleNumber",
                table: "sales",
                newName: "ix_sales_sale_number");

            migrationBuilder.RenameColumn(
                name: "Quantity",
                table: "sale_items",
                newName: "quantity");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "sale_items",
                newName: "id");

            migrationBuilder.RenameColumn(
                name: "UnitPrice",
                table: "sale_items",
                newName: "unit_price");

            migrationBuilder.RenameColumn(
                name: "TotalAmount",
                table: "sale_items",
                newName: "total_amount");

            migrationBuilder.RenameColumn(
                name: "SaleId",
                table: "sale_items",
                newName: "sale_id");

            migrationBuilder.RenameColumn(
                name: "IsCancelled",
                table: "sale_items",
                newName: "is_cancelled");

            migrationBuilder.RenameColumn(
                name: "DiscountPercentage",
                table: "sale_items",
                newName: "discount_percentage");

            migrationBuilder.RenameColumn(
                name: "DiscountAmount",
                table: "sale_items",
                newName: "discount_amount");

            migrationBuilder.RenameIndex(
                name: "IX_sale_items_SaleId",
                table: "sale_items",
                newName: "IX_sale_items_sale_id");

            // Nota: `xmin` é uma coluna de sistema que o PostgreSQL já mantém em toda tabela
            // (mapeada por SaleConfiguration desde 006-cancelar-venda, sem migration própria). O
            // scaffolding desta migration gerou um AddColumn/DropColumn para "xmin" só porque o
            // model snapshot nunca havia registrado essa propriedade até agora — não porque a
            // coluna precise ser criada. Removido manualmente: `ALTER TABLE ... ADD COLUMN xmin`
            // falharia no Postgres com "column name xmin conflicts with a system column name".

            migrationBuilder.AddForeignKey(
                name: "FK_sale_items_sales_sale_id",
                table: "sale_items",
                column: "sale_id",
                principalTable: "sales",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_sale_items_sales_sale_id",
                table: "sale_items");

            migrationBuilder.RenameColumn(
                name: "id",
                table: "sales",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "updated_at",
                table: "sales",
                newName: "UpdatedAt");

            migrationBuilder.RenameColumn(
                name: "total_amount",
                table: "sales",
                newName: "TotalAmount");

            migrationBuilder.RenameColumn(
                name: "sale_number",
                table: "sales",
                newName: "SaleNumber");

            migrationBuilder.RenameColumn(
                name: "sale_date",
                table: "sales",
                newName: "SaleDate");

            migrationBuilder.RenameColumn(
                name: "is_cancelled",
                table: "sales",
                newName: "IsCancelled");

            migrationBuilder.RenameColumn(
                name: "created_at",
                table: "sales",
                newName: "CreatedAt");

            migrationBuilder.RenameIndex(
                name: "ix_sales_sale_number",
                table: "sales",
                newName: "IX_sales_SaleNumber");

            migrationBuilder.RenameColumn(
                name: "quantity",
                table: "sale_items",
                newName: "Quantity");

            migrationBuilder.RenameColumn(
                name: "id",
                table: "sale_items",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "unit_price",
                table: "sale_items",
                newName: "UnitPrice");

            migrationBuilder.RenameColumn(
                name: "total_amount",
                table: "sale_items",
                newName: "TotalAmount");

            migrationBuilder.RenameColumn(
                name: "sale_id",
                table: "sale_items",
                newName: "SaleId");

            migrationBuilder.RenameColumn(
                name: "is_cancelled",
                table: "sale_items",
                newName: "IsCancelled");

            migrationBuilder.RenameColumn(
                name: "discount_percentage",
                table: "sale_items",
                newName: "DiscountPercentage");

            migrationBuilder.RenameColumn(
                name: "discount_amount",
                table: "sale_items",
                newName: "DiscountAmount");

            migrationBuilder.RenameIndex(
                name: "IX_sale_items_sale_id",
                table: "sale_items",
                newName: "IX_sale_items_SaleId");

            migrationBuilder.AddForeignKey(
                name: "FK_sale_items_sales_SaleId",
                table: "sale_items",
                column: "SaleId",
                principalTable: "sales",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
