#!/usr/bin/env bash
set -Eeuo pipefail
umask 077

script_dir=$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)
runtime_environment=${1:-}
[[ -n $runtime_environment && $runtime_environment == /* && -f $runtime_environment ]] || {
  echo 'Usage: rotate-authentik-api-token.sh /absolute/whale-deck/.env' >&2
  exit 2
}

fragment=$(mktemp "${runtime_environment}.authentik-rotation.XXXXXX")
trap 'rm -f -- "$fragment"' EXIT
"$script_dir/provision-authentik.sh" --rotate-token "$fragment" >/dev/null

python3 - "$runtime_environment" "$fragment" <<'PY'
import os
import stat
import sys
import tempfile

runtime_path, fragment_path = sys.argv[1:]
source_stat = os.stat(runtime_path)
key = "WHALEDECK_AUTHENTIK_API_TOKEN="
replacement = next(
    (line for line in open(fragment_path, encoding="utf-8").read().splitlines() if line.startswith(key)),
    None,
)
if replacement is None:
    raise RuntimeError("Provisioning output did not contain the Authentik API token")

source = open(runtime_path, encoding="utf-8").read().splitlines()
found = False
updated = []
for line in source:
    if line.startswith(key):
        updated.append(replacement)
        found = True
    else:
        updated.append(line)
if not found:
    updated.append(replacement)

directory = os.path.dirname(runtime_path)
descriptor, temporary = tempfile.mkstemp(prefix=".env.authentik-", dir=directory, text=True)
try:
    with os.fdopen(descriptor, "w", encoding="utf-8", newline="\n") as output:
        output.write("\n".join(updated) + "\n")
        output.flush()
        os.fsync(output.fileno())
    os.chmod(temporary, stat.S_IRUSR | stat.S_IWUSR)
    os.chown(temporary, source_stat.st_uid, source_stat.st_gid)
    os.replace(temporary, runtime_path)
finally:
    if os.path.exists(temporary):
        os.unlink(temporary)
PY

echo 'Whale Deck Authentik management token was rotated and the private runtime environment was updated.'
