using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NexStock.Migrations
{
    /// <inheritdoc />
    public partial class Add_Tenant_Activation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "AbpTenants",
                type: "boolean",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "AbpTenants");
        }
    }
}
