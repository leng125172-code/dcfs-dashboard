using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
#pragma warning disable CA1861 // EF Core scaffolding emits inline column-name arrays.
#pragma warning disable IDE0161 // Keep generated migration layout compatible with future EF scaffolding.

namespace WhaleDeck.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PlatformFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "alert_event_history",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    alert_event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    state = table.Column<string>(type: "text", nullable: false),
                    actor_subject = table.Column<string>(type: "text", nullable: false),
                    occurred_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_alert_event_history", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "alert_events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    rule_id = table.Column<Guid>(type: "uuid", nullable: false),
                    resource_id = table.Column<Guid>(type: "uuid", nullable: true),
                    fingerprint = table.Column<string>(type: "text", nullable: false),
                    state = table.Column<string>(type: "text", nullable: false),
                    severity = table.Column<string>(type: "text", nullable: false),
                    occurrence_count = table.Column<int>(type: "integer", nullable: false),
                    first_occurred_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    last_occurred_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    recovered_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    acknowledged_by_subject = table.Column<string>(type: "text", nullable: true),
                    acknowledged_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    silenced_until_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    related_job_id = table.Column<Guid>(type: "uuid", nullable: true),
                    summary_code = table.Column<string>(type: "text", nullable: false),
                    detail_json = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_alert_events", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "alert_rules",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    rule_type = table.Column<string>(type: "text", nullable: false),
                    resource_selector_json = table.Column<string>(type: "jsonb", nullable: false),
                    threshold_json = table.Column<string>(type: "jsonb", nullable: false),
                    evaluation_window_seconds = table.Column<int>(type: "integer", nullable: false),
                    severity = table.Column<string>(type: "text", nullable: false),
                    is_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    version = table.Column<long>(type: "bigint", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_alert_rules", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "application_installations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    catalog_app_id = table.Column<string>(type: "text", nullable: false),
                    template_id = table.Column<string>(type: "text", nullable: false),
                    template_version = table.Column<string>(type: "text", nullable: false),
                    display_name = table.Column<string>(type: "text", nullable: false),
                    installed_version = table.Column<string>(type: "text", nullable: false),
                    desired_version = table.Column<string>(type: "text", nullable: true),
                    state = table.Column<string>(type: "text", nullable: false),
                    auto_update_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    config_summary_json = table.Column<string>(type: "jsonb", nullable: false),
                    installed_by_subject = table.Column<string>(type: "text", nullable: false),
                    installed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_application_installations", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "application_resources",
                columns: table => new
                {
                    application_id = table.Column<Guid>(type: "uuid", nullable: false),
                    resource_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_application_resources", x => new { x.application_id, x.resource_id, x.role });
                });

            migrationBuilder.CreateTable(
                name: "application_update_runs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    application_id = table.Column<Guid>(type: "uuid", nullable: false),
                    old_image_digest = table.Column<string>(type: "text", nullable: true),
                    new_image_digest = table.Column<string>(type: "text", nullable: false),
                    version_policy = table.Column<string>(type: "text", nullable: true),
                    plan_hash = table.Column<string>(type: "text", nullable: false),
                    job_id = table.Column<Guid>(type: "uuid", nullable: false),
                    started_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    completed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    result = table.Column<string>(type: "text", nullable: false),
                    was_rolled_back = table.Column<bool>(type: "boolean", nullable: false),
                    error_summary = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_application_update_runs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "audit_events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    actor_subject = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    action = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    target_type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    target_id = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    result = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    source_ip = table.Column<string>(type: "text", nullable: true),
                    job_id = table.Column<Guid>(type: "uuid", nullable: true),
                    trace_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    detail_json = table.Column<string>(type: "jsonb", nullable: true),
                    occurred_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_events", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "backup_policies",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    instance_resource_id = table.Column<Guid>(type: "uuid", nullable: false),
                    is_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    schedule_expression = table.Column<string>(type: "text", nullable: false),
                    timezone = table.Column<string>(type: "text", nullable: false),
                    retention_count = table.Column<int>(type: "integer", nullable: false),
                    retention_days = table.Column<int>(type: "integer", nullable: false),
                    target_directory_id = table.Column<string>(type: "text", nullable: false),
                    compression = table.Column<string>(type: "text", nullable: false),
                    verify_after_backup = table.Column<bool>(type: "boolean", nullable: false),
                    capacity_warning_percent = table.Column<int>(type: "integer", nullable: false),
                    capacity_critical_percent = table.Column<int>(type: "integer", nullable: false),
                    version = table.Column<long>(type: "bigint", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_backup_policies", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "backup_records",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    instance_resource_id = table.Column<Guid>(type: "uuid", nullable: false),
                    policy_id = table.Column<Guid>(type: "uuid", nullable: true),
                    job_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    relative_path = table.Column<string>(type: "text", nullable: true),
                    size_bytes = table.Column<long>(type: "bigint", nullable: true),
                    checksum_algorithm = table.Column<string>(type: "text", nullable: true),
                    checksum = table.Column<string>(type: "text", nullable: true),
                    started_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    completed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    verified_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    expires_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    error_code = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_backup_records", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "catalog_snapshots",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider = table.Column<string>(type: "text", nullable: false),
                    payload_json = table.Column<string>(type: "jsonb", nullable: false),
                    fetched_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    source_etag = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_catalog_snapshots", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "config_snapshots",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    repository_id = table.Column<string>(type: "text", nullable: false),
                    branch = table.Column<string>(type: "text", nullable: false),
                    base_commit_sha = table.Column<string>(type: "text", nullable: false),
                    result_commit_sha = table.Column<string>(type: "text", nullable: true),
                    file_manifest_json = table.Column<string>(type: "jsonb", nullable: false),
                    diff_summary_json = table.Column<string>(type: "jsonb", nullable: false),
                    sensitive_scan_result = table.Column<string>(type: "text", nullable: false),
                    job_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_by_subject = table.Column<string>(type: "text", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_config_snapshots", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "config_sync_runs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    repository_id = table.Column<string>(type: "text", nullable: false),
                    direction = table.Column<string>(type: "text", nullable: false),
                    before_commit_sha = table.Column<string>(type: "text", nullable: true),
                    after_commit_sha = table.Column<string>(type: "text", nullable: true),
                    state = table.Column<string>(type: "text", nullable: false),
                    job_id = table.Column<Guid>(type: "uuid", nullable: false),
                    error_code = table.Column<string>(type: "text", nullable: true),
                    started_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    completed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_config_sync_runs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "managed_resources",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    resource_type = table.Column<string>(type: "text", nullable: false),
                    external_id = table.Column<string>(type: "text", nullable: false),
                    display_name = table.Column<string>(type: "text", nullable: false),
                    protection_level = table.Column<string>(type: "text", nullable: false),
                    labels_json = table.Column<string>(type: "jsonb", nullable: false),
                    source = table.Column<string>(type: "text", nullable: false),
                    last_seen_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_managed_resources", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "metric_rollups",
                columns: table => new
                {
                    series_id = table.Column<Guid>(type: "uuid", nullable: false),
                    window_start_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    resolution = table.Column<string>(type: "text", nullable: false),
                    minimum = table.Column<double>(type: "double precision", nullable: false),
                    maximum = table.Column<double>(type: "double precision", nullable: false),
                    average = table.Column<double>(type: "double precision", nullable: false),
                    sum = table.Column<double>(type: "double precision", nullable: false),
                    sample_count = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_metric_rollups", x => new { x.series_id, x.window_start_utc, x.resolution });
                });

            migrationBuilder.CreateTable(
                name: "metric_samples",
                columns: table => new
                {
                    series_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sampled_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    value_double = table.Column<double>(type: "double precision", nullable: true),
                    quality = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_metric_samples", x => new { x.series_id, x.sampled_at_utc });
                });

            migrationBuilder.CreateTable(
                name: "metric_series",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    resource_id = table.Column<Guid>(type: "uuid", nullable: false),
                    metric_kind = table.Column<string>(type: "text", nullable: false),
                    unit = table.Column<string>(type: "text", nullable: false),
                    dimensions_json = table.Column<string>(type: "jsonb", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    last_seen_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_metric_series", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "operation_job_events",
                columns: table => new
                {
                    job_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sequence = table.Column<long>(type: "bigint", nullable: false),
                    state = table.Column<string>(type: "text", nullable: false),
                    phase = table.Column<string>(type: "text", nullable: false),
                    progress_percent = table.Column<short>(type: "smallint", nullable: true),
                    message_code = table.Column<string>(type: "text", nullable: false),
                    detail_json = table.Column<string>(type: "jsonb", nullable: false),
                    occurred_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_operation_job_events", x => new { x.job_id, x.sequence });
                });

            migrationBuilder.CreateTable(
                name: "operation_jobs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    job_type = table.Column<string>(type: "text", nullable: false),
                    actor_subject = table.Column<string>(type: "text", nullable: false),
                    resource_id = table.Column<Guid>(type: "uuid", nullable: true),
                    state = table.Column<string>(type: "text", nullable: false),
                    phase = table.Column<string>(type: "text", nullable: false),
                    progress_percent = table.Column<short>(type: "smallint", nullable: true),
                    idempotency_key = table.Column<string>(type: "text", nullable: false),
                    request_json = table.Column<string>(type: "jsonb", nullable: false),
                    result_json = table.Column<string>(type: "jsonb", nullable: true),
                    error_code = table.Column<string>(type: "text", nullable: true),
                    agent_operation_id = table.Column<Guid>(type: "uuid", nullable: true),
                    cancel_requested_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    started_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    completed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_operation_jobs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "outbox_messages",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    message_type = table.Column<string>(type: "text", nullable: false),
                    payload_json = table.Column<string>(type: "jsonb", nullable: false),
                    occurred_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    available_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    attempts = table.Column<int>(type: "integer", nullable: false),
                    processed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_error_code = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_outbox_messages", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "platform_settings",
                columns: table => new
                {
                    key = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    value_json = table.Column<string>(type: "jsonb", nullable: false),
                    version = table.Column<long>(type: "bigint", nullable: false),
                    updated_by_subject = table.Column<string>(type: "text", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_platform_settings", x => x.key);
                });

            migrationBuilder.CreateTable(
                name: "portal_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_subject = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    scope = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    description = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: true),
                    url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    icon_kind = table.Column<string>(type: "text", nullable: false),
                    icon_value = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    color = table.Column<string>(type: "text", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    is_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    version = table.Column<long>(type: "bigint", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_portal_items", x => x.id);
                    table.CheckConstraint("ck_portal_scope_owner", "(scope = 'Personal' AND owner_subject IS NOT NULL) OR (scope = 'Public' AND owner_subject IS NULL)");
                });

            migrationBuilder.CreateTable(
                name: "public_portal_preferences",
                columns: table => new
                {
                    subject = table.Column<string>(type: "text", nullable: false),
                    portal_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    is_hidden = table.Column<bool>(type: "boolean", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_public_portal_preferences", x => new { x.subject, x.portal_item_id });
                });

            migrationBuilder.CreateTable(
                name: "role_mappings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    authentik_group_id = table.Column<string>(type: "text", nullable: false),
                    authentik_group_name_snapshot = table.Column<string>(type: "text", nullable: false),
                    role = table.Column<string>(type: "text", nullable: false),
                    is_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    version = table.Column<long>(type: "bigint", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_role_mappings", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "scheduled_task_runs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    schedule_id = table.Column<Guid>(type: "uuid", nullable: false),
                    job_id = table.Column<Guid>(type: "uuid", nullable: false),
                    scheduled_for_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    started_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    completed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    result = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_scheduled_task_runs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "scheduled_tasks",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    task_type = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    schedule_kind = table.Column<string>(type: "text", nullable: false),
                    schedule_expression = table.Column<string>(type: "text", nullable: false),
                    timezone = table.Column<string>(type: "text", nullable: false),
                    parameters_json = table.Column<string>(type: "jsonb", nullable: false),
                    concurrency_policy = table.Column<string>(type: "text", nullable: false),
                    timeout_seconds = table.Column<int>(type: "integer", nullable: false),
                    is_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    next_run_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_by_subject = table.Column<string>(type: "text", nullable: false),
                    updated_by_subject = table.Column<string>(type: "text", nullable: false),
                    version = table.Column<long>(type: "bigint", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_scheduled_tasks", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_alert_event_history_alert_event_id_occurred_at_utc",
                table: "alert_event_history",
                columns: new[] { "alert_event_id", "occurred_at_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_alert_events_fingerprint_state",
                table: "alert_events",
                columns: new[] { "fingerprint", "state" });

            migrationBuilder.CreateIndex(
                name: "IX_application_update_runs_application_id_started_at_utc",
                table: "application_update_runs",
                columns: new[] { "application_id", "started_at_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_audit_events_actor_subject_occurred_at_utc",
                table: "audit_events",
                columns: new[] { "actor_subject", "occurred_at_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_audit_events_occurred_at_utc",
                table: "audit_events",
                column: "occurred_at_utc");

            migrationBuilder.CreateIndex(
                name: "IX_audit_events_target_type_target_id_occurred_at_utc",
                table: "audit_events",
                columns: new[] { "target_type", "target_id", "occurred_at_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_backup_records_instance_resource_id_started_at_utc",
                table: "backup_records",
                columns: new[] { "instance_resource_id", "started_at_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_catalog_snapshots_provider_fetched_at_utc",
                table: "catalog_snapshots",
                columns: new[] { "provider", "fetched_at_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_managed_resources_protection_level",
                table: "managed_resources",
                column: "protection_level");

            migrationBuilder.CreateIndex(
                name: "IX_managed_resources_resource_type_external_id",
                table: "managed_resources",
                columns: new[] { "resource_type", "external_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_metric_samples_series_id_sampled_at_utc",
                table: "metric_samples",
                columns: new[] { "series_id", "sampled_at_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_metric_series_resource_id_metric_kind_dimensions_json",
                table: "metric_series",
                columns: new[] { "resource_id", "metric_kind", "dimensions_json" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_operation_jobs_actor_subject_idempotency_key",
                table: "operation_jobs",
                columns: new[] { "actor_subject", "idempotency_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_operation_jobs_resource_id_created_at_utc",
                table: "operation_jobs",
                columns: new[] { "resource_id", "created_at_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_operation_jobs_state_created_at_utc",
                table: "operation_jobs",
                columns: new[] { "state", "created_at_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_outbox_messages_processed_at_utc_available_at_utc",
                table: "outbox_messages",
                columns: new[] { "processed_at_utc", "available_at_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_portal_items_owner_subject_is_enabled_sort_order",
                table: "portal_items",
                columns: new[] { "owner_subject", "is_enabled", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "IX_portal_items_scope_is_enabled_sort_order",
                table: "portal_items",
                columns: new[] { "scope", "is_enabled", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "IX_role_mappings_authentik_group_id",
                table: "role_mappings",
                column: "authentik_group_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_scheduled_task_runs_schedule_id_scheduled_for_utc",
                table: "scheduled_task_runs",
                columns: new[] { "schedule_id", "scheduled_for_utc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "alert_event_history");

            migrationBuilder.DropTable(
                name: "alert_events");

            migrationBuilder.DropTable(
                name: "alert_rules");

            migrationBuilder.DropTable(
                name: "application_installations");

            migrationBuilder.DropTable(
                name: "application_resources");

            migrationBuilder.DropTable(
                name: "application_update_runs");

            migrationBuilder.DropTable(
                name: "audit_events");

            migrationBuilder.DropTable(
                name: "backup_policies");

            migrationBuilder.DropTable(
                name: "backup_records");

            migrationBuilder.DropTable(
                name: "catalog_snapshots");

            migrationBuilder.DropTable(
                name: "config_snapshots");

            migrationBuilder.DropTable(
                name: "config_sync_runs");

            migrationBuilder.DropTable(
                name: "managed_resources");

            migrationBuilder.DropTable(
                name: "metric_rollups");

            migrationBuilder.DropTable(
                name: "metric_samples");

            migrationBuilder.DropTable(
                name: "metric_series");

            migrationBuilder.DropTable(
                name: "operation_job_events");

            migrationBuilder.DropTable(
                name: "operation_jobs");

            migrationBuilder.DropTable(
                name: "outbox_messages");

            migrationBuilder.DropTable(
                name: "platform_settings");

            migrationBuilder.DropTable(
                name: "portal_items");

            migrationBuilder.DropTable(
                name: "public_portal_preferences");

            migrationBuilder.DropTable(
                name: "role_mappings");

            migrationBuilder.DropTable(
                name: "scheduled_task_runs");

            migrationBuilder.DropTable(
                name: "scheduled_tasks");
        }
    }
}
