#!/usr/bin/env python3
"""Exercise database management through API -> Worker -> Agent.

Every resource uses a unique ``wdv_*`` name and is removed before exit. Secrets
stay in memory and are never written to stdout, argv, PostgreSQL, or audit data.
"""

from __future__ import annotations

import json
import os
import secrets
import subprocess
import sys
import time
import uuid
from dataclasses import dataclass

import requests

from validate_oidc import authenticate, required_environment


TERMINAL_STATES = {"Succeeded", "Failed", "Canceled", "RolledBack"}


@dataclass(frozen=True)
class Engine:
    name: str
    resource_id: str
    container: str
    has_database: bool = True


ENGINES = [
    Engine("postgres", "database-platform.postgres", "database-platform-postgres"),
    Engine("mariadb", "database-platform.mariadb", "database-platform-mariadb"),
    Engine("sqlserver", "database-platform.sqlserver", "database-platform-sqlserver"),
    Engine("mongodb", "database-platform.mongodb", "database-platform-mongodb"),
    Engine("valkey", "database-platform.valkey", "database-platform-valkey", False),
    Engine("valkey72", "database-platform.valkey72", "database-platform-valkey72", False),
]


class Api:
    def __init__(self, base_url: str, session: requests.Session) -> None:
        self.base_url = base_url
        self.session = session
        csrf = session.get(f"{base_url}/api/v1/auth/csrf", timeout=20)
        csrf.raise_for_status()
        payload = csrf.json()
        self.headers = {
            "Origin": base_url,
            payload["headerName"]: payload["token"],
        }

    def post(self, path: str, payload: dict[str, object], expected: int) -> requests.Response:
        response = self.session.post(
            f"{self.base_url}/api/v1/{path.lstrip('/')}",
            json=payload,
            headers=self.headers,
            allow_redirects=False,
            timeout=30,
        )
        if response.status_code != expected:
            try:
                problem = response.json()
                detail = f" code={problem.get('code')} title={problem.get('title')} traceId={problem.get('traceId')}"
            except (ValueError, AttributeError):
                detail = ""
            raise RuntimeError(f"POST {path} returned HTTP {response.status_code}, expected {expected}.{detail}")
        return response

    def stage_secret(self, value: str) -> str:
        return self.post("secrets/stage", {"value": value, "generate": False}, 200).json()["token"]

    def plan(self, action: str, resource_id: str, parameters: dict[str, str]) -> str:
        payload = self.post(
            f"databases/{action}/plan",
            {"resourceId": resource_id, "parameters": parameters},
            200,
        ).json()
        if not payload.get("planHash"):
            raise RuntimeError(f"Database {action} plan did not return a plan hash")
        return payload["planHash"]

    def run(
        self,
        action: str,
        resource_id: str,
        parameters: dict[str, str],
        *,
        confirmed: bool = False,
        requires_plan: bool = False,
        timeout: int = 120,
    ) -> dict[str, object]:
        plan_hash = self.plan(action, resource_id, parameters) if requires_plan else None
        key = f"validation:{action}:{uuid.uuid4()}"
        job = self.post(
            f"databases/{action}",
            {
                "resourceId": resource_id,
                "idempotencyKey": key,
                "parameters": parameters,
                "planHash": plan_hash,
                "confirmed": confirmed,
            },
            202,
        ).json()
        return self.wait_job(job["id"], timeout)

    def wait_job(self, job_id: str, timeout: int) -> dict[str, object]:
        deadline = time.monotonic() + timeout
        previous = None
        while time.monotonic() < deadline:
            response = self.session.get(f"{self.base_url}/api/v1/jobs/{job_id}", timeout=20)
            response.raise_for_status()
            job = response.json()
            state = job.get("state")
            phase = job.get("phase")
            marker = (state, phase)
            if marker != previous:
                print(f"  job {job_id[:8]}: {state}/{phase}")
                previous = marker
            if state in TERMINAL_STATES:
                if state != "Succeeded":
                    raise RuntimeError(
                        f"Job {job_id} failed in {phase} with {job.get('errorCode') or 'unknown error'}"
                    )
                return job
            time.sleep(2)
        raise RuntimeError(f"Job {job_id} did not finish within {timeout} seconds")

    def assert_one_time_secret(self, original_ticket: str, job: dict[str, object], expected: str) -> None:
        expired = self.post("secrets/consume", {"token": original_ticket}, 404)
        if expired.status_code != 404:
            raise RuntimeError("Consumed input secret remained readable")
        result = job.get("resultJson")
        result_payload = json.loads(result) if isinstance(result, str) else result
        if not isinstance(result_payload, dict) or not result_payload.get("secretToken"):
            raise RuntimeError("Completed credential job did not return a successor secret token")
        successor = result_payload["secretToken"]
        first = self.post("secrets/consume", {"token": successor}, 200).json()
        if first.get("value") != expected:
            raise RuntimeError("Successor secret value did not match the staged credential")
        self.post("secrets/consume", {"token": successor}, 404)


def password() -> str:
    return f"Wd!{secrets.token_urlsafe(18)}9aA"


def docker_output(arguments: list[str]) -> str:
    result = subprocess.run(
        ["docker", *arguments],
        check=True,
        text=True,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        timeout=30,
    )
    return result.stdout.strip()


def assert_present(engine: Engine, database: str, principal: str) -> None:
    if engine.name == "postgres":
        output = docker_output(["exec", "-u", "postgres", engine.container, "psql", "-At", "--dbname", "postgres", "-c",
            f"SELECT datname FROM pg_database WHERE datname='{database}'; SELECT rolname FROM pg_roles WHERE rolname='{principal}';"])
    elif engine.name == "mariadb":
        query = f"SELECT SCHEMA_NAME FROM information_schema.SCHEMATA WHERE SCHEMA_NAME='{database}'; SELECT User FROM mysql.user WHERE User='{principal}';"
        output = docker_output(["exec", engine.container, "sh", "-ec", 'exec mariadb --protocol=socket -uroot -p"$MARIADB_ROOT_PASSWORD" -Nse "$1"', "--", query])
    elif engine.name == "sqlserver":
        query = f"SET NOCOUNT ON; SELECT name FROM sys.databases WHERE name='{database}'; SELECT name FROM sys.server_principals WHERE name='{principal}';"
        output = docker_output(["exec", engine.container, "sh", "-ec", 'tool=$(command -v sqlcmd || true); [ -n "$tool" ] || tool=/opt/mssql-tools18/bin/sqlcmd; exec "$tool" -C -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -h -1 -W -Q "$1"', "--", query])
    elif engine.name == "mongodb":
        script = f"const d=db.getMongo().getDBNames().includes('{database}'); const u=db.getSiblingDB('{database}').getUser('{principal}')!==null; print(d&&u?'present':'missing');"
        output = docker_output(["exec", engine.container, "sh", "-ec", 'exec mongosh --quiet --username "$MONGO_INITDB_ROOT_USERNAME" --password "$MONGO_INITDB_ROOT_PASSWORD" --authenticationDatabase admin --eval "$1"', "--", script])
    else:
        output = docker_output(["exec", engine.container, "sh", "-ec", 'exec valkey-cli --no-auth-warning -a "$VALKEY_PASSWORD" ACL GETUSER "$1"', "--", principal])
    if (engine.has_database and (database not in output or principal not in output)) or (not engine.has_database and "flags" not in output):
        raise RuntimeError(f"{engine.name} temporary resources were not visible after creation")


def assert_absent(engine: Engine, database: str, principal: str) -> None:
    if engine.name == "postgres":
        output = docker_output(["exec", "-u", "postgres", engine.container, "psql", "-At", "--dbname", "postgres", "-c",
            f"SELECT count(*) FROM pg_database WHERE datname='{database}'; SELECT count(*) FROM pg_roles WHERE rolname='{principal}';"])
    elif engine.name == "mariadb":
        query = f"SELECT count(*) FROM information_schema.SCHEMATA WHERE SCHEMA_NAME='{database}'; SELECT count(*) FROM mysql.user WHERE User='{principal}';"
        output = docker_output(["exec", engine.container, "sh", "-ec", 'exec mariadb --protocol=socket -uroot -p"$MARIADB_ROOT_PASSWORD" -Nse "$1"', "--", query])
    elif engine.name == "sqlserver":
        query = f"SET NOCOUNT ON; SELECT count(*) FROM sys.databases WHERE name='{database}'; SELECT count(*) FROM sys.server_principals WHERE name='{principal}';"
        output = docker_output(["exec", engine.container, "sh", "-ec", 'tool=$(command -v sqlcmd || true); [ -n "$tool" ] || tool=/opt/mssql-tools18/bin/sqlcmd; exec "$tool" -C -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -h -1 -W -Q "$1"', "--", query])
    elif engine.name == "mongodb":
        script = f"const d=db.getMongo().getDBNames().includes('{database}'); const u=d&&db.getSiblingDB('{database}').getUser('{principal}')!==null; print(!d&&!u?'absent':'present');"
        output = docker_output(["exec", engine.container, "sh", "-ec", 'exec mongosh --quiet --username "$MONGO_INITDB_ROOT_USERNAME" --password "$MONGO_INITDB_ROOT_PASSWORD" --authenticationDatabase admin --eval "$1"', "--", script])
    else:
        output = docker_output(["exec", engine.container, "sh", "-ec", 'exec valkey-cli --no-auth-warning -a "$VALKEY_PASSWORD" ACL GETUSER "$1"', "--", principal])
    if engine.name in {"postgres", "mariadb", "sqlserver"} and any(line.strip() != "0" for line in output.splitlines() if line.strip()):
        raise RuntimeError(f"{engine.name} temporary resources remained after cleanup")
    if engine.name == "mongodb" and "absent" not in output:
        raise RuntimeError("mongodb temporary resources remained after cleanup")
    if not engine.has_database and output:
        raise RuntimeError(f"{engine.name} temporary ACL user remained after cleanup")


def validate_engine(api: Api, engine: Engine, suffix: str) -> None:
    database = f"wdv_{engine.name}_{suffix}"[:63]
    principal = f"wdv_{engine.name}_u_{suffix}"[:63]
    database_created = False
    principal_created = False
    print(f"Validating {engine.name}...")
    try:
        if engine.has_database:
            api.run("create", engine.resource_id, {"name": database}, requires_plan=True)
            database_created = True

        initial_password = password()
        ticket = api.stage_secret(initial_password)
        parameters = {"principal": principal, "role": "readwrite", "inputTicket": ticket, "inputKind": "password"}
        if engine.has_database:
            parameters["database"] = database
        else:
            parameters["keyPrefix"] = f"wdv:{suffix}:"
        created = api.run("create-principal", engine.resource_id, parameters)
        principal_created = True
        api.assert_one_time_secret(ticket, created, initial_password)
        assert_present(engine, database, principal)

        grant = {"principal": principal, "role": "readonly"}
        if engine.has_database:
            grant["database"] = database
        else:
            grant["keyPrefix"] = f"wdv:{suffix}:"
        api.run("grant", engine.resource_id, grant)

        rotated_password = password()
        rotate_ticket = api.stage_secret(rotated_password)
        rotate = {"principal": principal, "inputTicket": rotate_ticket, "inputKind": "password"}
        if engine.name == "mongodb":
            rotate["database"] = database
        api.run("rotate", engine.resource_id, rotate)

        terminate = {"principal": principal}
        if engine.name == "postgres":
            terminate = {"database": database}
        elif engine.name == "mongodb":
            terminate["database"] = database
        api.run("terminate-connection", engine.resource_id, terminate)

        disable = {"principal": principal}
        if engine.name == "mongodb":
            disable["database"] = database
        api.run("disable-principal", engine.resource_id, disable)

        if engine.name == "postgres":
            api.run("delete", engine.resource_id, {"name": database}, confirmed=True, requires_plan=True)
            database_created = False
        delete_principal = {"principal": principal}
        if engine.name in {"sqlserver", "mongodb"}:
            delete_principal["database"] = database
        api.run("delete-principal", engine.resource_id, delete_principal, confirmed=True, requires_plan=True)
        principal_created = False
        if engine.has_database and database_created:
            api.run("delete", engine.resource_id, {"name": database}, confirmed=True, requires_plan=True)
            database_created = False
        assert_absent(engine, database, principal)
        print(f"{engine.name}: lifecycle passed")
    finally:
        # Best-effort API cleanup keeps failure paths scoped to the unique validation names.
        if principal_created:
            try:
                parameters = {"principal": principal}
                if engine.name in {"sqlserver", "mongodb"}:
                    parameters["database"] = database
                api.run("delete-principal", engine.resource_id, parameters, confirmed=True, requires_plan=True)
            except Exception as error:
                print(f"{engine.name}: principal cleanup warning: {error}", file=sys.stderr)
        if database_created:
            try:
                api.run("delete", engine.resource_id, {"name": database}, confirmed=True, requires_plan=True)
            except Exception as error:
                print(f"{engine.name}: database cleanup warning: {error}", file=sys.stderr)


def main() -> int:
    base_url = os.environ.get("WHALEDECK_URL", "http://192.168.100.13:8080").rstrip("/")
    session, _ = authenticate(
        base_url,
        required_environment("VALIDATION_USERNAME"),
        required_environment("VALIDATION_PASSWORD"),
    )
    api = Api(base_url, session)
    suffix = f"{int(time.time()) % 1_000_000}_{secrets.token_hex(2)}"
    for engine in ENGINES:
        validate_engine(api, engine, suffix)
    print("Database validation passed: six adapters completed managed lifecycle and cleanup")
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except Exception as error:
        print(f"Database validation failed: {error}", file=sys.stderr)
        raise SystemExit(1)
