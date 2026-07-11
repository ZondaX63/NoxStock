using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NexStock.Migrations
{
    /// <inheritdoc />
    public partial class Add_Document_Source_Link : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SourceDocumentId",
                table: "AppDocuments",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppDocuments_SourceDocumentId",
                table: "AppDocuments",
                column: "SourceDocumentId");

            migrationBuilder.AddForeignKey(
                name: "FK_AppDocuments_AppDocuments_SourceDocumentId",
                table: "AppDocuments",
                column: "SourceDocumentId",
                principalTable: "AppDocuments",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AppDocuments_AppDocuments_SourceDocumentId",
                table: "AppDocuments");

            migrationBuilder.DropIndex(
                name: "IX_AppDocuments_SourceDocumentId",
                table: "AppDocuments");

            migrationBuilder.DropColumn(
                name: "SourceDocumentId",
                table: "AppDocuments");
        }
    }
}
