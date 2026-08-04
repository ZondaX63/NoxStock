using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NoxStock.Migrations
{
    public partial class Add_Customer_Default_Sales_Discounts : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(name: "DefaultSalesDiscount1", table: "AppBusinessPartners", type: "decimal(9,4)", nullable: false, defaultValue: 0m);
            migrationBuilder.AddColumn<decimal>(name: "DefaultSalesDiscount2", table: "AppBusinessPartners", type: "decimal(9,4)", nullable: false, defaultValue: 0m);
            migrationBuilder.AddColumn<decimal>(name: "DefaultSalesDiscount3", table: "AppBusinessPartners", type: "decimal(9,4)", nullable: false, defaultValue: 0m);
            migrationBuilder.AddColumn<decimal>(name: "DefaultSalesDiscount4", table: "AppBusinessPartners", type: "decimal(9,4)", nullable: false, defaultValue: 0m);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "DefaultSalesDiscount1", table: "AppBusinessPartners");
            migrationBuilder.DropColumn(name: "DefaultSalesDiscount2", table: "AppBusinessPartners");
            migrationBuilder.DropColumn(name: "DefaultSalesDiscount3", table: "AppBusinessPartners");
            migrationBuilder.DropColumn(name: "DefaultSalesDiscount4", table: "AppBusinessPartners");
        }
    }
}
