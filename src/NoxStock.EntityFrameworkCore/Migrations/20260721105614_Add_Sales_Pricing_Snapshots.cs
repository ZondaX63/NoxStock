using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NoxStock.Migrations
{
    /// <inheritdoc />
    public partial class Add_Sales_Pricing_Snapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "AppliedMarkupPercent",
                table: "AppDocumentItems",
                type: "numeric(9,4)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CostCurrencyAmount",
                table: "AppDocumentItems",
                type: "numeric(18,6)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CostCurrencyCode",
                table: "AppDocumentItems",
                type: "character varying(3)",
                maxLength: 3,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CostTryAmount",
                table: "AppDocumentItems",
                type: "numeric(18,6)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PriceRateDate",
                table: "AppDocumentItems",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PricingSnapshotDate",
                table: "AppDocumentItems",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReferenceSaleCurrencyCode",
                table: "AppDocumentItems",
                type: "character varying(3)",
                maxLength: 3,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ReferenceSaleExchangeRate",
                table: "AppDocumentItems",
                type: "numeric(18,6)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ReferenceSaleUnitPrice",
                table: "AppDocumentItems",
                type: "numeric(18,6)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "UnitPriceTry",
                table: "AppDocumentItems",
                type: "numeric(18,6)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AppliedMarkupPercent",
                table: "AppDocumentItems");

            migrationBuilder.DropColumn(
                name: "CostCurrencyAmount",
                table: "AppDocumentItems");

            migrationBuilder.DropColumn(
                name: "CostCurrencyCode",
                table: "AppDocumentItems");

            migrationBuilder.DropColumn(
                name: "CostTryAmount",
                table: "AppDocumentItems");

            migrationBuilder.DropColumn(
                name: "PriceRateDate",
                table: "AppDocumentItems");

            migrationBuilder.DropColumn(
                name: "PricingSnapshotDate",
                table: "AppDocumentItems");

            migrationBuilder.DropColumn(
                name: "ReferenceSaleCurrencyCode",
                table: "AppDocumentItems");

            migrationBuilder.DropColumn(
                name: "ReferenceSaleExchangeRate",
                table: "AppDocumentItems");

            migrationBuilder.DropColumn(
                name: "ReferenceSaleUnitPrice",
                table: "AppDocumentItems");

            migrationBuilder.DropColumn(
                name: "UnitPriceTry",
                table: "AppDocumentItems");
        }
    }
}
