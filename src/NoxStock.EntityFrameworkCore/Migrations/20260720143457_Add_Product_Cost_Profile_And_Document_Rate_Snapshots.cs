using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NoxStock.Migrations
{
    /// <inheritdoc />
    public partial class Add_Product_Cost_Profile_And_Document_Rate_Snapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LastCostUpdatedAt",
                table: "AppProducts",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "LastPurchaseCostCurrencyAmount",
                table: "AppProducts",
                type: "numeric(18,6)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastPurchaseCostCurrencyCode",
                table: "AppProducts",
                type: "character varying(3)",
                maxLength: 3,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LastPurchaseCostHistoryId",
                table: "AppProducts",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "LastPurchaseCostTryAmount",
                table: "AppProducts",
                type: "numeric(18,6)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastPurchaseDate",
                table: "AppProducts",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LastPurchaseDocumentId",
                table: "AppProducts",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LastPurchaseDocumentItemId",
                table: "AppProducts",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "LastPurchaseExchangeRate",
                table: "AppProducts",
                type: "numeric(18,6)",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LastPurchaseSupplierId",
                table: "AppProducts",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PricingCurrencyCode",
                table: "AppProducts",
                type: "character varying(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "TRY");

            migrationBuilder.AddColumn<int>(
                name: "PricingMode",
                table: "AppProducts",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "IsUnitPriceOverridden",
                table: "AppDocumentItems",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "PriceCurrencyCode",
                table: "AppDocumentItems",
                type: "character varying(3)",
                maxLength: 3,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PriceExchangeRate",
                table: "AppDocumentItems",
                type: "numeric(18,6)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PricingMode",
                table: "AppDocumentItems",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ProductCostHistoryId",
                table: "AppDocumentItems",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AppDocumentExchangeRateSnapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    DocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    CurrencyCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    RateToTry = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    RateDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppDocumentExchangeRateSnapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppDocumentExchangeRateSnapshots_AppDocuments_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "AppDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AppProductCostHistories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceDocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceDocumentItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    SupplierId = table.Column<Guid>(type: "uuid", nullable: true),
                    CostDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    SourceCurrencyCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    SourceNetUnitCost = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    SourceExchangeRate = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    TryEquivalentNetUnitCost = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    PricingCurrencyCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    PricingCurrencyEquivalentCost = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    ChangeType = table.Column<int>(type: "integer", nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppProductCostHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppProductCostHistories_AppBusinessPartners_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "AppBusinessPartners",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_AppProductCostHistories_AppDocumentItems_SourceDocumentItem~",
                        column: x => x.SourceDocumentItemId,
                        principalTable: "AppDocumentItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AppProductCostHistories_AppDocuments_SourceDocumentId",
                        column: x => x.SourceDocumentId,
                        principalTable: "AppDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AppProductCostHistories_AppProducts_ProductId",
                        column: x => x.ProductId,
                        principalTable: "AppProducts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppDocumentItems_ProductCostHistoryId",
                table: "AppDocumentItems",
                column: "ProductCostHistoryId");

            migrationBuilder.CreateIndex(
                name: "IX_AppDocumentExchangeRateSnapshots_DocumentId",
                table: "AppDocumentExchangeRateSnapshots",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_AppDocumentExchangeRateSnapshots_TenantId_DocumentId_Curren~",
                table: "AppDocumentExchangeRateSnapshots",
                columns: new[] { "TenantId", "DocumentId", "CurrencyCode" },
                unique: true,
                filter: "\"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "IX_AppProductCostHistories_ProductId",
                table: "AppProductCostHistories",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_AppProductCostHistories_SourceDocumentId",
                table: "AppProductCostHistories",
                column: "SourceDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_AppProductCostHistories_SourceDocumentItemId",
                table: "AppProductCostHistories",
                column: "SourceDocumentItemId");

            migrationBuilder.CreateIndex(
                name: "IX_AppProductCostHistories_SupplierId",
                table: "AppProductCostHistories",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_AppProductCostHistories_TenantId_ProductId_CostDate",
                table: "AppProductCostHistories",
                columns: new[] { "TenantId", "ProductId", "CostDate" });

            migrationBuilder.CreateIndex(
                name: "IX_AppProductCostHistories_TenantId_SourceDocumentItemId_Chang~",
                table: "AppProductCostHistories",
                columns: new[] { "TenantId", "SourceDocumentItemId", "ChangeType" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_AppDocumentItems_AppProductCostHistories_ProductCostHistory~",
                table: "AppDocumentItems",
                column: "ProductCostHistoryId",
                principalTable: "AppProductCostHistories",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AppDocumentItems_AppProductCostHistories_ProductCostHistory~",
                table: "AppDocumentItems");

            migrationBuilder.DropTable(
                name: "AppDocumentExchangeRateSnapshots");

            migrationBuilder.DropTable(
                name: "AppProductCostHistories");

            migrationBuilder.DropIndex(
                name: "IX_AppDocumentItems_ProductCostHistoryId",
                table: "AppDocumentItems");

            migrationBuilder.DropColumn(
                name: "LastCostUpdatedAt",
                table: "AppProducts");

            migrationBuilder.DropColumn(
                name: "LastPurchaseCostCurrencyAmount",
                table: "AppProducts");

            migrationBuilder.DropColumn(
                name: "LastPurchaseCostCurrencyCode",
                table: "AppProducts");

            migrationBuilder.DropColumn(
                name: "LastPurchaseCostHistoryId",
                table: "AppProducts");

            migrationBuilder.DropColumn(
                name: "LastPurchaseCostTryAmount",
                table: "AppProducts");

            migrationBuilder.DropColumn(
                name: "LastPurchaseDate",
                table: "AppProducts");

            migrationBuilder.DropColumn(
                name: "LastPurchaseDocumentId",
                table: "AppProducts");

            migrationBuilder.DropColumn(
                name: "LastPurchaseDocumentItemId",
                table: "AppProducts");

            migrationBuilder.DropColumn(
                name: "LastPurchaseExchangeRate",
                table: "AppProducts");

            migrationBuilder.DropColumn(
                name: "LastPurchaseSupplierId",
                table: "AppProducts");

            migrationBuilder.DropColumn(
                name: "PricingCurrencyCode",
                table: "AppProducts");

            migrationBuilder.DropColumn(
                name: "PricingMode",
                table: "AppProducts");

            migrationBuilder.DropColumn(
                name: "IsUnitPriceOverridden",
                table: "AppDocumentItems");

            migrationBuilder.DropColumn(
                name: "PriceCurrencyCode",
                table: "AppDocumentItems");

            migrationBuilder.DropColumn(
                name: "PriceExchangeRate",
                table: "AppDocumentItems");

            migrationBuilder.DropColumn(
                name: "PricingMode",
                table: "AppDocumentItems");

            migrationBuilder.DropColumn(
                name: "ProductCostHistoryId",
                table: "AppDocumentItems");
        }
    }
}
