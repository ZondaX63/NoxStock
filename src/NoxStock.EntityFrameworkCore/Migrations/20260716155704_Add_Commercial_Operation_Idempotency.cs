using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NoxStock.Migrations
{
    /// <inheritdoc />
    public partial class Add_Commercial_Operation_Idempotency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "OperationKey",
                table: "AppDocuments",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OperationKey",
                table: "AppAccountTransactions",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppDocuments_TenantId_Type_OperationKey",
                table: "AppDocuments",
                columns: new[] { "TenantId", "Type", "OperationKey" },
                unique: true,
                filter: "\"OperationKey\" IS NOT NULL AND \"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "IX_AppAccountTransactions_TenantId_DocumentId_OperationKey",
                table: "AppAccountTransactions",
                columns: new[] { "TenantId", "DocumentId", "OperationKey" },
                unique: true,
                filter: "\"OperationKey\" IS NOT NULL AND \"IsDeleted\" = FALSE");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AppDocuments_TenantId_Type_OperationKey",
                table: "AppDocuments");

            migrationBuilder.DropIndex(
                name: "IX_AppAccountTransactions_TenantId_DocumentId_OperationKey",
                table: "AppAccountTransactions");

            migrationBuilder.DropColumn(
                name: "OperationKey",
                table: "AppDocuments");

            migrationBuilder.DropColumn(
                name: "OperationKey",
                table: "AppAccountTransactions");
        }
    }
}
