using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NoxStock.Migrations
{
    /// <inheritdoc />
    public partial class AllowReallocatedReversedPayments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AppPaymentAllocations_TenantId_AccountTransactionId_Documen~",
                table: "AppPaymentAllocations");

            migrationBuilder.CreateIndex(
                name: "IX_AppPaymentAllocations_TenantId_AccountTransactionId_Documen~",
                table: "AppPaymentAllocations",
                columns: new[] { "TenantId", "AccountTransactionId", "DocumentId" },
                unique: true,
                filter: "\"IsDeleted\" = FALSE AND \"Status\" = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AppPaymentAllocations_TenantId_AccountTransactionId_Documen~",
                table: "AppPaymentAllocations");

            migrationBuilder.CreateIndex(
                name: "IX_AppPaymentAllocations_TenantId_AccountTransactionId_Documen~",
                table: "AppPaymentAllocations",
                columns: new[] { "TenantId", "AccountTransactionId", "DocumentId" },
                unique: true,
                filter: "\"IsDeleted\" = FALSE");
        }
    }
}
