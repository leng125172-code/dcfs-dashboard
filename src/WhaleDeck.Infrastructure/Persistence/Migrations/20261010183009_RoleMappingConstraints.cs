using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WhaleDeck.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class RoleMappingConstraints : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<string>(
                name: "role",
                table: "role_mappings",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

        migrationBuilder.AlterColumn<string>(
                name: "authentik_group_name_snapshot",
                table: "role_mappings",
                type: "character varying(150)",
                maxLength: 150,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

        migrationBuilder.AlterColumn<string>(
                name: "authentik_group_id",
                table: "role_mappings",
                type: "character varying(128)",
                maxLength: 128,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<string>(
                name: "role",
                table: "role_mappings",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(32)",
                oldMaxLength: 32);

        migrationBuilder.AlterColumn<string>(
                name: "authentik_group_name_snapshot",
                table: "role_mappings",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(150)",
                oldMaxLength: 150);

        migrationBuilder.AlterColumn<string>(
                name: "authentik_group_id",
                table: "role_mappings",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(128)",
                oldMaxLength: 128);
    }
}
