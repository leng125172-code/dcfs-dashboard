# WhaleDeck

WhaleDeck is the containerized control plane for the Precision 7920 workstation. It provides a single IP-based entry point for platform status, Authentik-backed login and user identity, GitLab, user synchronization, and later restricted container operations.

## Technology

- ASP.NET Core 10 Web API and .NET Worker Service
- EF Core 10 with Npgsql for PostgreSQL 18
- Valkey 9 through the official `Valkey.Glide` client
- Vue 3, TypeScript, Vite, Pinia, Vue Router, and Element Plus
- Nginx gateway serving the Vue build and proxying API and Authentik traffic

The repository is a modular monolith. `Domain` has no infrastructure dependency, `Application` contains use cases and ports, `Infrastructure` implements persistence and external integrations, and `Api` owns HTTP/OIDC concerns.

WhaleDeck has no local password store and does not duplicate Authentik user profiles. Interactive login uses Authentik through the backend OIDC authorization-code flow. Authentik subject IDs and group claims are mapped to platform-specific permissions and immutable audit events; Authentik remains the source of truth for names, email addresses, groups, credentials, MFA, and account state.

## Repository layout

```text
src/
  WhaleDeck.Api/
  WhaleDeck.Application/
  WhaleDeck.Domain/
  WhaleDeck.Infrastructure/
  WhaleDeck.Worker/
web/
tests/
deploy/gateway/
```

## Branch model

- `master`: stable, deployable releases only
- `dev`: integration branch for active development
- Feature branches start from `dev` and merge back into `dev`
- Release changes move from `dev` to `master` through a pull request

Direct feature work must not be committed to `master`.

## Local verification

```powershell
dotnet restore
dotnet build --configuration Release --no-restore
dotnet test --configuration Release --no-build

Set-Location web
corepack pnpm install --frozen-lockfile
corepack pnpm lint
corepack pnpm test:unit
corepack pnpm build
```

If the workstation exports a SOCKS URL through `HTTP_PROXY` or `HTTPS_PROXY`, Node/Corepack may reject it. Clear those variables only for the current install process or provide the local proxy using an `http://127.0.0.1:<port>` URL. Do not change the system proxy as part of the project scripts.

## Runtime configuration

Copy `.env.example` to `.env`, set mode `0600` on Linux, and replace every placeholder. Secrets are never committed. Infrastructure settings come from environment variables, dynamic platform settings live in PostgreSQL, and public Vue settings are written to `/tmp/runtime-config.js` when the gateway starts.

The intended canonical URLs are:

- WhaleDeck: `http://192.168.22.19:8080`
- Authentik: `http://192.168.22.19:8081`
- GitLab: `http://192.168.22.19:8082` (added when GitLab is deployed)

Only the gateway publishes host ports. The API and Worker are internal containers. The API must never mount `/var/run/docker.sock`; future container actions go through a separate allow-listed agent.

## Container boundaries

- `whaledeck-gateway`: `whaledeck-app-ui` and `whaledeck-app-authentik`
- `whaledeck-api`: `whaledeck-app-ui`, `whaledeck-app-authentik`, `whaledeck-db-postgres`, and `whaledeck-cache-general`
- `whaledeck-worker`: `whaledeck-db-postgres` and `whaledeck-cache-general`

Docker uses the `local` logging driver with a maximum of five compressed 10 MiB files per container. Application request logs below server errors are emitted at debug level to avoid repeating the previous log-volume incident.

The Dockerfiles currently pin exact base-image tags. Before the first workstation deployment, resolve and append immutable manifest digests through the configured Xuanyuan/MCR mirrors and commit those digests with the deployment change.
