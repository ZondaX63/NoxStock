using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NoxStock.Migrations
{
    /// <inheritdoc />
    public partial class Add_Document_Number_Uniqueness : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_AppDocuments_TenantId_Type_DocNumber",
                table: "AppDocuments",
                columns: new[] { "TenantId", "Type", "DocNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AppDocuments_TenantId_Type_DocNumber",
                table: "AppDocuments");
        }
    }
}
