using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NoxStock.Migrations
{
    /// <summary>
    /// Stores the product sales-discount chain on the document line so that
    /// later product-card changes cannot alter a financial document's audit trail.
    /// </summary>
    public partial class Add_Sales_Discount_Snapshots_And_Historical_Profitability : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "SalesDiscount1",
                table: "AppDocumentItems",
                type: "decimal(9,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "SalesDiscount2",
                table: "AppDocumentItems",
                type: "decimal(9,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "SalesDiscount3",
                table: "AppDocumentItems",
                type: "decimal(9,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "SalesDiscount4",
                table: "AppDocumentItems",
                type: "decimal(9,4)",
                nullable: false,
                defaultValue: 0m);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "SalesDiscount1", table: "AppDocumentItems");
            migrationBuilder.DropColumn(name: "SalesDiscount2", table: "AppDocumentItems");
            migrationBuilder.DropColumn(name: "SalesDiscount3", table: "AppDocumentItems");
            migrationBuilder.DropColumn(name: "SalesDiscount4", table: "AppDocumentItems");
        }
    }
}
