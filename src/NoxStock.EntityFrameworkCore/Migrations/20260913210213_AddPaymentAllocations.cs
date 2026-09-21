using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NoxStock.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentAllocations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "LegacyPaidAmountBaseline",
                table: "AppDocuments",
                type: "numeric(18,4)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AppPaymentAllocations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    AccountTransactionId = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    BusinessPartnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    AllocationDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    ReversedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ReversedBy = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("PK_AppPaymentAllocations", x => x.Id);
                    table.CheckConstraint("CK_AppPaymentAllocations_Amount_Positive", "\"Amount\" > 0");
                    table.ForeignKey(
                        name: "FK_AppPaymentAllocations_AppAccountTransactions_AccountTransac~",
                        column: x => x.AccountTransactionId,
                        principalTable: "AppAccountTransactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppPaymentAllocations_AppBusinessPartners_BusinessPartnerId",
                        column: x => x.BusinessPartnerId,
                        principalTable: "AppBusinessPartners",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppPaymentAllocations_AppDocuments_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "AppDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppPaymentAllocations_AccountTransactionId",
                table: "AppPaymentAllocations",
                column: "AccountTransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_AppPaymentAllocations_BusinessPartnerId",
                table: "AppPaymentAllocations",
                column: "BusinessPartnerId");

            migrationBuilder.CreateIndex(
                name: "IX_AppPaymentAllocations_DocumentId",
                table: "AppPaymentAllocations",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_AppPaymentAllocations_TenantId_AccountTransactionId_Documen~",
                table: "AppPaymentAllocations",
                columns: new[] { "TenantId", "AccountTransactionId", "DocumentId" },
                unique: true,
                filter: "\"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "IX_AppPaymentAllocations_TenantId_AccountTransactionId_Status",
                table: "AppPaymentAllocations",
                columns: new[] { "TenantId", "AccountTransactionId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_AppPaymentAllocations_TenantId_DocumentId_Status_Allocation~",
                table: "AppPaymentAllocations",
                columns: new[] { "TenantId", "DocumentId", "Status", "AllocationDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppPaymentAllocations");

            migrationBuilder.DropColumn(
                name: "LegacyPaidAmountBaseline",
                table: "AppDocuments");
        }
    }
}
