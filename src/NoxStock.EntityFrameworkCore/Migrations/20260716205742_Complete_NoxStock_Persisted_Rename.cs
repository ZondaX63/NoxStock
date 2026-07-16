using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NoxStock.Migrations
{
    /// <inheritdoc />
    public partial class Complete_NoxStock_Persisted_Rename : Migration
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
                    DELETE FROM "AbpPermissions"
                    WHERE "Name" LIKE target_name || '%';

                    DELETE FROM "AbpPermissionGroups"
                    WHERE "Name" = target_name;

                    UPDATE "AbpPermissionGroups"
                    SET "Name" = replace("Name", source_name, target_name),
                        "DisplayName" = replace("DisplayName", source_name, target_name),
                        "ExtraProperties" = replace("ExtraProperties", source_name, target_name)
                    WHERE "Name" = source_name;

                    UPDATE "AbpPermissions"
                    SET "GroupName" = replace("GroupName", source_name, target_name),
                        "Name" = replace("Name", source_name, target_name),
                        "ParentName" = replace("ParentName", source_name, target_name),
                        "DisplayName" = replace("DisplayName", source_name, target_name),
                        "ExtraProperties" = replace("ExtraProperties", source_name, target_name)
                    WHERE "GroupName" = source_name
                       OR "Name" LIKE source_name || '%';

                    DELETE FROM "AbpSettingDefinitions"
                    WHERE "Name" LIKE target_name || '%';

                    UPDATE "AbpSettingDefinitions"
                    SET "Name" = replace("Name", source_name, target_name),
                        "DisplayName" = replace("DisplayName", source_name, target_name)
                    WHERE "Name" LIKE source_name || '%';

                    UPDATE "AbpAuditLogActions"
                    SET "ServiceName" = replace("ServiceName", source_name, target_name)
                    WHERE "ServiceName" LIKE source_name || '%';
                END $rename$;
                """;
        }
    }
}
