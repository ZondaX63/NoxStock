using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NoxStock.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentPrintSnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BusinessPartnerAddressSnapshot",
                table: "AppDocuments",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BusinessPartnerNameSnapshot",
                table: "AppDocuments",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BusinessPartnerTaxNumberSnapshot",
                table: "AppDocuments",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BusinessPartnerTaxOfficeSnapshot",
                table: "AppDocuments",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CompanyAddressSnapshot",
                table: "AppDocuments",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CompanyEmailSnapshot",
                table: "AppDocuments",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CompanyNameSnapshot",
                table: "AppDocuments",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CompanyPhoneSnapshot",
                table: "AppDocuments",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CompanyTaxNumberSnapshot",
                table: "AppDocuments",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CompanyTaxOfficeSnapshot",
                table: "AppDocuments",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PrintSnapshotCapturedAt",
                table: "AppDocuments",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProductNameSnapshot",
                table: "AppDocumentItems",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProductSkuSnapshot",
                table: "AppDocumentItems",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProductUnitSnapshot",
                table: "AppDocumentItems",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BusinessPartnerAddressSnapshot",
                table: "AppDocuments");

            migrationBuilder.DropColumn(
                name: "BusinessPartnerNameSnapshot",
                table: "AppDocuments");

            migrationBuilder.DropColumn(
                name: "BusinessPartnerTaxNumberSnapshot",
                table: "AppDocuments");

            migrationBuilder.DropColumn(
                name: "BusinessPartnerTaxOfficeSnapshot",
                table: "AppDocuments");

            migrationBuilder.DropColumn(
                name: "CompanyAddressSnapshot",
                table: "AppDocuments");

            migrationBuilder.DropColumn(
                name: "CompanyEmailSnapshot",
                table: "AppDocuments");

            migrationBuilder.DropColumn(
                name: "CompanyNameSnapshot",
                table: "AppDocuments");

            migrationBuilder.DropColumn(
                name: "CompanyPhoneSnapshot",
                table: "AppDocuments");

            migrationBuilder.DropColumn(
                name: "CompanyTaxNumberSnapshot",
                table: "AppDocuments");

            migrationBuilder.DropColumn(
                name: "CompanyTaxOfficeSnapshot",
                table: "AppDocuments");

            migrationBuilder.DropColumn(
                name: "PrintSnapshotCapturedAt",
                table: "AppDocuments");

            migrationBuilder.DropColumn(
                name: "ProductNameSnapshot",
                table: "AppDocumentItems");

            migrationBuilder.DropColumn(
                name: "ProductSkuSnapshot",
                table: "AppDocumentItems");

            migrationBuilder.DropColumn(
                name: "ProductUnitSnapshot",
                table: "AppDocumentItems");
        }
    }
}
