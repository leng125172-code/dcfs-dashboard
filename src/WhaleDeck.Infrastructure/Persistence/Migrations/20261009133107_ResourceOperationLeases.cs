using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
#pragma warning disable IDE0161 // Keep generated migration layout compatible with future EF scaffolding.

namespace WhaleDeck.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ResourceOperationLeases : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "resource_operation_leases",
                columns: table => new
                {
                    lock_key = table.Column<string>(type: "character varying(192)", maxLength: 192, nullable: false),
                    job_id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_id = table.Column<string>(type: "character varying(192)", maxLength: 192, nullable: false),
                    acquired_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    lease_expires_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_resource_operation_leases", x => x.lock_key);
                });

            migrationBuilder.CreateIndex(
                name: "IX_resource_operation_leases_job_id",
                table: "resource_operation_leases",
                column: "job_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_resource_operation_leases_lease_expires_at_utc",
                table: "resource_operation_leases",
                column: "lease_expires_at_utc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "resource_operation_leases");
        }
    }
}
