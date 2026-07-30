using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NoxStock.Migrations
{
    /// <inheritdoc />
    public partial class Single_Reference_Currency_And_Try_Sales : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM "AppProducts" p
                        LEFT JOIN "AppExchangeRates" r
                            ON r."Currency" = UPPER(COALESCE(p."SaleCurrency", 'TRY'))
                        WHERE p."PricingMode" = 0
                          AND UPPER(COALESCE(p."SaleCurrency", 'TRY')) <> 'TRY'
                          AND (r."Rate" IS NULL OR r."Rate" <= 0)
                    ) THEN
                        RAISE EXCEPTION 'Manual foreign-currency product prices cannot be converted to TRY because a current exchange rate is missing.';
                    END IF;
                END $$;

                UPDATE "AppProducts" p
                SET "SalePrice" = ROUND(p."SalePrice" * r."Rate", 4)
                FROM "AppExchangeRates" r
                WHERE p."PricingMode" = 0
                  AND UPPER(COALESCE(p."SaleCurrency", 'TRY')) <> 'TRY'
                  AND r."Currency" = UPPER(p."SaleCurrency");

                UPDATE "AppProducts"
                SET "PurchaseCurrency" =
                    CASE
                        WHEN "PricingMode" = 1 OR "LastPurchaseCostHistoryId" IS NOT NULL
                            THEN COALESCE(NULLIF(UPPER("PricingCurrencyCode"), ''), NULLIF(UPPER("PurchaseCurrency"), ''), 'TRY')
                        ELSE COALESCE(NULLIF(UPPER("PurchaseCurrency"), ''), 'TRY')
                    END;

                UPDATE "AppDocumentItems" i
                SET "UnitPrice" = ROUND(i."UnitPrice" * d."ExchangeRate", 4),
                    "Total" = ROUND(i."Total" * d."ExchangeRate", 4),
                    "PriceCurrencyCode" = 'TRY',
                    "PriceExchangeRate" = 1,
                    "UnitPriceTry" = ROUND(i."UnitPrice" * d."ExchangeRate", 6)
                FROM "AppDocuments" d
                WHERE i."DocumentId" = d."Id"
                  AND d."Status" IN (1, 2)
                  AND d."Type" IN (1, 3, 5, 6, 8)
                  AND UPPER(COALESCE(d."Currency", 'TRY')) <> 'TRY';

                UPDATE "AppDocuments"
                SET "Subtotal" = ROUND("Subtotal" * "ExchangeRate", 4),
                    "VatTotal" = ROUND("VatTotal" * "ExchangeRate", 4),
                    "DiscountTotal" = ROUND("DiscountTotal" * "ExchangeRate", 4),
                    "GrandTotal" = ROUND("GrandTotal" * "ExchangeRate", 4),
                    "PaidAmount" = ROUND("PaidAmount" * "ExchangeRate", 4),
                    "Currency" = 'TRY',
                    "ExchangeRate" = 1
                WHERE "Status" IN (1, 2)
                  AND "Type" IN (1, 3, 5, 6, 8)
                  AND UPPER(COALESCE("Currency", 'TRY')) <> 'TRY';
                """);

            migrationBuilder.DropColumn(
                name: "Currency",
                table: "AppProducts");

            migrationBuilder.DropColumn(
                name: "PricingCurrencyCode",
                table: "AppProducts");

            migrationBuilder.DropColumn(
                name: "SaleCurrency",
                table: "AppProducts");

            migrationBuilder.RenameColumn(
                name: "PricingCurrencyEquivalentCost",
                table: "AppProductCostHistories",
                newName: "ReferenceCurrencyEquivalentCost");

            migrationBuilder.RenameColumn(
                name: "PricingCurrencyCode",
                table: "AppProductCostHistories",
                newName: "ReferenceCurrencyCode");

            migrationBuilder.RenameColumn(
                name: "ReferenceSaleUnitPrice",
                table: "AppDocumentItems",
                newName: "ReferenceUnitPrice");

            migrationBuilder.RenameColumn(
                name: "ReferenceSaleExchangeRate",
                table: "AppDocumentItems",
                newName: "ReferenceExchangeRate");

            migrationBuilder.RenameColumn(
                name: "ReferenceSaleCurrencyCode",
                table: "AppDocumentItems",
                newName: "ReferenceCurrencyCode");

            migrationBuilder.AddColumn<decimal>(
                name: "PurchaseDiscount1",
                table: "AppDocumentItems",
                type: "numeric(9,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "PurchaseDiscount2",
                table: "AppDocumentItems",
                type: "numeric(9,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "PurchaseDiscount3",
                table: "AppDocumentItems",
                type: "numeric(9,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "PurchaseDiscount4",
                table: "AppDocumentItems",
                type: "numeric(9,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.Sql(
                """
                UPDATE "AppDocumentItems" i
                SET "PurchaseDiscount1" = i."DiscountRate"
                FROM "AppDocuments" d
                WHERE i."DocumentId" = d."Id"
                  AND d."Type" IN (2, 4)
                  AND i."DiscountRate" <> 0;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PurchaseDiscount1",
                table: "AppDocumentItems");

            migrationBuilder.DropColumn(
                name: "PurchaseDiscount2",
                table: "AppDocumentItems");

            migrationBuilder.DropColumn(
                name: "PurchaseDiscount3",
                table: "AppDocumentItems");

            migrationBuilder.DropColumn(
                name: "PurchaseDiscount4",
                table: "AppDocumentItems");

            migrationBuilder.RenameColumn(
                name: "ReferenceCurrencyEquivalentCost",
                table: "AppProductCostHistories",
                newName: "PricingCurrencyEquivalentCost");

            migrationBuilder.RenameColumn(
                name: "ReferenceCurrencyCode",
                table: "AppProductCostHistories",
                newName: "PricingCurrencyCode");

            migrationBuilder.RenameColumn(
                name: "ReferenceUnitPrice",
                table: "AppDocumentItems",
                newName: "ReferenceSaleUnitPrice");

            migrationBuilder.RenameColumn(
                name: "ReferenceExchangeRate",
                table: "AppDocumentItems",
                newName: "ReferenceSaleExchangeRate");

            migrationBuilder.RenameColumn(
                name: "ReferenceCurrencyCode",
                table: "AppDocumentItems",
                newName: "ReferenceSaleCurrencyCode");

            migrationBuilder.AddColumn<string>(
                name: "Currency",
                table: "AppProducts",
                type: "character varying(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "TRY");

            migrationBuilder.AddColumn<string>(
                name: "PricingCurrencyCode",
                table: "AppProducts",
                type: "character varying(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "TRY");

            migrationBuilder.AddColumn<string>(
                name: "SaleCurrency",
                table: "AppProducts",
                type: "character varying(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "TRY");
        }
    }
}
