using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NoxStock.Migrations
{
    /// <inheritdoc />
    public partial class CompleteP0FinanceSafety : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "BalanceAfter",
                table: "AppAccountTransactions",
                type: "numeric(18,4)",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BusinessPartnerId",
                table: "AppAccountTransactions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CancellationReason",
                table: "AppAccountTransactions",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CancelledAt",
                table: "AppAccountTransactions",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CancelledBy",
                table: "AppAccountTransactions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaymentMethod",
                table: "AppAccountTransactions",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReferenceNumber",
                table: "AppAccountTransactions",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RelatedAccountId",
                table: "AppAccountTransactions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReversalOfTransactionId",
                table: "AppAccountTransactions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "AppAccountTransactions",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateTable(
                name: "AppBusinessPartnerOpeningBalances",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    BusinessPartnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Direction = table.Column<int>(type: "integer", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    OpeningDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    OperationKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    CancelledAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CancelledBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CancellationReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ReversalOfOpeningBalanceId = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("PK_AppBusinessPartnerOpeningBalances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppBusinessPartnerOpeningBalances_AppBusinessPartnerOpening~",
                        column: x => x.ReversalOfOpeningBalanceId,
                        principalTable: "AppBusinessPartnerOpeningBalances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppBusinessPartnerOpeningBalances_AppBusinessPartners_Busin~",
                        column: x => x.BusinessPartnerId,
                        principalTable: "AppBusinessPartners",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppAccountTransactions_BusinessPartnerId",
                table: "AppAccountTransactions",
                column: "BusinessPartnerId");

            migrationBuilder.CreateIndex(
                name: "IX_AppAccountTransactions_RelatedAccountId",
                table: "AppAccountTransactions",
                column: "RelatedAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_AppAccountTransactions_ReversalOfTransactionId",
                table: "AppAccountTransactions",
                column: "ReversalOfTransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_AppAccountTransactions_TenantId_AccountId",
                table: "AppAccountTransactions",
                columns: new[] { "TenantId", "AccountId" },
                unique: true,
                filter: "\"Type\" = 1 AND \"Status\" = 1 AND \"OperationKey\" IS NOT NULL AND \"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "IX_AppAccountTransactions_TenantId_OperationKey_Type",
                table: "AppAccountTransactions",
                columns: new[] { "TenantId", "OperationKey", "Type" },
                unique: true,
                filter: "\"OperationKey\" IS NOT NULL AND \"DocumentId\" IS NULL AND \"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "IX_AppBusinessPartnerOpeningBalances_BusinessPartnerId",
                table: "AppBusinessPartnerOpeningBalances",
                column: "BusinessPartnerId");

            migrationBuilder.CreateIndex(
                name: "IX_AppBusinessPartnerOpeningBalances_ReversalOfOpeningBalanceId",
                table: "AppBusinessPartnerOpeningBalances",
                column: "ReversalOfOpeningBalanceId");

            migrationBuilder.CreateIndex(
                name: "IX_AppBusinessPartnerOpeningBalances_TenantId_BusinessPartnerId",
                table: "AppBusinessPartnerOpeningBalances",
                columns: new[] { "TenantId", "BusinessPartnerId" },
                unique: true,
                filter: "\"Status\" = 1 AND \"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "IX_AppBusinessPartnerOpeningBalances_TenantId_OperationKey",
                table: "AppBusinessPartnerOpeningBalances",
                columns: new[] { "TenantId", "OperationKey" },
                unique: true,
                filter: "\"IsDeleted\" = FALSE");

            migrationBuilder.AddForeignKey(
                name: "FK_AppAccountTransactions_AppAccountTransactions_ReversalOfTra~",
                table: "AppAccountTransactions",
                column: "ReversalOfTransactionId",
                principalTable: "AppAccountTransactions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AppAccountTransactions_AppAccounts_RelatedAccountId",
                table: "AppAccountTransactions",
                column: "RelatedAccountId",
                principalTable: "AppAccounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AppAccountTransactions_AppBusinessPartners_BusinessPartnerId",
                table: "AppAccountTransactions",
                column: "BusinessPartnerId",
                principalTable: "AppBusinessPartners",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AppAccountTransactions_AppAccountTransactions_ReversalOfTra~",
                table: "AppAccountTransactions");

            migrationBuilder.DropForeignKey(
                name: "FK_AppAccountTransactions_AppAccounts_RelatedAccountId",
                table: "AppAccountTransactions");

            migrationBuilder.DropForeignKey(
                name: "FK_AppAccountTransactions_AppBusinessPartners_BusinessPartnerId",
                table: "AppAccountTransactions");

            migrationBuilder.DropTable(
                name: "AppBusinessPartnerOpeningBalances");

            migrationBuilder.DropIndex(
                name: "IX_AppAccountTransactions_BusinessPartnerId",
                table: "AppAccountTransactions");

            migrationBuilder.DropIndex(
                name: "IX_AppAccountTransactions_RelatedAccountId",
                table: "AppAccountTransactions");

            migrationBuilder.DropIndex(
                name: "IX_AppAccountTransactions_ReversalOfTransactionId",
                table: "AppAccountTransactions");

            migrationBuilder.DropIndex(
                name: "IX_AppAccountTransactions_TenantId_AccountId",
                table: "AppAccountTransactions");

            migrationBuilder.DropIndex(
                name: "IX_AppAccountTransactions_TenantId_OperationKey_Type",
                table: "AppAccountTransactions");

            migrationBuilder.DropColumn(
                name: "BalanceAfter",
                table: "AppAccountTransactions");

            migrationBuilder.DropColumn(
                name: "BusinessPartnerId",
                table: "AppAccountTransactions");

            migrationBuilder.DropColumn(
                name: "CancellationReason",
                table: "AppAccountTransactions");

            migrationBuilder.DropColumn(
                name: "CancelledAt",
                table: "AppAccountTransactions");

            migrationBuilder.DropColumn(
                name: "CancelledBy",
                table: "AppAccountTransactions");

            migrationBuilder.DropColumn(
                name: "PaymentMethod",
                table: "AppAccountTransactions");

            migrationBuilder.DropColumn(
                name: "ReferenceNumber",
                table: "AppAccountTransactions");

            migrationBuilder.DropColumn(
                name: "RelatedAccountId",
                table: "AppAccountTransactions");

            migrationBuilder.DropColumn(
                name: "ReversalOfTransactionId",
                table: "AppAccountTransactions");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "AppAccountTransactions");
        }
    }
}
