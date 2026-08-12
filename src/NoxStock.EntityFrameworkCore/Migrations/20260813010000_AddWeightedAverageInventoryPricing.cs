using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using NoxStock.EntityFrameworkCore;

#nullable disable

namespace NoxStock.Migrations;

[DbContext(typeof(NoxStockDbContext))]
[Migration("20260813010000_AddWeightedAverageInventoryPricing")]
public partial class AddWeightedAverageInventoryPricing : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<decimal>(
            name: "AverageInventoryCostTry",
            table: "AppProducts",
            type: "numeric(18,2)",
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "InventoryCostUpdatedAt",
            table: "AppProducts",
            type: "timestamp without time zone",
            nullable: true);

        migrationBuilder.AddColumn<decimal>(name: "Quantity", table: "AppProductCostHistories", type: "numeric(18,4)", nullable: false, defaultValue: 0m);
        migrationBuilder.AddColumn<decimal>(name: "QuantityBefore", table: "AppProductCostHistories", type: "numeric(18,4)", nullable: true);
        migrationBuilder.AddColumn<decimal>(name: "AverageCostBeforeTry", table: "AppProductCostHistories", type: "numeric(18,2)", nullable: true);
        migrationBuilder.AddColumn<decimal>(name: "QuantityAfter", table: "AppProductCostHistories", type: "numeric(18,4)", nullable: true);
        migrationBuilder.AddColumn<decimal>(name: "AverageCostAfterTry", table: "AppProductCostHistories", type: "numeric(18,2)", nullable: true);

        migrationBuilder.AddColumn<decimal>(
            name: "TotalCostTryAmount",
            table: "AppDocumentItems",
            type: "numeric(18,2)",
            nullable: true);

        // Yalnızca onaylı son alış maliyeti bulunan mevcut stoklarda güvenilir
        // bir başlangıç değeri üretir. Kaynağı olmayan ürünler tahmin edilmez.
        migrationBuilder.Sql("""
            UPDATE "AppProducts"
            SET "AverageInventoryCostTry" = ROUND("LastPurchaseCostTryAmount", 2),
                "InventoryCostUpdatedAt" = COALESCE("LastCostUpdatedAt", NOW())
            WHERE "Quantity" > 0
              AND "LastPurchaseCostTryAmount" IS NOT NULL
              AND "LastPurchaseCostTryAmount" > 0;
            """);

        migrationBuilder.Sql("""
            UPDATE "AppDocumentItems"
            SET "TotalCostTryAmount" = ROUND("CostTryAmount" * "Quantity", 2)
            WHERE "CostTryAmount" IS NOT NULL;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "AverageInventoryCostTry", table: "AppProducts");
        migrationBuilder.DropColumn(name: "InventoryCostUpdatedAt", table: "AppProducts");
        migrationBuilder.DropColumn(name: "Quantity", table: "AppProductCostHistories");
        migrationBuilder.DropColumn(name: "QuantityBefore", table: "AppProductCostHistories");
        migrationBuilder.DropColumn(name: "AverageCostBeforeTry", table: "AppProductCostHistories");
        migrationBuilder.DropColumn(name: "QuantityAfter", table: "AppProductCostHistories");
        migrationBuilder.DropColumn(name: "AverageCostAfterTry", table: "AppProductCostHistories");
        migrationBuilder.DropColumn(name: "TotalCostTryAmount", table: "AppDocumentItems");
    }
}
