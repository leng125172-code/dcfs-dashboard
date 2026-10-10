using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
#pragma warning disable CA1861 // EF Core scaffolding emits inline column-name arrays.

namespace WhaleDeck.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class DockerEventTimeline : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
                name: "docker_events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    occurred_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    event_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    action = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    resource_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    resource_name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    image = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    attributes_json = table.Column<string>(type: "jsonb", nullable: false),
                    recorded_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_docker_events", x => x.id);
                });

        migrationBuilder.CreateIndex(
                name: "IX_docker_events_fingerprint",
                table: "docker_events",
                column: "fingerprint",
                unique: true);

        migrationBuilder.CreateIndex(
                name: "IX_docker_events_occurred_at_utc",
                table: "docker_events",
                column: "occurred_at_utc");

        migrationBuilder.CreateIndex(
                name: "IX_docker_events_resource_id_occurred_at_utc",
                table: "docker_events",
                columns: new[] { "resource_id", "occurred_at_utc" });

        migrationBuilder.CreateIndex(
                name: "IX_docker_events_resource_name_occurred_at_utc",
                table: "docker_events",
                columns: new[] { "resource_name", "occurred_at_utc" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
                name: "docker_events");
    }
}
