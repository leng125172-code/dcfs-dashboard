#!/usr/bin/env python3
"""Exercise registered backup operations through API -> Worker -> Agent."""

from __future__ import annotations

import os
import secrets
import sys
import time

from validate_databases import Api
from validate_oidc import authenticate, required_environment


RESOURCES = {
    "postgres": "database-platform.postgres",
    "mariadb": "database-platform.mariadb",
    "sqlserver": "database-platform.sqlserver",
    "mongodb": "database-platform.mongodb",
    "valkey": "database-platform.valkey",
    "valkey72": "database-platform.valkey72",
}


def assert_backup_record(api: Api, resource_id: str, job: dict[str, object]) -> None:
    response = api.session.get(
        f"{api.base_url}/api/v1/backups/records",
        params={"instanceResourceId": resource_id, "take": 20},
        timeout=20,
    )
    response.raise_for_status()
    records = response.json()
    job_id = job.get("id")
    record = next((item for item in records if item.get("jobId") == job_id), None)
    if record is None:
        raise RuntimeError(f"Backup record for job {job_id} was not returned")
    if record.get("status") != "Succeeded":
        raise RuntimeError(f"Backup record for job {job_id} is not successful")
    if not record.get("relativePath"):
        raise RuntimeError(f"Backup record for job {job_id} has no relative path")
    if not isinstance(record.get("sizeBytes"), int) or record["sizeBytes"] <= 0:
        raise RuntimeError(f"Backup record for job {job_id} has no positive size")
    if record.get("checksumAlgorithm") != "SHA256":
        raise RuntimeError(f"Backup record for job {job_id} has an unexpected checksum algorithm")
    checksum = record.get("checksum")
    if not isinstance(checksum, str) or len(checksum) != 64:
        raise RuntimeError(f"Backup record for job {job_id} has an invalid SHA-256 checksum")
    if not record.get("verifiedAtUtc"):
        raise RuntimeError(f"Backup record for job {job_id} was not marked verified")


def selected_resources() -> list[tuple[str, str]]:
    requested = {
        item.strip().lower()
        for item in os.environ.get("VALIDATION_ENGINES", "").split(",")
        if item.strip()
    }
    unknown = requested.difference(RESOURCES)
    if unknown:
        raise RuntimeError(f"Unknown backup engines: {', '.join(sorted(unknown))}")
    return [(name, resource) for name, resource in RESOURCES.items() if not requested or name in requested]


def main() -> int:
    base_url = os.environ.get("WHALEDECK_URL", "http://192.168.100.13:8080").rstrip("/")
    session, _ = authenticate(
        base_url,
        required_environment("VALIDATION_USERNAME"),
        required_environment("VALIDATION_PASSWORD"),
    )
    api = Api(base_url, session)
    resources = selected_resources()
    for name, resource_id in resources:
        print(f"Validating {name} backup...")
        temporary_database = None
        try:
            if name == "sqlserver":
                candidate = f"wdv_backup_{int(time.time()) % 1_000_000}_{secrets.token_hex(2)}"
                api.run("create", resource_id, {"name": candidate}, requires_plan=True)
                temporary_database = candidate
            run_job = api.run("run", resource_id, {}, area="backups", timeout=1800)
            assert_backup_record(api, resource_id, run_job)
            api.run("verify", resource_id, {}, area="backups", timeout=300)
        finally:
            if temporary_database is not None:
                api.run(
                    "delete",
                    resource_id,
                    {"name": temporary_database},
                    confirmed=True,
                    requires_plan=True,
                )
        print(f"{name}: backup and verification operations passed")
    print(f"Backup validation passed: {', '.join(name for name, _ in resources)}")
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except Exception as error:
        print(f"Backup validation failed: {error}", file=sys.stderr)
        raise SystemExit(1)
