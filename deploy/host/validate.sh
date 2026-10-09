#!/usr/bin/env bash
set -Eeuo pipefail
umask 077

[[ $EUID -eq 0 ]] || { echo 'Run this validator with sudo.' >&2; exit 1; }

assert_metadata() {
  local path=$1 expected=$2 actual
  actual=$(stat -c '%a:%U:%G' "$path")
  [[ $actual == "$expected" ]] || {
    printf 'Unexpected metadata for %s: expected %s, got %s\n' "$path" "$expected" "$actual" >&2
    exit 1
  }
}

for unit in whaledeck-agent.service whaledeck-maintenance.service; do
  systemctl is-enabled --quiet "$unit"
  systemctl is-active --quiet "$unit"
  [[ $(systemctl show "$unit" -p NRestarts --value) == 0 ]]
done

[[ -S /run/whaledeck/agent.sock ]]
assert_metadata /run/whaledeck '770:whaledeck-agent:whaledeck'
assert_metadata /run/whaledeck/agent.sock '660:whaledeck-agent:whaledeck'
assert_metadata /run/whaledeck/maintenance '770:whaledeck-agent:whaledeck'
assert_metadata /etc/whaledeck '750:root:whaledeck'
assert_metadata /etc/whaledeck/agent-capability.key '640:root:whaledeck'
assert_metadata /etc/whaledeck/resources.json '640:root:whaledeck'
assert_metadata /usr/local/libexec/whaledeck-privileged '750:root:root'
assert_metadata /etc/sudoers.d/whaledeck-agent '440:root:root'
visudo -cf /etc/sudoers.d/whaledeck-agent >/dev/null

[[ $(runuser -u whaledeck-agent -- sudo -n /usr/local/libexec/whaledeck-privileged self-check) == ok ]]
runuser -u whaledeck-agent -- docker version --format '{{.Server.Version}}' >/dev/null
runuser -u whaledeck-agent -- test -r /etc/whaledeck/agent-capability.key
runuser -u whaledeck-agent -- test ! -w /etc/whaledeck/agent-capability.key

temporary=$(mktemp -d /tmp/whaledeck-validate.XXXXXX)
cleanup() {
  rm -f -- "$temporary/grpc-headers" "$temporary/grpc-body" "$temporary/status.json"
  rmdir -- "$temporary"
}
trap cleanup EXIT
printf '\0\0\0\0\0' | curl --http2-prior-knowledge --unix-socket /run/whaledeck/agent.sock \
  --silent --show-error --dump-header "$temporary/grpc-headers" --output "$temporary/grpc-body" \
  --header 'Content-Type: application/grpc' --header 'TE: trailers' --data-binary @- \
  http://localhost/whaledeck.agent.v1.AgentService/GetHealth
grep -Eqi '^grpc-status: *0' "$temporary/grpc-headers"
(( $(stat -c '%s' "$temporary/grpc-body") >= 5 ))

for address in 192.168.22.19 192.168.100.13; do
  for port in 8080 8081; do
    code=$(curl --noproxy '*' --max-time 5 --silent --show-error \
      --output "$temporary/status.json" --write-out '%{http_code}' \
      "http://$address:$port/maintenance/status")
    [[ $code == 200 || $code == 503 ]]
    jq -e '.state | type == "string"' "$temporary/status.json" >/dev/null
  done
done

echo 'Whale Deck host services passed: systemd, permissions, UDS gRPC, restricted helper, Docker access and both IP entry points.'
