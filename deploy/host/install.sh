#!/usr/bin/env bash
set -Eeuo pipefail

[[ $EUID -eq 0 ]] || { echo 'Run this installer with sudo.' >&2; exit 1; }
source_root=${1:-}
[[ -d "$source_root/agent" && -d "$source_root/maintenance" ]] || {
  echo 'Usage: sudo install.sh <publish-root-containing-agent-and-maintenance>' >&2
  exit 2
}

getent group whaledeck >/dev/null || groupadd --system whaledeck
id whaledeck-agent >/dev/null 2>&1 || useradd --system --gid whaledeck --groups docker,systemd-journal --home-dir /var/lib/whaledeck-agent --shell /usr/sbin/nologin whaledeck-agent
id whaledeck-maintenance >/dev/null 2>&1 || useradd --system --gid whaledeck --home-dir /nonexistent --shell /usr/sbin/nologin whaledeck-maintenance

install -d -o root -g whaledeck -m 0750 /etc/whaledeck
install -d -o whaledeck-agent -g whaledeck -m 0750 /data/WhaleDeck/apps
install -d -o root -g root -m 0755 /opt/whaledeck-agent /opt/whaledeck-maintenance /usr/local/libexec
cp -a "$source_root/agent/." /opt/whaledeck-agent/
cp -a "$source_root/maintenance/." /opt/whaledeck-maintenance/
chown -R root:root /opt/whaledeck-agent /opt/whaledeck-maintenance
chmod 0755 /opt/whaledeck-agent/WhaleDeck.Agent /opt/whaledeck-maintenance/WhaleDeck.MaintenanceHost

if [[ ! -f /etc/whaledeck/agent-capability.key ]]; then
  umask 0077
  openssl rand 32 > /etc/whaledeck/agent-capability.key
fi
chown root:whaledeck /etc/whaledeck/agent-capability.key
chmod 0640 /etc/whaledeck/agent-capability.key
install -o root -g whaledeck -m 0640 "$(dirname "$0")/resources.json" /etc/whaledeck/resources.json
install -o root -g root -m 0750 "$(dirname "$0")/whaledeck-privileged" /usr/local/libexec/whaledeck-privileged
printf '%s\n' 'whaledeck-agent ALL=(root) NOPASSWD: /usr/local/libexec/whaledeck-privileged *' > /etc/sudoers.d/whaledeck-agent
chmod 0440 /etc/sudoers.d/whaledeck-agent
visudo -cf /etc/sudoers.d/whaledeck-agent

install -o root -g root -m 0644 "$(dirname "$0")/whaledeck-agent.service" /etc/systemd/system/whaledeck-agent.service
install -o root -g root -m 0644 "$(dirname "$0")/whaledeck-maintenance.service" /etc/systemd/system/whaledeck-maintenance.service
systemctl daemon-reload
systemctl enable --now whaledeck-agent.service whaledeck-maintenance.service
systemctl --no-pager --full status whaledeck-agent.service whaledeck-maintenance.service
echo "Whale Deck group id: $(getent group whaledeck | cut -d: -f3)"
