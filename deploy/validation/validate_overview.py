#!/usr/bin/env python3
"""Validate the administrator overview against the live workstation."""

from __future__ import annotations

import os
import re
import sys

from validate_oidc import authenticate, required_environment


VIRTUAL_INTERFACE = re.compile(r"^(lo|veth|docker|br-|virbr|cni|flannel|tun|tap)")
SAFE_SLUG = re.compile(r"^[a-z0-9][a-z0-9-]*$")


def main() -> int:
    base_url = os.environ.get("WHALEDECK_URL", "http://192.168.100.13:8080").rstrip("/")
    session, _ = authenticate(
        base_url,
        required_environment("VALIDATION_USERNAME"),
        required_environment("VALIDATION_PASSWORD"),
    )
    response = session.get(f"{base_url}/api/v1/overview", timeout=60)
    response.raise_for_status()
    overview = response.json()
    if overview.get("scope") != "Administrator":
        raise RuntimeError("The validation account did not receive the administrator overview")
    agent = overview.get("agent") or {}
    if not agent.get("available") or not agent.get("dockerAvailable"):
        raise RuntimeError("The overview reports an unavailable Agent or Docker Engine")
    host = overview.get("host") or {}
    if host.get("hostName") != "Precision-7920-Tower":
        raise RuntimeError("The overview returned an unexpected host")
    addresses = host.get("addresses") or []
    if not addresses:
        raise RuntimeError("The overview did not return an active workstation address")
    if any(VIRTUAL_INTERFACE.match(str(item.get("interfaceName", ""))) for item in addresses):
        raise RuntimeError("A Docker virtual interface leaked into workstation addresses")
    metrics = overview.get("metrics") or []
    required_metrics = {"cpu.utilization", "memory.utilization", "disk.utilization"}
    if not required_metrics.issubset({item.get("kind") for item in metrics}):
        raise RuntimeError("The overview is missing required live resource metrics")
    resources = overview.get("resources") or []
    if any("dcfs" in str(item.get("name", "")).lower() for item in resources):
        raise RuntimeError("A legacy dcfs resource leaked into the overview")
    applications = overview.get("applications") or []
    if len(applications) > 6:
        raise RuntimeError("The overview returned more than the requested Top 6 applications")
    if any(not SAFE_SLUG.fullmatch(str(item.get("id", ""))) for item in applications):
        raise RuntimeError("The application catalog returned an unsafe installation slug")
    print(
        "Live overview validation passed: "
        f"{len(addresses)} addresses, {len(metrics)} metrics, "
        f"{len(resources)} resources, {len(applications)} applications"
    )
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except Exception as error:
        print(f"Live overview validation failed: {error}", file=sys.stderr)
        raise SystemExit(1)
