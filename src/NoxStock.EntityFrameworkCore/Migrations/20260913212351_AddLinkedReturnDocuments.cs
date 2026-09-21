using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NoxStock.Migrations
{
    /// <inheritdoc />
    public partial class AddLinkedReturnDocuments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SourceDocumentItemId",
                table: "AppDocumentItems",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppDocumentItems_SourceDocumentItemId",
                table: "AppDocumentItems",
                column: "SourceDocumentItemId");

            migrationBuilder.CreateIndex(
                name: "IX_AppDocumentItems_TenantId_SourceDocumentItemId",
                table: "AppDocumentItems",
                columns: new[] { "TenantId", "SourceDocumentItemId" });

            migrationBuilder.AddForeignKey(
                name: "FK_AppDocumentItems_AppDocumentItems_SourceDocumentItemId",
                table: "AppDocumentItems",
                column: "SourceDocumentItemId",
                principalTable: "AppDocumentItems",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AppDocumentItems_AppDocumentItems_SourceDocumentItemId",
                table: "AppDocumentItems");

            migrationBuilder.DropIndex(
                name: "IX_AppDocumentItems_SourceDocumentItemId",
                table: "AppDocumentItems");

            migrationBuilder.DropIndex(
                name: "IX_AppDocumentItems_TenantId_SourceDocumentItemId",
                table: "AppDocumentItems");

            migrationBuilder.DropColumn(
                name: "SourceDocumentItemId",
                table: "AppDocumentItems");
        }
    }
}
