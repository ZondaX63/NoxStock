using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NoxStock.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantCloneOperations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppTenantCloneOperations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceBackupId = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetTenantName = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    TargetTenantCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    OperationKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ProcessedRecordCount = table.Column<long>(type: "bigint", nullable: false),
                    TargetTenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    FailureCode = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    RequestedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ProtectedPackagePassword = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    ProtectedAdminPassword = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    AdminEmailAddress = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppTenantCloneOperations", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppTenantCloneOperations_OperationKey",
                table: "AppTenantCloneOperations",
                column: "OperationKey",
                unique: true,
                filter: "\"OperationKey\" IS NOT NULL AND \"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "IX_AppTenantCloneOperations_SourceBackupId_TargetTenantCode_Ta~",
                table: "AppTenantCloneOperations",
                columns: new[] { "SourceBackupId", "TargetTenantCode", "TargetTenantName" },
                unique: true,
                filter: "\"Status\" IN (1, 2, 3, 4, 5, 6) AND \"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "IX_AppTenantCloneOperations_Status_RequestedAtUtc",
                table: "AppTenantCloneOperations",
                columns: new[] { "Status", "RequestedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppTenantCloneOperations");
        }
    }
}
