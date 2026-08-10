using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SalesApi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSalesListIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ix_sales_branch_id",
                table: "sales",
                column: "branch_id");

            migrationBuilder.CreateIndex(
                name: "ix_sales_customer_id",
                table: "sales",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "ix_sales_sale_date",
                table: "sales",
                column: "SaleDate");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_sales_branch_id",
                table: "sales");

            migrationBuilder.DropIndex(
                name: "ix_sales_customer_id",
                table: "sales");

            migrationBuilder.DropIndex(
                name: "ix_sales_sale_date",
                table: "sales");
        }
    }
}
