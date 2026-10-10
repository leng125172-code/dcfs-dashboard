#!/usr/bin/env bash
set -Eeuo pipefail

repo_root=$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)
[[ $EUID -eq 0 ]] || { echo 'Run with sudo so host services can be installed.' >&2; exit 1; }
artifacts_root=$(
  "$repo_root/deploy/host/publish.sh" \
    | sed -n 's/^Host artifacts published to //p'
)
[[ -d $artifacts_root ]] || { echo 'Host artifact publication did not return a valid directory.' >&2; exit 3; }

"$repo_root/deploy/host/install.sh" "$artifacts_root"
