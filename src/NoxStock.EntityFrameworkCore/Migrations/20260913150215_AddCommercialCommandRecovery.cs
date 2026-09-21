using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NoxStock.Migrations
{
    /// <inheritdoc />
    public partial class AddCommercialCommandRecovery : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AttemptCount",
                table: "AppCommercialCommands",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "CompletedAt",
                table: "AppCommercialCommands",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ExecutionToken",
                table: "AppCommercialCommands",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FailureCode",
                table: "AppCommercialCommands",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LeaseExpiresAt",
                table: "AppCommercialCommands",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ProcessingStartedAt",
                table: "AppCommercialCommands",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppCommercialCommands_TenantId_Status_LeaseExpiresAt",
                table: "AppCommercialCommands",
                columns: new[] { "TenantId", "Status", "LeaseExpiresAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AppCommercialCommands_TenantId_Status_LeaseExpiresAt",
                table: "AppCommercialCommands");

            migrationBuilder.DropColumn(
                name: "AttemptCount",
                table: "AppCommercialCommands");

            migrationBuilder.DropColumn(
                name: "CompletedAt",
                table: "AppCommercialCommands");

            migrationBuilder.DropColumn(
                name: "ExecutionToken",
                table: "AppCommercialCommands");

            migrationBuilder.DropColumn(
                name: "FailureCode",
                table: "AppCommercialCommands");

            migrationBuilder.DropColumn(
                name: "LeaseExpiresAt",
                table: "AppCommercialCommands");

            migrationBuilder.DropColumn(
                name: "ProcessingStartedAt",
                table: "AppCommercialCommands");
        }
    }
}
