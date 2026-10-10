using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
#pragma warning disable CA1861 // EF Core scaffolding emits inline column-name arrays.

namespace WhaleDeck.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class ContainerUpdateHistory : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
                name: "container_update_runs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    external_container_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    container_name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    image = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    old_image_digest = table.Column<string>(type: "text", nullable: true),
                    new_image_digest = table.Column<string>(type: "text", nullable: true),
                    version_policy = table.Column<string>(type: "text", nullable: true),
                    job_id = table.Column<Guid>(type: "uuid", nullable: false),
                    started_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    completed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    result = table.Column<string>(type: "text", nullable: false),
                    was_rolled_back = table.Column<bool>(type: "boolean", nullable: false),
                    error_summary = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_container_update_runs", x => x.id);
                });

        migrationBuilder.CreateIndex(
            name: "IX_container_update_runs_container_name_started_at_utc",
            table: "container_update_runs",
            columns: new[] { "container_name", "started_at_utc" });

        migrationBuilder.CreateIndex(
            name: "IX_container_update_runs_job_id",
            table: "container_update_runs",
            column: "job_id",
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "container_update_runs");
    }
}
