using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace WhaleDeck.Agent.Services;

public sealed partial class ManagedActionExecutor(
    BoundedProcessRunner processes,
    DockerEngine docker)
{
    private const string PrivilegedHelper = "/usr/local/libexec/whaledeck-privileged";
    private const string DatabaseRepository = "/data/GitRepos/database-platform";
    private const string AppsRoot = "/data/WhaleDeck/apps";
    private static readonly TimeSpan ShortTimeout = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan LongTimeout = TimeSpan.FromHours(2);
    private static readonly JsonSerializerOptions IndentedJson = new() { WriteIndented = true };

    public async Task<string?> ExecuteAsync(
        string category,
        RegisteredResource resource,
        string action,
        IReadOnlyDictionary<string, string> parameters,
        CancellationToken cancellationToken)
    {
        if (category == "Database")
        {
            return await ExecuteDatabaseOrBackupAsync(resource, action, parameters, cancellationToken);
        }
        var task = category switch
        {
            "Systemd" => ExecuteHostAsync(resource, action, cancellationToken),
            "Compose" => ExecuteApplicationAsync(action, parameters, cancellationToken),
            "ConfigRepository" => ExecuteRepositoryAsync(resource, action, parameters, cancellationToken),
            "PlatformMaintenance" => ExecutePlatformAsync(resource, action, parameters, cancellationToken),
            _ => throw new InvalidOperationException("The managed action category is unsupported.")
        };
        await task;
        return null;
    }

    private async Task<string?> ExecuteDatabaseOrBackupAsync(
        RegisteredResource resource,
        string action,
        IReadOnlyDictionary<string, string> parameters,
        CancellationToken cancellationToken)
    {
        if (action is "run" or "verify" or "cancel" or "save-policy")
            return await ExecuteBackupAsync(resource, action, parameters, cancellationToken);

        var task = resource.Id switch
        {
            "database-platform.postgres" => ExecutePostgresAsync(action, parameters, cancellationToken),
            "database-platform.mariadb" => ExecuteMariaDbAsync(action, parameters, cancellationToken),
            "database-platform.mongodb" => ExecuteMongoDbAsync(action, parameters, cancellationToken),
            "database-platform.sqlserver" => ExecuteSqlServerAsync(action, parameters, cancellationToken),
            "database-platform.valkey" => ExecuteValkeyAsync("database-platform-valkey", action, parameters, cancellationToken),
            "database-platform.valkey72" => ExecuteValkeyAsync("database-platform-valkey72", action, parameters, cancellationToken),
            _ => throw new InvalidOperationException("The registered resource is not a database instance.")
        };
        await task;
        return null;
    }

    private async Task ExecutePostgresAsync(string action, IReadOnlyDictionary<string, string> parameters, CancellationToken cancellationToken)
    {
        var name = OptionalIdentifier(parameters, "name");
        var principal = OptionalIdentifier(parameters, "principal");
        var database = OptionalIdentifier(parameters, "database");
        var role = OptionalRole(parameters);
        var password = OptionalSecret(parameters);
        var sql = action switch
        {
            "create" => $"CREATE DATABASE {PgIdentifier(Required(name, "name"))};",
            "delete" => $"DROP DATABASE IF EXISTS {PgIdentifier(Required(name, "name"))} WITH (FORCE);",
            "create-principal" => $"CREATE ROLE {PgIdentifier(Required(principal, "principal"))} LOGIN PASSWORD {SqlLiteral(Required(password, "password"))};" +
                PostgresGrantSql(principal!, Required(database, "database"), role),
            "delete-principal" => $"DROP ROLE IF EXISTS {PgIdentifier(Required(principal, "principal"))};",
            "disable-principal" => $"ALTER ROLE {PgIdentifier(Required(principal, "principal"))} NOLOGIN;",
            "grant" => PostgresGrantSql(Required(principal, "principal"), Required(database, "database"), role),
            "rotate" => $"ALTER ROLE {PgIdentifier(Required(principal, "principal"))} PASSWORD {SqlLiteral(Required(password, "password"))};",
            "terminate-connection" => $"SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname = {SqlLiteral(Required(database, "database"))} AND pid <> pg_backend_pid();",
            _ => throw new InvalidOperationException("The PostgreSQL action is unsupported.")
        };
        await RunDockerStdinAsync(["exec", "-i", "-u", "postgres", "database-platform-postgres", "psql", "-v", "ON_ERROR_STOP=1", "--dbname", "postgres"], sql, "DATABASE_POSTGRES_FAILED", cancellationToken);
    }

    private async Task ExecuteMariaDbAsync(string action, IReadOnlyDictionary<string, string> parameters, CancellationToken cancellationToken)
    {
        var name = OptionalIdentifier(parameters, "name");
        var principal = OptionalIdentifier(parameters, "principal");
        var database = OptionalIdentifier(parameters, "database");
        var role = OptionalRole(parameters);
        var password = OptionalSecret(parameters);
        var account = principal is null ? null : $"{SqlLiteral(principal)}@'%'";
        var sql = action switch
        {
            "create" => $"CREATE DATABASE IF NOT EXISTS {MySqlIdentifier(Required(name, "name"))};",
            "delete" => $"DROP DATABASE IF EXISTS {MySqlIdentifier(Required(name, "name"))};",
            "create-principal" => $"CREATE USER IF NOT EXISTS {account} IDENTIFIED BY {SqlLiteral(Required(password, "password"))};" + MariaGrantSql(account!, Required(database, "database"), role),
            "delete-principal" => $"DROP USER IF EXISTS {account};",
            "disable-principal" => $"ALTER USER {account} ACCOUNT LOCK;",
            "grant" => MariaGrantSql(account!, Required(database, "database"), role),
            "rotate" => $"ALTER USER {account} IDENTIFIED BY {SqlLiteral(Required(password, "password"))};",
            "terminate-connection" => BuildMariaTerminateSql(Required(principal, "principal")),
            _ => throw new InvalidOperationException("The MariaDB action is unsupported.")
        };
        const string shell = "exec mariadb --protocol=socket -uroot -p\"$MARIADB_ROOT_PASSWORD\"";
        await RunDockerStdinAsync(["exec", "-i", "database-platform-mariadb", "sh", "-ec", shell], sql, "DATABASE_MARIADB_FAILED", cancellationToken);
    }

    private async Task ExecuteSqlServerAsync(string action, IReadOnlyDictionary<string, string> parameters, CancellationToken cancellationToken)
    {
        var name = OptionalIdentifier(parameters, "name");
        var principal = OptionalIdentifier(parameters, "principal");
        var database = OptionalIdentifier(parameters, "database");
        var role = SqlServerRole(OptionalRole(parameters));
        var password = OptionalSecret(parameters);
        var sql = action switch
        {
            "create" => $"IF DB_ID({SqlLiteral(Required(name, "name"))}) IS NULL CREATE DATABASE {SqlServerIdentifier(name!)};",
            "delete" => $"IF DB_ID({SqlLiteral(Required(name, "name"))}) IS NOT NULL BEGIN ALTER DATABASE {SqlServerIdentifier(name!)} SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE {SqlServerIdentifier(name!)}; END;",
            "create-principal" => $"IF SUSER_ID({SqlLiteral(Required(principal, "principal"))}) IS NULL CREATE LOGIN {SqlServerIdentifier(principal!)} WITH PASSWORD={SqlLiteral(Required(password, "password"))}, CHECK_POLICY=ON;\n" +
                $"USE {SqlServerIdentifier(Required(database, "database"))}; IF USER_ID({SqlLiteral(principal!)}) IS NULL CREATE USER {SqlServerIdentifier(principal!)} FOR LOGIN {SqlServerIdentifier(principal!)}; ALTER ROLE {SqlServerIdentifier(role)} ADD MEMBER {SqlServerIdentifier(principal!)};",
            "delete-principal" => $"USE {SqlServerIdentifier(Required(database, "database"))}; IF USER_ID({SqlLiteral(Required(principal, "principal"))}) IS NOT NULL DROP USER {SqlServerIdentifier(principal!)}; " +
                $"USE [master]; IF SUSER_ID({SqlLiteral(principal!)}) IS NOT NULL DROP LOGIN {SqlServerIdentifier(principal!)};",
            "disable-principal" => $"ALTER LOGIN {SqlServerIdentifier(Required(principal, "principal"))} DISABLE;",
            "grant" => $"USE {SqlServerIdentifier(Required(database, "database"))}; IF USER_ID({SqlLiteral(Required(principal, "principal"))}) IS NULL CREATE USER {SqlServerIdentifier(principal!)} FOR LOGIN {SqlServerIdentifier(principal!)}; ALTER ROLE {SqlServerIdentifier(role)} ADD MEMBER {SqlServerIdentifier(principal!)};",
            "rotate" => $"ALTER LOGIN {SqlServerIdentifier(Required(principal, "principal"))} WITH PASSWORD={SqlLiteral(Required(password, "password"))};",
            "terminate-connection" => $"DECLARE @sql nvarchar(max)=N''; SELECT @sql += N'KILL ' + CAST(session_id AS nvarchar(12)) + N';' FROM sys.dm_exec_sessions WHERE login_name={SqlLiteral(Required(principal, "principal"))} AND session_id<>@@SPID; EXEC sp_executesql @sql;",
            _ => throw new InvalidOperationException("The SQL Server action is unsupported.")
        };
        const string shell = "tool=$(command -v sqlcmd || true); [ -n \"$tool\" ] || tool=/opt/mssql-tools18/bin/sqlcmd; exec \"$tool\" -C -S localhost -U sa -P \"$MSSQL_SA_PASSWORD\" -b -i /dev/stdin";
        await RunDockerStdinAsync(["exec", "-i", "database-platform-sqlserver", "sh", "-ec", shell], sql, "DATABASE_SQLSERVER_FAILED", cancellationToken);
    }

    private async Task ExecuteMongoDbAsync(string action, IReadOnlyDictionary<string, string> parameters, CancellationToken cancellationToken)
    {
        var name = OptionalIdentifier(parameters, "name");
        var principal = OptionalIdentifier(parameters, "principal");
        var database = OptionalIdentifier(parameters, "database");
        var role = MongoRole(OptionalRole(parameters));
        var password = OptionalSecret(parameters);
        var js = action switch
        {
            "create" => $"db.getSiblingDB({Json(name ?? throw new InvalidOperationException("name is required."))}).createCollection('_whaledeck_meta');",
            "delete" => $"db.getSiblingDB({Json(name ?? throw new InvalidOperationException("name is required."))}).dropDatabase();",
            "create-principal" => $"db.getSiblingDB({Json(Required(database, "database"))}).createUser({{user:{Json(Required(principal, "principal"))},pwd:{Json(Required(password, "password"))},roles:[{{role:{Json(role)},db:{Json(database!)}}}]}});",
            "delete-principal" => $"db.getSiblingDB({Json(Required(database, "database"))}).dropUser({Json(Required(principal, "principal"))});",
            "disable-principal" => $"db.getSiblingDB({Json(Required(database, "database"))}).updateUser({Json(Required(principal, "principal"))},{{roles:[]}});",
            "grant" => $"db.getSiblingDB({Json(Required(database, "database"))}).updateUser({Json(Required(principal, "principal"))},{{roles:[{{role:{Json(role)},db:{Json(database!)}}}]}});",
            "rotate" => $"db.getSiblingDB({Json(Required(database, "database"))}).updateUser({Json(Required(principal, "principal"))},{{pwd:{Json(Required(password, "password"))}}});",
            "terminate-connection" => $"db.getSiblingDB('admin').aggregate([{{$currentOp:{{allUsers:true,idleConnections:true}}}},{{$match:{{'effectiveUsers.user':{Json(Required(principal, "principal"))}}}}}]).forEach(op => {{ if (op.opid) db.getSiblingDB('admin').killOp(op.opid); }});",
            _ => throw new InvalidOperationException("The MongoDB action is unsupported.")
        };
        const string shell = "exec mongosh --quiet --username \"$MONGO_INITDB_ROOT_USERNAME\" --password \"$MONGO_INITDB_ROOT_PASSWORD\" --authenticationDatabase admin";
        await RunDockerStdinAsync(["exec", "-i", "database-platform-mongodb", "sh", "-ec", shell], js, "DATABASE_MONGODB_FAILED", cancellationToken);
    }

    private async Task ExecuteValkeyAsync(string container, string action, IReadOnlyDictionary<string, string> parameters, CancellationToken cancellationToken)
    {
        var principal = Required(OptionalIdentifier(parameters, "principal"), "principal");
        var role = OptionalRole(parameters);
        var password = OptionalSecret(parameters);
        var prefix = parameters.TryGetValue("keyPrefix", out var configuredPrefix) ? configuredPrefix : $"app:{principal}:";
        if (!KeyPrefixPattern().IsMatch(prefix)) throw new InvalidOperationException("The Valkey key prefix is invalid.");

        string[] command = action switch
        {
            "create-principal" => ["ACL", "SETUSER", principal, "on", "resetpass", $">{Required(password, "password")}", "resetkeys", $"~{prefix}*", "resetchannels", "nocommands", .. ValkeyPermissions(role)],
            "delete-principal" => ["ACL", "DELUSER", principal],
            "disable-principal" => ["ACL", "SETUSER", principal, "off"],
            "grant" => ["ACL", "SETUSER", principal, "on", "resetkeys", $"~{prefix}*", "resetchannels", "nocommands", .. ValkeyPermissions(role)],
            "rotate" => ["ACL", "SETUSER", principal, "resetpass", $">{Required(password, "password")}"],
            "terminate-connection" => ["CLIENT", "KILL", "USER", principal],
            _ => throw new InvalidOperationException("The Valkey action is unsupported.")
        };
        var resp = ToResp(command);
        const string shell = "exec valkey-cli --no-auth-warning -a \"$VALKEY_PASSWORD\" --pipe";
        await RunDockerStdinAsync(["exec", "-i", container, "sh", "-ec", shell], resp, "DATABASE_VALKEY_FAILED", cancellationToken);
    }

    private async Task<string?> ExecuteBackupAsync(
        RegisteredResource resource,
        string action,
        IReadOnlyDictionary<string, string> parameters,
        CancellationToken cancellationToken)
    {
        if (action == "cancel") throw new InvalidOperationException("Cancel the active backup job instead of starting a second backup action.");
        if (action == "save-policy") return null;
        var engine = resource.Id switch
        {
            "database-platform.postgres" => "postgres",
            "database-platform.mariadb" => "mariaDb",
            "database-platform.mongodb" => "mongoDb",
            "database-platform.sqlserver" => "sqlServer",
            "database-platform.valkey" => "valkey",
            "database-platform.valkey72" => "valkey72",
            _ => throw new InvalidOperationException("The backup resource is unsupported.")
        };
        var criticalPercent = BoundedInteger(parameters, "capacityCriticalPercent", 51, 99, 95);
        var retentionDays = BoundedInteger(parameters, "retentionDays", 1, 3650, 14);
        var retentionCount = BoundedInteger(parameters, "retentionCount", 1, 365, 14);
        var verifyAfterBackup = OptionalBoolean(parameters, "verifyAfterBackup", true);
        var result = await processes.RunAsync(
            "/usr/bin/sudo",
            [PrivilegedHelper, "database-backup", engine, action,
                criticalPercent.ToString(System.Globalization.CultureInfo.InvariantCulture),
                retentionDays.ToString(System.Globalization.CultureInfo.InvariantCulture),
                retentionCount.ToString(System.Globalization.CultureInfo.InvariantCulture),
                verifyAfterBackup.ToString().ToLowerInvariant()],
            null,
            LongTimeout,
            "BACKUP_OPERATION_FAILED",
            cancellationToken,
            DatabaseRepository);
        var resultJson = result.StandardOutput.Trim();
        using var _ = JsonDocument.Parse(resultJson);
        return resultJson;
    }

    private async Task ExecuteHostAsync(RegisteredResource resource, string action, CancellationToken cancellationToken)
    {
        string[] helperArguments = action switch
        {
            "update-check" => ["system-update-check"],
            "update-install" => ["system-update-install"],
            "reboot" => ["reboot-schedule"],
            "systemd-start" => ["systemd", resource.ExternalId, "start"],
            "systemd-stop" => ["systemd", resource.ExternalId, "stop"],
            "systemd-restart" when resource.ExternalId == "whaledeck-agent.service" => ["systemd-schedule", resource.ExternalId, "restart"],
            "systemd-restart" => ["systemd", resource.ExternalId, "restart"],
            _ => throw new InvalidOperationException("The host action is unsupported.")
        };
        await RunHelperAsync(helperArguments, null, action is "update-install" ? LongTimeout : ShortTimeout, "HOST_OPERATION_FAILED", cancellationToken);
    }

    private async Task ExecuteApplicationAsync(string action, IReadOnlyDictionary<string, string> parameters, CancellationToken cancellationToken)
    {
        var slug = Required(OptionalSlug(parameters, "slug"), "slug");
        var directory = ResolveChild(AppsRoot, slug);
        if (action == "install")
        {
            if (Directory.Exists(directory)) throw new InvalidOperationException("The application is already installed.");
            var image = Required(parameters.TryGetValue("image", out var imageValue) ? imageValue : null, "image");
            ValidateImage(image);
            var autoUpdate = parameters.TryGetValue("autoupdate", out var autoUpdateValue) &&
                bool.TryParse(autoUpdateValue, out var parsedAutoUpdate) && parsedAutoUpdate;
            Directory.CreateDirectory(directory);
            try
            {
                var compose = BuildApplicationCompose(slug, image, parameters);
                AtomicWrite(Path.Combine(directory, "compose.yml"), compose);
                AtomicWrite(Path.Combine(directory, "manifest.json"), JsonSerializer.Serialize(new { slug, image, autoUpdate, installedAtUtc = DateTimeOffset.UtcNow }, IndentedJson));
                AtomicWrite(Path.Combine(directory, ".env"), parameters.TryGetValue("environment", out var environment) ? NormalizeEnvironment(environment) : string.Empty, UnixFileMode.UserRead | UnixFileMode.UserWrite);
                AtomicWrite(Path.Combine(directory, ".env.example"), string.Empty);
                AtomicWrite(Path.Combine(directory, "README.md"), $"# {slug}\n\nManaged by Whale Deck.\n");
                await ComposeAsync(directory, ["up", "-d", "--remove-orphans"], cancellationToken);
            }
            catch
            {
                try { await ComposeAsync(directory, ["down", "--remove-orphans"], CancellationToken.None); }
                catch { /* best-effort cleanup before removing the failed installation directory */ }
                if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
                throw;
            }
            return;
        }

        if (!Directory.Exists(directory)) throw new InvalidOperationException("The application installation was not found.");
        switch (action)
        {
            case "start": await ComposeAsync(directory, ["start"], cancellationToken); break;
            case "stop": await ComposeAsync(directory, ["stop"], cancellationToken); break;
            case "restart": await ComposeAsync(directory, ["restart"], cancellationToken); break;
            case "update":
                if (parameters.TryGetValue("automatic", out var automatic) && bool.TryParse(automatic, out var isAutomatic) && isAutomatic)
                {
                    using var manifest = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(directory, "manifest.json"), cancellationToken));
                    if (!manifest.RootElement.TryGetProperty("autoUpdate", out var enabled) || !enabled.GetBoolean())
                        throw new InvalidOperationException("Automatic update is not enabled for this application.");
                }
                await ComposeAsync(directory, ["pull"], cancellationToken);
                await ComposeAsync(directory, ["up", "-d", "--force-recreate"], cancellationToken);
                break;
            case "reinstall":
                await ComposeAsync(directory, ["pull"], cancellationToken);
                await ComposeAsync(directory, ["up", "-d", "--force-recreate"], cancellationToken);
                break;
            case "uninstall":
                var args = parameters.TryGetValue("deleteVolumes", out var delete) && bool.TryParse(delete, out var deleteVolumes) && deleteVolumes
                    ? new[] { "down", "--volumes", "--remove-orphans" }
                    : ["down", "--remove-orphans"];
                await ComposeAsync(directory, args, cancellationToken);
                Directory.Delete(directory, recursive: true);
                break;
            default: throw new InvalidOperationException("The application action is unsupported.");
        }
    }

    private async Task ExecuteRepositoryAsync(RegisteredResource resource, string action, IReadOnlyDictionary<string, string> parameters, CancellationToken cancellationToken)
    {
        var path = resource.Path ?? throw new InvalidOperationException("The repository path is not registered.");
        await EnsureRepositorySafeAsync(path, cancellationToken);
        if (action == "snapshot") return;
        if (action != "commit-push") throw new InvalidOperationException("The repository action is unsupported.");
        var message = parameters.TryGetValue("message", out var value) ? value.Trim() : "chore: save Whale Deck configuration";
        if (message.Length is < 3 or > 120 || message.Contains('\n') || message.Contains('\r')) throw new InvalidOperationException("The commit message is invalid.");
        await GitAsync(path, ["add", "--all"], cancellationToken);
        await EnsureRepositorySafeAsync(path, cancellationToken, staged: true);
        var status = await GitAsync(path, ["diff", "--cached", "--quiet"], cancellationToken, allowExitCodeOne: true);
        if (status.ExitCode == 0) return;
        await GitAsync(path, ["-c", "user.name=Whale Deck", "-c", "user.email=whaledeck@localhost", "commit", "-m", message], cancellationToken);
        await GitAsync(path, ["push", "origin", "HEAD"], cancellationToken);
    }

    private async Task ExecutePlatformAsync(RegisteredResource resource, string action, IReadOnlyDictionary<string, string> parameters, CancellationToken cancellationToken)
    {
        switch (action)
        {
            case "validate-settings":
            case "apply-settings":
                await ExecuteDockerSettingsAsync(action, parameters, cancellationToken);
                return;
            case "diagnose":
            case "diagnostic-bundle":
                await processes.RunAsync(Path.Combine(DatabaseRepository, "scripts", "check-all.sh"), [], null, LongTimeout, "PLATFORM_DIAGNOSTICS_FAILED", cancellationToken, DatabaseRepository);
                return;
            case "plan-update":
                await GitAsync("/data/GitRepos/whale-deck", ["status", "--short"], cancellationToken);
                await GitAsync(DatabaseRepository, ["status", "--short"], cancellationToken);
                return;
            case "apply-update":
                await RunHelperAsync(["platform-update-schedule"], null, ShortTimeout, "PLATFORM_UPDATE_FAILED", cancellationToken);
                return;
            case "rollback":
                await RunHelperAsync(["platform-rollback-schedule"], null, ShortTimeout, "PLATFORM_ROLLBACK_FAILED", cancellationToken);
                return;
            default: throw new InvalidOperationException("The platform maintenance action is unsupported.");
        }
    }

    private async Task ExecuteDockerSettingsAsync(string action, IReadOnlyDictionary<string, string> parameters, CancellationToken cancellationToken)
    {
        if (!parameters.TryGetValue("settingsJson", out var settingsJson) || string.IsNullOrWhiteSpace(settingsJson))
            throw new InvalidOperationException("settingsJson is required.");
        var current = JsonNode.Parse(await File.ReadAllTextAsync("/etc/docker/daemon.json", cancellationToken))?.AsObject() ?? new JsonObject();
        var requested = JsonNode.Parse(settingsJson)?.AsObject() ?? throw new InvalidOperationException("Docker settings must be a JSON object.");
        var allowed = new HashSet<string>(["registry-mirrors", "log-driver", "log-opts", "live-restore", "features", "dns", "proxies"], StringComparer.Ordinal);
        foreach (var setting in requested)
        {
            if (!allowed.Contains(setting.Key)) throw new InvalidOperationException($"Docker setting is not editable: {setting.Key}");
            current[setting.Key] = setting.Value?.DeepClone();
        }
        var candidate = current.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
        await RunHelperAsync(["docker-stage"], candidate, ShortTimeout, "DOCKER_SETTINGS_STAGE_FAILED", cancellationToken);
        await RunHelperAsync(["docker-validate", "/etc/docker/daemon.json.whaledeck-candidate"], null, ShortTimeout, "DOCKER_SETTINGS_INVALID", cancellationToken);
        if (action == "validate-settings") return;
        await RunHelperAsync(["docker-apply", "/etc/docker/daemon.json.whaledeck-candidate"], null, ShortTimeout, "DOCKER_SETTINGS_APPLY_FAILED", cancellationToken);
        for (var attempt = 0; attempt < 30; attempt++)
        {
            if (await docker.IsAvailableAsync(cancellationToken)) return;
            await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
        }
        await RunHelperAsync(["docker-rollback"], null, ShortTimeout, "DOCKER_SETTINGS_ROLLBACK_FAILED", cancellationToken);
        throw new InvalidOperationException("Docker did not become healthy after applying settings.");
    }

    private Task<ProcessResult> RunDockerStdinAsync(string[] arguments, string stdin, string errorCode, CancellationToken cancellationToken) =>
        processes.RunAsync("/usr/bin/docker", arguments, stdin, LongTimeout, errorCode, cancellationToken);

    private Task<ProcessResult> RunHelperAsync(string[] arguments, string? stdin, TimeSpan timeout, string errorCode, CancellationToken cancellationToken) =>
        processes.RunAsync("/usr/bin/sudo", ["-n", PrivilegedHelper, .. arguments], stdin, timeout, errorCode, cancellationToken);

    private Task<ProcessResult> ComposeAsync(string directory, string[] arguments, CancellationToken cancellationToken) =>
        processes.RunAsync("/usr/bin/docker", ["compose", "--project-directory", directory, "--file", Path.Combine(directory, "compose.yml"), "--env-file", Path.Combine(directory, ".env"), .. arguments], null, LongTimeout, "APPLICATION_COMPOSE_FAILED", cancellationToken, directory);

    private Task<ProcessResult> GitAsync(string directory, string[] arguments, CancellationToken cancellationToken, bool allowExitCodeOne = false) =>
        RunGitInternalAsync(directory, arguments, allowExitCodeOne, cancellationToken);

    private async Task<ProcessResult> RunGitInternalAsync(string directory, string[] arguments, bool allowExitCodeOne, CancellationToken cancellationToken)
    {
        try
        {
            return await processes.RunAsync("/usr/bin/git", arguments, null, ShortTimeout, "CONFIG_REPOSITORY_FAILED", cancellationToken, directory,
                new Dictionary<string, string> { ["GIT_TERMINAL_PROMPT"] = "0" });
        }
        catch (InvalidOperationException) when (allowExitCodeOne)
        {
            return new ProcessResult(1, string.Empty, string.Empty);
        }
    }

    private async Task EnsureRepositorySafeAsync(string path, CancellationToken cancellationToken, bool staged = false)
    {
        var files = await GitAsync(path, staged ? ["diff", "--cached", "--name-only", "-z"] : ["ls-files", "-co", "--exclude-standard", "-z"], cancellationToken);
        foreach (var relative in files.StandardOutput.Split('\0', StringSplitOptions.RemoveEmptyEntries))
        {
            var full = Path.GetFullPath(Path.Combine(path, relative));
            if (!full.StartsWith(Path.GetFullPath(path) + Path.DirectorySeparatorChar, StringComparison.Ordinal) || !File.Exists(full)) continue;
            if (SensitiveFileNamePattern().IsMatch(relative)) throw new InvalidOperationException("A sensitive file cannot be committed.");
            if (new FileInfo(full).Length <= 1024 * 1024)
            {
                var content = await File.ReadAllTextAsync(full, cancellationToken);
                if (SensitiveContentPattern().IsMatch(content)) throw new InvalidOperationException("Sensitive-looking content blocked the configuration snapshot.");
            }
        }
    }

    private static string BuildApplicationCompose(string slug, string image, IReadOnlyDictionary<string, string> parameters)
    {
        var autoUpdate = parameters.TryGetValue("autoupdate", out var autoUpdateValue) &&
            bool.TryParse(autoUpdateValue, out var parsedAutoUpdate) && parsedAutoUpdate;
        var builder = new StringBuilder($"name: {slug}\nservices:\n  app:\n    image: {Json(image)}\n    container_name: {Json("whaledeck-app-" + slug)}\n    restart: unless-stopped\n    init: true\n    labels:\n      io.whaledeck.resource-id: {Json("application." + slug)}\n      io.whaledeck.protected: \"false\"\n      io.whaledeck.autoupdate: {Json(autoUpdate ? "true" : "false")}\n      autoupdate: {Json(autoUpdate ? "true" : "false")}\n    security_opt:\n      - no-new-privileges:true\n    cap_drop:\n      - ALL\n    logging:\n      driver: local\n      options:\n        max-size: 10m\n        max-file: \"5\"\n        compress: \"true\"\n");
        if (parameters.TryGetValue("ports", out var ports) && !string.IsNullOrWhiteSpace(ports))
        {
            builder.Append("    ports:\n");
            foreach (var port in ports.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            {
                if (!PortPattern().IsMatch(port)) throw new InvalidOperationException("An application port mapping is invalid.");
                builder.Append("      - ").Append(Json(port)).Append('\n');
            }
        }
        builder.Append("    env_file:\n      - .env\n");
        return builder.ToString();
    }

    private static string NormalizeEnvironment(string source)
    {
        var output = new StringBuilder();
        foreach (var line in source.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            var separator = line.IndexOf('=');
            if (separator < 1 || !EnvironmentNamePattern().IsMatch(line[..separator])) throw new InvalidOperationException("An environment variable name is invalid.");
            if (line[(separator + 1)..].Contains('\n') || line[(separator + 1)..].Contains('\0')) throw new InvalidOperationException("An environment variable value is invalid.");
            output.Append(line).Append('\n');
        }
        return output.ToString();
    }

    private static void AtomicWrite(string path, string content, UnixFileMode mode = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead | UnixFileMode.OtherRead)
    {
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        File.WriteAllText(temporary, content);
        if (OperatingSystem.IsLinux()) File.SetUnixFileMode(temporary, mode);
        File.Move(temporary, path, true);
    }

    private static string ResolveChild(string root, string child)
    {
        var normalizedRoot = Path.GetFullPath(root) + Path.DirectorySeparatorChar;
        var path = Path.GetFullPath(Path.Combine(root, child));
        return path.StartsWith(normalizedRoot, StringComparison.Ordinal) ? path : throw new InvalidOperationException("The managed path is outside the approved root.");
    }

    private static void ValidateImage(string image)
    {
        if (!ImagePattern().IsMatch(image) || image.Contains("..", StringComparison.Ordinal)) throw new InvalidOperationException("The image reference is invalid.");
        var first = image.Split('/')[0];
        if (first.Contains('.') && first is not ("docker.io" or "ghcr.io" or "quay.io" or "xuanyuan.cloud" or "registry.cn-hangzhou.aliyuncs.com"))
            throw new InvalidOperationException("The image registry is not approved.");
    }

    private static string PostgresGrantSql(string principal, string database, string role) => role switch
    {
        "readonly" => $"GRANT CONNECT ON DATABASE {PgIdentifier(database)} TO {PgIdentifier(principal)};",
        "readwrite" => $"GRANT CONNECT, CREATE, TEMPORARY ON DATABASE {PgIdentifier(database)} TO {PgIdentifier(principal)};",
        "admin" => $"GRANT ALL PRIVILEGES ON DATABASE {PgIdentifier(database)} TO {PgIdentifier(principal)};",
        _ => throw new InvalidOperationException("The database role is unsupported.")
    };

    private static string MariaGrantSql(string account, string database, string role) => role switch
    {
        "readonly" => $"GRANT SELECT, SHOW VIEW ON {MySqlIdentifier(database)}.* TO {account};",
        "readwrite" => $"GRANT SELECT, INSERT, UPDATE, DELETE, CREATE, ALTER, INDEX ON {MySqlIdentifier(database)}.* TO {account};",
        "admin" => $"GRANT ALL PRIVILEGES ON {MySqlIdentifier(database)}.* TO {account};",
        _ => throw new InvalidOperationException("The database role is unsupported.")
    };

    private static string BuildMariaTerminateSql(string principal) =>
        $"SET @u={SqlLiteral(principal)}; SELECT GROUP_CONCAT(CONCAT('KILL ',ID) SEPARATOR ';') INTO @kills FROM information_schema.PROCESSLIST WHERE USER=@u AND ID<>CONNECTION_ID(); SET @kills=IFNULL(@kills,'SELECT 1'); PREPARE s FROM @kills; EXECUTE s; DEALLOCATE PREPARE s;";

    private static string SqlServerRole(string role) => role switch { "readonly" => "db_datareader", "readwrite" => "db_datawriter", "admin" => "db_owner", _ => throw new InvalidOperationException("The database role is unsupported.") };
    private static string MongoRole(string role) => role switch { "readonly" => "read", "readwrite" => "readWrite", "admin" => "dbOwner", _ => throw new InvalidOperationException("The database role is unsupported.") };
    private static string[] ValkeyPermissions(string role) => role switch
    {
        "readonly" => ["+@read", "+ping", "-@dangerous", "-@admin"],
        "readwrite" => ["+@read", "+@write", "+ping", "-@dangerous", "-@admin"],
        "admin" => ["+@read", "+@write", "+@connection", "+ping", "-@dangerous", "-@admin"],
        _ => throw new InvalidOperationException("The database role is unsupported.")
    };

    private static string ToResp(IEnumerable<string> values)
    {
        var items = values.ToArray();
        var builder = new StringBuilder().Append('*').Append(items.Length).Append("\r\n");
        foreach (var item in items) builder.Append('$').Append(Encoding.UTF8.GetByteCount(item)).Append("\r\n").Append(item).Append("\r\n");
        return builder.ToString();
    }

    private static string? OptionalIdentifier(IReadOnlyDictionary<string, string> parameters, string key) =>
        parameters.TryGetValue(key, out var value) && IdentifierPattern().IsMatch(value) ? value : parameters.ContainsKey(key) ? throw new InvalidOperationException($"{key} is invalid.") : null;
    private static string? OptionalSlug(IReadOnlyDictionary<string, string> parameters, string key) =>
        parameters.TryGetValue(key, out var value) && SlugPattern().IsMatch(value) ? value : parameters.ContainsKey(key) ? throw new InvalidOperationException($"{key} is invalid.") : null;
    private static string OptionalRole(IReadOnlyDictionary<string, string> parameters) => parameters.TryGetValue("role", out var value) && value is "readonly" or "readwrite" or "admin" ? value : "readonly";
    private static string? OptionalSecret(IReadOnlyDictionary<string, string> parameters) => parameters.TryGetValue("password", out var value) && value.Length is >= 16 and <= 256 && !value.Contains('\0') ? value : parameters.ContainsKey("password") ? throw new InvalidOperationException("password is invalid.") : null;
    private static string Required(string? value, string name) => !string.IsNullOrWhiteSpace(value) ? value : throw new InvalidOperationException($"{name} is required.");
    private static int BoundedInteger(IReadOnlyDictionary<string, string> parameters, string name, int minimum, int maximum, int fallback) =>
        !parameters.TryGetValue(name, out var value) ? fallback :
        int.TryParse(value, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var parsed) && parsed >= minimum && parsed <= maximum
            ? parsed : throw new InvalidOperationException($"{name} is outside the approved range.");
    private static bool OptionalBoolean(IReadOnlyDictionary<string, string> parameters, string name, bool fallback) =>
        !parameters.TryGetValue(name, out var value) ? fallback :
        bool.TryParse(value, out var parsed) ? parsed : throw new InvalidOperationException($"{name} must be true or false.");
    private static string PgIdentifier(string value) => $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
    private static string MySqlIdentifier(string value) => $"`{value.Replace("`", "``", StringComparison.Ordinal)}`";
    private static string SqlServerIdentifier(string value) => $"[{value.Replace("]", "]]", StringComparison.Ordinal)}]";
    private static string SqlLiteral(string value) => $"'{value.Replace("'", "''", StringComparison.Ordinal)}'";
    private static string Json(string value) => JsonSerializer.Serialize(value);

    [GeneratedRegex("^[A-Za-z][A-Za-z0-9_]{0,62}$", RegexOptions.CultureInvariant)] private static partial Regex IdentifierPattern();
    [GeneratedRegex("^[a-z0-9][a-z0-9-]{0,62}$", RegexOptions.CultureInvariant)] private static partial Regex SlugPattern();
    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9:_.-]{0,127}$", RegexOptions.CultureInvariant)] private static partial Regex KeyPrefixPattern();
    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9./_:@-]{0,254}$", RegexOptions.CultureInvariant)] private static partial Regex ImagePattern();
    [GeneratedRegex("^(0\\.0\\.0\\.0|127\\.0\\.0\\.1):[1-9][0-9]{0,4}:[1-9][0-9]{0,4}(/(tcp|udp))?$", RegexOptions.CultureInvariant)] private static partial Regex PortPattern();
    [GeneratedRegex("^[A-Z_][A-Z0-9_]{0,127}$", RegexOptions.CultureInvariant)] private static partial Regex EnvironmentNamePattern();
    [GeneratedRegex("(^|/)(\\.env($|\\.)|.*(password|secret|token|private[-_.]?key|credential).*)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)] private static partial Regex SensitiveFileNamePattern();
    [GeneratedRegex("(?im)(password|secret|token|client_secret|private_key)\\s*[:=]\\s*[^$<{\\s][^\\r\\n]{5,}", RegexOptions.CultureInvariant)] private static partial Regex SensitiveContentPattern();
}
