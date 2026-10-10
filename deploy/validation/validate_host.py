#!/usr/bin/env python3
"""Validate read-only host APIs and the bounded diagnostic bundle flow."""

from __future__ import annotations

import io
import json
import os
import re
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

    containers = request(api, "GET", "containers?includeStopped=true").json()
    running = next((item for item in containers if item.get("state") == "running"), None)
    if not running:
        raise RuntimeError("The Docker inventory did not contain a running container")
    container_id = running.get("id")
    if not isinstance(container_id, str) or not container_id:
        raise RuntimeError("The running container did not contain an id")
    stats = request(api, "GET", f"containers/{container_id}/stats").json()
    values = stats.get("values") if isinstance(stats, dict) else None
    if not isinstance(values, dict) or not {"cpu", "memory", "network", "block", "pids"}.issubset(values):
        raise RuntimeError("The bounded container statistics did not contain the expected values")
    logs = request(api, "GET", f"containers/{container_id}/logs?tail=20&sinceMinutes=60").json()
    if not isinstance(logs.get("lines"), list) or not isinstance(logs.get("truncated"), bool):
        raise RuntimeError("The bounded container log query did not return the expected shape")

    diagnostic_request = {
        "resourceId": None,
        "parameters": {},
        "planHash": None,
        "confirmed": False,
        "idempotencyKey": "host-validation-" + os.urandom(8).hex(),
    }
    submitted = request(
        api,
        "POST",
        "platform/diagnostic-bundle",
        payload=diagnostic_request,
        expected=202,
    ).json()
    job_id = submitted["id"]
    duplicate = request(
        api,
        "POST",
        "platform/diagnostic-bundle",
        payload=diagnostic_request,
        expected=202,
    ).json()
    if duplicate.get("id") != job_id:
        raise RuntimeError("The idempotency key created a duplicate job")
    deadline = time.monotonic() + 90
    job: dict[str, object] = submitted
    while time.monotonic() < deadline:
        job = request(api, "GET", f"jobs/{job_id}").json()
        if job.get("state") in {"Succeeded", "Failed", "Canceled"}:
            break
        time.sleep(2)
    if job.get("state") != "Succeeded":
        raise RuntimeError(f"Diagnostic bundle job ended in {job.get('state')} / {job.get('errorCode')}")

    event_url = f"{base_url}/api/v1/jobs/{job_id}/events"
    event_stream = session.get(event_url, headers={"Accept": "text/event-stream"}, timeout=20)
    event_stream.raise_for_status()
    if "text/event-stream" not in event_stream.headers.get("Content-Type", ""):
        raise RuntimeError("The job event endpoint did not return an SSE stream")
    event_ids = [int(value) for value in re.findall(r"^id:\s*(\d+)\s*$", event_stream.text, re.MULTILINE)]
    if not event_ids:
        raise RuntimeError("The job SSE stream did not contain progress events")
    replay = session.get(
        event_url,
        headers={"Accept": "text/event-stream", "Last-Event-ID": str(max(event_ids))},
        timeout=20,
    )
    replay.raise_for_status()
    if re.search(r"^id:\s*\d+\s*$", replay.text, re.MULTILINE):
        raise RuntimeError("Last-Event-ID replayed an already consumed job event")

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
        f"{len(journal)} journal entries, container telemetry, idempotent SSE jobs and a "
        f"{len(download.content)} byte diagnostic bundle"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
