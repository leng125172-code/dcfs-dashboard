#!/usr/bin/env bash
set -Eeuo pipefail

repo_root=$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)
runtime_id="${WHALEDECK_RUNTIME_ID:-linux-x64}"

command -v dotnet >/dev/null 2>&1 || { echo 'dotnet SDK is required.' >&2; exit 1; }
[[ $EUID -eq 0 ]] || { echo 'Run with sudo so host services can be installed.' >&2; exit 1; }

# SDK resolution follows the working directory, not the supplied project path.
# Respect the repository's global.json before publishing either host program.
cd "$repo_root"
dotnet --version >/dev/null || { echo 'Install an SDK matching this repository global.json before publishing host services.' >&2; exit 1; }

install -d -m 0700 "$repo_root/deploy/artifacts"
artifacts_root=$(mktemp -d "$repo_root/deploy/artifacts/host-publish.XXXXXX")
# Each run gets its own directory; failed builds and previous releases are retained.
dotnet publish "$repo_root/src/WhaleDeck.Agent/WhaleDeck.Agent.csproj" \
  --configuration Release --runtime "$runtime_id" --self-contained true --output "$artifacts_root/agent"
dotnet publish "$repo_root/src/WhaleDeck.MaintenanceHost/WhaleDeck.MaintenanceHost.csproj" \
  --configuration Release --runtime "$runtime_id" --self-contained true --output "$artifacts_root/maintenance"

"$repo_root/deploy/host/install.sh" "$artifacts_root"
