using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NoxStock.Migrations
{
    /// <inheritdoc />
    public partial class Add_Product_Pricing_Pipeline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "ExchangeRateAtPurchase",
                table: "AppProducts",
                type: "numeric(18,6)",
                nullable: false,
                defaultValue: 1m);

            migrationBuilder.AddColumn<decimal>(
                name: "PurchaseDiscount1",
                table: "AppProducts",
                type: "numeric(9,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "PurchaseDiscount2",
                table: "AppProducts",
                type: "numeric(9,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "PurchaseDiscount3",
                table: "AppProducts",
                type: "numeric(9,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "PurchaseDiscount4",
                table: "AppProducts",
                type: "numeric(9,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "PurchaseListPrice",
                table: "AppProducts",
                type: "numeric(18,6)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "SalesDiscount1",
                table: "AppProducts",
                type: "numeric(9,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "SalesDiscount2",
                table: "AppProducts",
                type: "numeric(9,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "SalesDiscount3",
                table: "AppProducts",
                type: "numeric(9,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "SalesDiscount4",
                table: "AppProducts",
                type: "numeric(9,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "VatRate",
                table: "AppProducts",
                type: "integer",
                nullable: false,
                defaultValue: 20);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExchangeRateAtPurchase",
                table: "AppProducts");

            migrationBuilder.DropColumn(
                name: "PurchaseDiscount1",
                table: "AppProducts");

            migrationBuilder.DropColumn(
                name: "PurchaseDiscount2",
                table: "AppProducts");

            migrationBuilder.DropColumn(
                name: "PurchaseDiscount3",
                table: "AppProducts");

            migrationBuilder.DropColumn(
                name: "PurchaseDiscount4",
                table: "AppProducts");

            migrationBuilder.DropColumn(
                name: "PurchaseListPrice",
                table: "AppProducts");

            migrationBuilder.DropColumn(
                name: "SalesDiscount1",
                table: "AppProducts");

            migrationBuilder.DropColumn(
                name: "SalesDiscount2",
                table: "AppProducts");

            migrationBuilder.DropColumn(
                name: "SalesDiscount3",
                table: "AppProducts");

            migrationBuilder.DropColumn(
                name: "SalesDiscount4",
                table: "AppProducts");

            migrationBuilder.DropColumn(
                name: "VatRate",
                table: "AppProducts");
        }
    }
}
