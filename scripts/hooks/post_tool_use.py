#!/usr/bin/env python3
"""
PostToolUse dispatcher for Apex Air architectural guardrails.

Hook 4 — Post-write build validation.
After any .cs file is written, locates the owning .csproj by walking up
the directory tree and runs `dotnet build` on that project only.
Compiler errors are written to stdout so Claude can self-correct.
"""

import json
import os
import subprocess
import sys
from datetime import datetime, timezone


AUDIT_LOG = os.path.join(
    os.path.dirname(__file__), "..", "..", ".claude", "hook-audit.log"
)

BUILD_TIMEOUT = 60  # seconds


def _audit(message: str) -> None:
    ts = datetime.now(timezone.utc).isoformat()
    os.makedirs(os.path.dirname(AUDIT_LOG), exist_ok=True)
    with open(AUDIT_LOG, "a") as fh:
        fh.write(f"[{ts}] BUILD — {message}\n")


def _find_csproj(start_dir: str) -> str | None:
    """Walk up from start_dir until a .csproj file is found."""
    current = os.path.abspath(start_dir)
    while True:
        for entry in os.listdir(current):
            if entry.endswith(".csproj"):
                return os.path.join(current, entry)
        parent = os.path.dirname(current)
        if parent == current:
            return None
        current = parent


def _run_build(csproj_path: str) -> None:
    _audit(f"dotnet build triggered for {csproj_path}")
    try:
        result = subprocess.run(
            ["dotnet", "build", csproj_path, "--no-restore", "-v", "quiet"],
            capture_output=True,
            text=True,
            timeout=BUILD_TIMEOUT,
        )
    except subprocess.TimeoutExpired:
        msg = f"BUILD TIMEOUT: dotnet build for {csproj_path} exceeded {BUILD_TIMEOUT}s."
        _audit(msg)
        print(msg)
        return
    except FileNotFoundError:
        # dotnet SDK not installed in this environment — skip silently.
        return

    if result.returncode != 0:
        output = (result.stdout + result.stderr).strip()
        _audit(f"build failed: {csproj_path}")
        print(
            f"BUILD FAILED for {csproj_path}:\n\n{output}\n\n"
            "Please review the compiler errors above and correct the file."
        )


# ---------------------------------------------------------------------------
# Dispatcher
# ---------------------------------------------------------------------------

def main() -> None:
    try:
        payload = json.load(sys.stdin)
    except json.JSONDecodeError as exc:
        print(f"post_tool_use: invalid JSON from stdin: {exc}", file=sys.stderr)
        sys.exit(0)

    tool_name: str = payload.get("tool_name", "")
    tool_input: dict = payload.get("tool_input", {})

    if tool_name != "Write":
        sys.exit(0)

    file_path: str = tool_input.get("file_path", tool_input.get("path", ""))
    if not file_path.endswith(".cs"):
        sys.exit(0)

    start_dir = os.path.dirname(os.path.abspath(file_path))
    csproj = _find_csproj(start_dir)
    if csproj is None:
        sys.exit(0)

    _run_build(csproj)
    sys.exit(0)


if __name__ == "__main__":
    main()
