#!/usr/bin/env python3
"""Exercise Authentik directory management through Whale Deck jobs."""

from __future__ import annotations

import os
import sys

from validate_databases import Api
from validate_oidc import authenticate, required_environment


def main() -> int:
    base_url = os.environ.get("WHALEDECK_URL", "http://192.168.100.13:8080").rstrip("/")
    session, _ = authenticate(
        base_url,
        required_environment("VALIDATION_USERNAME"),
        required_environment("VALIDATION_PASSWORD"),
    )
    api = Api(base_url, session)
    username = required_environment("VALIDATION_MANAGED_USERNAME")
    password = required_environment("VALIDATION_MANAGED_PASSWORD")

    for endpoint in ("users", "groups", "sso"):
        response = session.get(f"{base_url}/api/v1/{endpoint}", timeout=30)
        response.raise_for_status()
        if not isinstance(response.json(), list):
            raise RuntimeError(f"Authentik {endpoint} endpoint returned an unexpected payload")

    ticket = api.stage_secret(password)
    api.run(
        "create-user",
        "",
        {
            "username": username,
            "name": username,
            "email": f"{username}@invalid.local",
            "inputTicket": ticket,
            "inputKind": "password",
        },
        area="identity",
        timeout=120,
    )
    users = session.get(f"{base_url}/api/v1/users", timeout=30)
    users.raise_for_status()
    created = next((item for item in users.json() if item.get("name") == username), None)
    if created is None or created.get("state") != "Active":
        raise RuntimeError("The user created through Whale Deck was not returned as active")

    user_id = created["id"]
    updated_name = f"{username}-updated"
    api.run(
        "update-user",
        user_id,
        {"name": updated_name, "email": f"{username}@invalid.local"},
        area="identity",
        timeout=120,
    )
    users = session.get(f"{base_url}/api/v1/users", timeout=30)
    users.raise_for_status()
    updated = next((item for item in users.json() if item.get("id") == user_id), None)
    if updated is None or updated.get("name") != updated_name:
        raise RuntimeError("The Authentik user update was not visible through Whale Deck")

    api.run("disable-user", user_id, {}, area="identity", timeout=120)
    users = session.get(f"{base_url}/api/v1/users", timeout=30)
    users.raise_for_status()
    disabled = next((item for item in users.json() if item.get("id") == user_id), None)
    if disabled is None or disabled.get("state") != "Disabled":
        raise RuntimeError("The Authentik user was not disabled through Whale Deck")

    sso_slug = required_environment("VALIDATION_SSO_SLUG")
    sso_name = f"Whale Deck validation {sso_slug[-8:]}"
    api.run(
        "create-sso",
        "",
        {
            "slug": sso_slug,
            "name": sso_name,
            "providerType": "oauth2",
            "redirectUri": "http://192.168.100.13:19999/oidc/callback",
            "openInNewTab": "false",
        },
        area="identity",
        timeout=120,
    )
    applications = session.get(f"{base_url}/api/v1/sso", timeout=30)
    applications.raise_for_status()
    created_sso = next((item for item in applications.json() if item.get("name") == sso_name), None)
    if created_sso is None:
        raise RuntimeError("The SSO application created through Whale Deck was not returned")
    updated_sso_name = f"{sso_name} updated"
    api.run(
        "update-sso",
        created_sso["id"],
        {"name": updated_sso_name, "openInNewTab": "true"},
        area="identity",
        timeout=120,
    )
    applications = session.get(f"{base_url}/api/v1/sso", timeout=30)
    applications.raise_for_status()
    if not any(item.get("id") == created_sso["id"] and item.get("name") == updated_sso_name for item in applications.json()):
        raise RuntimeError("The SSO application update was not visible through Whale Deck")

    print("Authentik user and approved OIDC application lifecycle validation passed")
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except Exception as error:
        print(f"Authentik identity validation failed: {error}", file=sys.stderr)
        raise SystemExit(1)
