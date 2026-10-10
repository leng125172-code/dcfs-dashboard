#!/usr/bin/env bash
set -Eeuo pipefail

[[ $EUID -eq 0 ]] || { echo 'Run with sudo.' >&2; exit 1; }
command -v nmcli >/dev/null 2>&1 || { echo 'NetworkManager CLI is required.' >&2; exit 1; }
command -v udevadm >/dev/null 2>&1 || { echo 'udevadm is required.' >&2; exit 1; }

interface=${1:-enp0s31f6}
connection=${2:-Onboard Ethernet DHCP}
[[ $interface =~ ^enp[0-9]+s[0-9]+(f[0-9]+)?$ ]] || { echo 'The target must be a predictable-name PCI Ethernet interface.' >&2; exit 2; }
[[ -e /sys/class/net/$interface ]] || { echo "Network interface not found: $interface" >&2; exit 2; }

bus=$(udevadm info -q property "/sys/class/net/$interface" | awk -F= '$1 == "ID_BUS" { print $2; exit }')
[[ $bus == pci ]] || { echo "Refusing to bind the onboard profile to a non-PCI device: $interface" >&2; exit 2; }
mac=$(<"/sys/class/net/$interface/address")
[[ $mac =~ ^([[:xdigit:]]{2}:){5}[[:xdigit:]]{2}$ ]] || { echo 'Unable to read a valid interface MAC address.' >&2; exit 2; }

nmcli connection show "$connection" >/dev/null 2>&1 || { echo "NetworkManager connection not found: $connection" >&2; exit 2; }
active_device=$(nmcli -t -f NAME,DEVICE connection show --active |
  awk -F: -v expected="$connection" '$1 == expected { print $2; exit }')
if [[ -n $active_device && $active_device != "$interface" ]]; then
  echo "Refusing to move an active connection from $active_device to $interface." >&2
  exit 2
fi

script_dir=$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)
install -D -m 0644 \
  "$script_dir/networkmanager/99-whaledeck-unmanaged-docker.conf" \
  /etc/NetworkManager/conf.d/99-whaledeck-unmanaged-docker.conf

# The existing netplan-generated connection used match: {}, which caused every
# Docker veth to inherit the workstation's static address profile. Bind it to
# the original onboard PCI NIC by both predictable name and permanent MAC.
nmcli connection modify "$connection" \
  connection.interface-name "$interface" \
  802-3-ethernet.mac-address "$mac"

netplan generate
nmcli general reload conf

# Apply the unmanaged policy immediately without restarting NetworkManager or
# disturbing the currently active USB Ethernet connection.
while IFS= read -r device; do
  [[ -n $device ]] || continue
  nmcli device set "$device" managed no || true
done < <(find /sys/class/net -mindepth 1 -maxdepth 1 -printf '%f\n' | grep -E '^(veth|br-|docker)')

configured_interface=$(nmcli --escape no -g connection.interface-name connection show "$connection")
configured_mac=$(nmcli --escape no -g 802-3-ethernet.mac-address connection show "$connection")
[[ $configured_interface == "$interface" ]] || { echo 'Connection interface binding was not applied.' >&2; exit 1; }
[[ ${configured_mac,,} == ${mac,,} ]] || { echo 'Connection MAC binding was not applied.' >&2; exit 1; }

echo "Bound '$connection' to $interface ($mac); Docker virtual interfaces are unmanaged by NetworkManager."
