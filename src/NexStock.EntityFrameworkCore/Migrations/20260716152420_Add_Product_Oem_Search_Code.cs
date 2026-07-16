using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NexStock.Migrations
{
    /// <inheritdoc />
    public partial class Add_Product_Oem_Search_Code : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SearchCode",
                table: "AppProductOEMCodes",
                type: "character varying(150)",
                maxLength: 150,
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql("""
                UPDATE "AppProductOEMCodes"
                SET "SearchCode" = UPPER(REGEXP_REPLACE("OemCode", '[^[:alnum:]]', '', 'g'));
                """);

            migrationBuilder.CreateIndex(
                name: "IX_AppProductOEMCodes_TenantId_SearchCode",
                table: "AppProductOEMCodes",
                columns: new[] { "TenantId", "SearchCode" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AppProductOEMCodes_TenantId_SearchCode",
                table: "AppProductOEMCodes");

            migrationBuilder.DropColumn(
                name: "SearchCode",
                table: "AppProductOEMCodes");
        }
    }
}
