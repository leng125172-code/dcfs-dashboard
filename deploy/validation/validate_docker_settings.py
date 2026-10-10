#!/usr/bin/env python3
"""Validate safe Docker settings reads, validation and controlled restart."""

from __future__ import annotations

import json
import os
import sys

from validate_databases import Api
from validate_oidc import authenticate, required_environment


EDITABLE_KEYS = {
    "registry-mirrors",
    "log-driver",
    "log-opts",
    "live-restore",
    "features",
    "dns",
    "proxies",
}


def read_settings(api: Api) -> tuple[dict[str, object], list[str]]:
    response = api.session.get(f"{api.base_url}/api/v1/docker/settings", timeout=30)
    response.raise_for_status()
    payload = response.json()
    settings = json.loads(payload["settingsJson"])
    keys = payload["editableKeys"]
    if not isinstance(settings, dict) or not isinstance(keys, list):
        raise RuntimeError("Docker settings response has an invalid shape")
    if not set(settings).issubset(EDITABLE_KEYS) or set(keys) != EDITABLE_KEYS:
        raise RuntimeError("Docker settings exposed fields outside the approved whitelist")
    return settings, keys


def main() -> int:
    base_url = os.environ.get("WHALEDECK_URL", "http://192.168.100.13:8080").rstrip("/")
    session, _ = authenticate(
        base_url,
        required_environment("VALIDATION_USERNAME"),
        required_environment("VALIDATION_PASSWORD"),
    )
    api = Api(base_url, session)
    settings, _ = read_settings(api)
    mirrors = settings.get("registry-mirrors")
    if not isinstance(mirrors, list) or not any("xuanyuan.run" in str(item) for item in mirrors):
        raise RuntimeError("The workstation Xuanyuan registry mirror was not returned")

    settings_json = json.dumps(settings, separators=(",", ":"), ensure_ascii=False)
    api.run("validate-settings", "", {"settingsJson": settings_json}, area="docker", timeout=180)
    api.run(
        "apply-settings",
        "",
        {"settingsJson": settings_json},
        area="docker",
        confirmed=True,
        requires_plan=True,
        timeout=360,
    )
    current, _ = read_settings(api)
    if current != settings:
        raise RuntimeError("Docker settings changed unexpectedly after applying the same editable values")

    containers = api.session.get(f"{api.base_url}/api/v1/containers", timeout=30)
    containers.raise_for_status()
    names = {item.get("name") for item in containers.json()}
    required = {"whaledeck-api", "whaledeck-worker", "whaledeck-gateway"}
    if not required.issubset(names):
        raise RuntimeError("Whale Deck core containers did not recover after the Docker restart")
    print("Docker settings validation and controlled restart passed")
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except Exception as error:
        print(f"Docker settings validation failed: {error}", file=sys.stderr)
        raise SystemExit(1)
