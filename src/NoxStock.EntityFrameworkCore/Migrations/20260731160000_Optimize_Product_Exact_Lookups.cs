using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NoxStock.Migrations
{
    public partial class Optimize_Product_Exact_Lookups : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_AppProducts_TenantId_Barcode",
                table: "AppProducts",
                columns: new[] { "TenantId", "Barcode" });

            migrationBuilder.CreateIndex(
                name: "IX_AppProducts_TenantId_Sku",
                table: "AppProducts",
                columns: new[] { "TenantId", "Sku" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AppProducts_TenantId_Barcode",
                table: "AppProducts");

            migrationBuilder.DropIndex(
                name: "IX_AppProducts_TenantId_Sku",
                table: "AppProducts");
        }
    }
}
