#!/usr/bin/env bash
set -Eeuo pipefail
root=$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)
bash_path=$(command -v bash)

# An empty PATH proves the dry-run neither contacts the network nor invokes
# package managers, Docker, sudo, systemd, Git or filesystem mutation utilities.
full=$(PATH=/nonexistent "$bash_path" "$root/install.sh" --dry-run)
[[ $full == *'7. Clone/update Whale Deck'* ]]
dependencies=$(PATH=/nonexistent "$bash_path" "$root/install.sh" --dry-run --dependencies-only)
[[ $dependencies == *'6. Prepare private settings'* && $dependencies != *'7.'* ]]
prepare=$(PATH=/nonexistent "$bash_path" "$root/install.sh" --dry-run --prepare-only)
[[ $prepare == *'4. Optionally merge'* && $prepare != *'5.'* ]]
if PATH=/nonexistent "$bash_path" "$root/install.sh" --dry-run --install-root / >/dev/null 2>&1; then
  echo 'FAIL: broad root directory accepted' >&2; exit 1
fi
if PATH=/nonexistent "$bash_path" "$root/install.sh" --dry-run --configure-mirror invalid >/dev/null 2>&1; then
  echo 'FAIL: invalid mirror option accepted' >&2; exit 1
fi
echo 'Installer CLI: 5 checks passed; no system operations executed.'
