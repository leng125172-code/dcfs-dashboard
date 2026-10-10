#!/usr/bin/env python3
"""Validate read-only host APIs and the bounded diagnostic bundle flow."""

from __future__ import annotations

import io
import json
import os
import tarfile
import time

from validate_databases import Api
from validate_governance import request
from validate_oidc import authenticate, required_environment


def main() -> int:
    base_url = os.environ.get("WHALEDECK_URL", "http://192.168.100.13:8080").rstrip("/")
    session, _ = authenticate(
        base_url,
        required_environment("VALIDATION_USERNAME"),
        required_environment("VALIDATION_PASSWORD"),
    )
    api = Api(base_url, session)

    interfaces = request(api, "GET", "host/network-interfaces").json()
    if not interfaces or not any(item.get("attributes", {}).get("addresses") for item in interfaces):
        raise RuntimeError("The host network inventory did not contain an addressed interface")

    storage = request(api, "GET", "host/storage-devices").json()
    if not storage or not any(item.get("type") == "BlockDevice" for item in storage):
        raise RuntimeError("The host storage inventory did not contain a block device")

    journal = request(api, "GET", "host/journal?take=20&sinceMinutes=1440").json()
    if not isinstance(journal, list):
        raise RuntimeError("The bounded journal query did not return a list")

    submitted = request(
        api,
        "POST",
        "platform/diagnostic-bundle",
        payload={
            "resourceId": None,
            "parameters": {},
            "planHash": None,
            "confirmed": False,
            "idempotencyKey": "host-validation-" + os.urandom(8).hex(),
        },
        expected=202,
    ).json()
    job_id = submitted["id"]
    deadline = time.monotonic() + 90
    job: dict[str, object] = submitted
    while time.monotonic() < deadline:
        job = request(api, "GET", f"jobs/{job_id}").json()
        if job.get("state") in {"Succeeded", "Failed", "Canceled"}:
            break
        time.sleep(2)
    if job.get("state") != "Succeeded":
        raise RuntimeError(f"Diagnostic bundle job ended in {job.get('state')} / {job.get('errorCode')}")
    result = json.loads(str(job.get("resultJson") or "{}"))
    bundle_id = result.get("bundleId")
    if not isinstance(bundle_id, str) or len(bundle_id) != 32:
        raise RuntimeError("The diagnostic bundle result did not contain a valid bundle id")

    download = request(api, "GET", f"platform/diagnostics/{bundle_id}")
    if len(download.content) > 10 * 1024 * 1024:
        raise RuntimeError("The diagnostic bundle exceeded the size limit")
    with tarfile.open(fileobj=io.BytesIO(download.content), mode="r:gz") as archive:
        names = {name.removeprefix("./") for name in archive.getnames()}
        required = {"metadata.json", "kernel.txt", "storage.txt", "services.txt", "containers.jsonl", "docker-version.json", "recent-errors.txt"}
        if not required.issubset(names):
            raise RuntimeError(f"Diagnostic bundle is missing files: {sorted(required - names)}")

    print(
        f"Host validation passed: {len(interfaces)} interfaces, {len(storage)} storage records, "
        f"{len(journal)} journal entries and a {len(download.content)} byte diagnostic bundle"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
