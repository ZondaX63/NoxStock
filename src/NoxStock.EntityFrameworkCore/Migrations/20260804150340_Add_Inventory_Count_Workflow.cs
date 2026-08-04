using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NoxStock.Migrations
{
    /// <inheritdoc />
    public partial class Add_Inventory_Count_Workflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsIncompleteProfile",
                table: "AppProducts",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "AppInventoryCounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReferenceNumber = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    CountDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CountedByName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    SubmittedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CompletedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CompletedByName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    CountedProductCount = table.Column<int>(type: "integer", nullable: false),
                    TotalCountedQuantity = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    ZeroedProductCount = table.Column<int>(type: "integer", nullable: false),
                    AdjustedProductCount = table.Column<int>(type: "integer", nullable: false),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
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
                    table.PrimaryKey("PK_AppInventoryCounts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppInventoryCountItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    InventoryCountId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Sku = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Barcode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Unit = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    SystemQuantity = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    CountedQuantity = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsQuickCreatedProduct = table.Column<bool>(type: "boolean", nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppInventoryCountItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppInventoryCountItems_AppInventoryCounts_InventoryCountId",
                        column: x => x.InventoryCountId,
                        principalTable: "AppInventoryCounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AppInventoryCountItems_AppProducts_ProductId",
                        column: x => x.ProductId,
                        principalTable: "AppProducts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppInventoryCountItems_InventoryCountId",
                table: "AppInventoryCountItems",
                column: "InventoryCountId");

            migrationBuilder.CreateIndex(
                name: "IX_AppInventoryCountItems_ProductId",
                table: "AppInventoryCountItems",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_AppInventoryCountItems_TenantId_InventoryCountId_ProductId",
                table: "AppInventoryCountItems",
                columns: new[] { "TenantId", "InventoryCountId", "ProductId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppInventoryCounts_TenantId",
                table: "AppInventoryCounts",
                column: "TenantId",
                unique: true,
                filter: "\"IsDeleted\" = FALSE AND \"Status\" IN (1, 2)");

            migrationBuilder.CreateIndex(
                name: "IX_AppInventoryCounts_TenantId_ReferenceNumber",
                table: "AppInventoryCounts",
                columns: new[] { "TenantId", "ReferenceNumber" },
                unique: true,
                filter: "\"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "IX_AppInventoryCounts_TenantId_Status_CountDate",
                table: "AppInventoryCounts",
                columns: new[] { "TenantId", "Status", "CountDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppInventoryCountItems");

            migrationBuilder.DropTable(
                name: "AppInventoryCounts");

            migrationBuilder.DropColumn(
                name: "IsIncompleteProfile",
                table: "AppProducts");
        }
    }
}
