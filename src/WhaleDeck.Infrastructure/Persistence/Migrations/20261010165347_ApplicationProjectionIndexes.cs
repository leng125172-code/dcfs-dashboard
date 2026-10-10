using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WhaleDeck.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class ApplicationProjectionIndexes : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateIndex(
            name: "IX_application_update_runs_job_id",
            table: "application_update_runs",
            column: "job_id",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_application_installations_catalog_app_id",
            table: "application_installations",
            column: "catalog_app_id",
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_application_update_runs_job_id",
            table: "application_update_runs");

        migrationBuilder.DropIndex(
            name: "IX_application_installations_catalog_app_id",
            table: "application_installations");
    }
}
