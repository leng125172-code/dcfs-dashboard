#!/usr/bin/env bash
set -Eeuo pipefail
umask 077

[[ $EUID -eq 0 ]] || { echo 'Run this configurator with sudo.' >&2; exit 1; }
for tool in docker dockerd jq systemctl; do
  command -v "$tool" >/dev/null || { echo "Missing required tool: $tool" >&2; exit 1; }
done

daemon=/etc/docker/daemon.json
candidate=/etc/docker/daemon.json.whaledeck-candidate
backup=/etc/docker/daemon.json.whaledeck-backup
helper=/usr/local/libexec/whaledeck-privileged
[[ -r $daemon ]] || { echo "Missing Docker configuration: $daemon" >&2; exit 1; }
[[ -x $helper ]] || { echo "Missing Whale Deck privileged helper: $helper" >&2; exit 1; }
[[ ! -e $candidate ]] || { echo "Refusing to overwrite existing candidate: $candidate" >&2; exit 1; }

temporary=$(mktemp /etc/docker/daemon.json.whaledeck-safety.XXXXXX)
cleanup() {
  rm -f -- "$temporary" "$candidate"
}
trap cleanup EXIT

jq '
  .["log-driver"] = "local"
  | .["log-opts"] = ((.["log-opts"] // {}) + {
      "max-size": "10m",
      "max-file": "5",
      "compress": "true"
    })
  | .["live-restore"] = true
' "$daemon" > "$temporary"
dockerd --validate --config-file "$temporary" >/dev/null

if diff -q <(jq -S . "$daemon") <(jq -S . "$temporary") >/dev/null; then
  echo 'Docker safety defaults are already configured; restart skipped.'
  exit 0
fi

install -o root -g root -m 0644 "$temporary" "$candidate"
"$helper" docker-validate "$candidate" >/dev/null
"$helper" docker-apply "$candidate"

for attempt in {1..60}; do
  docker info >/dev/null 2>&1 && break
  sleep 1
done
if ! docker info >/dev/null 2>&1; then
  echo 'Docker did not become ready; restoring the previous configuration.' >&2
  install -o root -g root -m 0644 "$backup" "$daemon"
  systemctl restart docker.service
  exit 1
fi

[[ $(docker info --format '{{.LoggingDriver}}') == local ]]
echo 'Docker safety defaults applied: local log rotation and live-restore are enabled.'
