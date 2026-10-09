#!/usr/bin/env bash
set -Eeuo pipefail

[[ $EUID -eq 0 ]] || { echo 'Run this validator with sudo.' >&2; exit 1; }
repo_root=$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)
database_root=/data/GitRepos/database-platform
expected=(
  database-platform-postgres
  database-platform-mariadb
  database-platform-mongodb
  database-platform-sqlserver
  database-platform-valkey
  database-platform-valkey72
  database-platform-authentik-server
  database-platform-authentik-worker
)

all_dependencies_healthy() {
  local name state
  for name in "${expected[@]}"; do
    state=$(docker inspect --format '{{.State.Running}} {{if .State.Health}}{{.State.Health.Status}}{{else}}missing-healthcheck{{end}}' "$name" 2>/dev/null || true)
    [[ $state == 'true healthy' ]] || return 1
  done
}

all_dependencies_healthy || { echo 'Refusing Docker restart: one or more dependencies are not healthy.' >&2; exit 1; }
agent_pid=$(systemctl show whaledeck-agent.service -p MainPID --value)
maintenance_pid=$(systemctl show whaledeck-maintenance.service -p MainPID --value)

"$repo_root/deploy/host/configure-docker-safety.sh"

for attempt in {1..180}; do
  all_dependencies_healthy && break
  sleep 1
done
all_dependencies_healthy || { echo 'Dependencies did not return to healthy state after the Docker restart.' >&2; exit 1; }

[[ $(systemctl show whaledeck-agent.service -p MainPID --value) == "$agent_pid" ]]
[[ $(systemctl show whaledeck-maintenance.service -p MainPID --value) == "$maintenance_pid" ]]
[[ $(docker info --format '{{.LoggingDriver}}') == local ]]
[[ $(docker info --format '{{.LiveRestoreEnabled}}') == true ]]
"$database_root/scripts/check-all.sh"
"$repo_root/deploy/host/validate.sh"

echo 'Docker restart acceptance passed: dependencies recovered and host services remained on their original processes.'
