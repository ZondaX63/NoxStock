using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NoxStock.Migrations
{
    /// <inheritdoc />
    public partial class Add_Business_Partner_Transfers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppBusinessPartnerTransfers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    SupplierId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReferenceNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    TransferDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("PK_AppBusinessPartnerTransfers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppBusinessPartnerTransfers_AppBusinessPartners_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "AppBusinessPartners",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppBusinessPartnerTransfers_AppBusinessPartners_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "AppBusinessPartners",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppBusinessPartnerTransfers_CustomerId",
                table: "AppBusinessPartnerTransfers",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_AppBusinessPartnerTransfers_SupplierId",
                table: "AppBusinessPartnerTransfers",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_AppBusinessPartnerTransfers_TenantId_CustomerId_TransferDate",
                table: "AppBusinessPartnerTransfers",
                columns: new[] { "TenantId", "CustomerId", "TransferDate" });

            migrationBuilder.CreateIndex(
                name: "IX_AppBusinessPartnerTransfers_TenantId_ReferenceNumber",
                table: "AppBusinessPartnerTransfers",
                columns: new[] { "TenantId", "ReferenceNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppBusinessPartnerTransfers_TenantId_SupplierId_TransferDate",
                table: "AppBusinessPartnerTransfers",
                columns: new[] { "TenantId", "SupplierId", "TransferDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppBusinessPartnerTransfers");
        }
    }
}
