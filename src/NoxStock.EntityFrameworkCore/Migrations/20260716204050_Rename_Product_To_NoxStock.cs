using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NoxStock.Migrations
{
    /// <inheritdoc />
    public partial class Rename_Product_To_NoxStock : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(BuildRenameSql("NoxStock"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(BuildRenameSql(OldProductNameSql));
        }

        private const string OldProductNameSql =
            "chr(78) || chr(101) || chr(120) || chr(83) || chr(116) || chr(111) || chr(99) || chr(107)";

        private static string BuildRenameSql(string targetName)
        {
            var targetExpression = targetName == "NoxStock"
                ? "'NoxStock'"
                : targetName;
            var sourceExpression = targetName == "NoxStock"
                ? OldProductNameSql
                : "'NoxStock'";

            return $$"""
                DO $rename$
                DECLARE
                    source_name text := {{sourceExpression}};
                    target_name text := {{targetExpression}};
                BEGIN
                    UPDATE "AbpSettings"
                    SET "Name" = replace("Name", source_name, target_name)
                    WHERE "Name" LIKE source_name || '%';

                    UPDATE "AbpPermissionGrants"
                    SET "Name" = replace("Name", source_name, target_name)
                    WHERE "Name" LIKE source_name || '%';

                    UPDATE "AbpFeatureValues"
                    SET "Name" = replace("Name", source_name, target_name)
                    WHERE "Name" LIKE source_name || '%';

                    UPDATE "OpenIddictApplications"
                    SET "ClientId" = replace("ClientId", source_name, target_name),
                        "DisplayName" = replace("DisplayName", source_name, target_name),
                        "Permissions" = replace("Permissions", source_name, target_name)
                    WHERE "ClientId" LIKE '%' || source_name || '%'
                       OR "DisplayName" LIKE '%' || source_name || '%'
                       OR "Permissions" LIKE '%' || source_name || '%';

                    UPDATE "OpenIddictScopes"
                    SET "Name" = replace("Name", source_name, target_name),
                        "DisplayName" = replace("DisplayName", source_name, target_name),
                        "Resources" = replace("Resources", source_name, target_name)
                    WHERE "Name" LIKE '%' || source_name || '%'
                       OR "DisplayName" LIKE '%' || source_name || '%'
                       OR "Resources" LIKE '%' || source_name || '%';

                    UPDATE "AbpEntityChanges"
                    SET "EntityTypeFullName" = replace("EntityTypeFullName", source_name, target_name)
                    WHERE "EntityTypeFullName" LIKE source_name || '%';

                    UPDATE "AbpAuditLogs"
                    SET "ApplicationName" = replace("ApplicationName", source_name, target_name)
                    WHERE "ApplicationName" LIKE '%' || source_name || '%';

                    UPDATE "AbpSecurityLogs"
                    SET "ApplicationName" = replace("ApplicationName", source_name, target_name)
                    WHERE "ApplicationName" LIKE '%' || source_name || '%';
                END $rename$;
                """;
        }
    }
}
