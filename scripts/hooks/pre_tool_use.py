#!/usr/bin/env python3
"""
PreToolUse dispatcher for Apex Air architectural guardrails.

Reads a Claude Code hook payload from stdin, routes to the appropriate
guard, and exits with:
  0  — allow the operation (optionally with additionalContext on stdout)
  2  — block the operation (reason on stderr)
"""

import json
import re
import sys
import os
from datetime import datetime, timezone


AUDIT_LOG = os.path.join(
    os.path.dirname(__file__), "..", "..", ".claude", "hook-audit.log"
)


def _audit(reason: str) -> None:
    ts = datetime.now(timezone.utc).isoformat()
    os.makedirs(os.path.dirname(AUDIT_LOG), exist_ok=True)
    with open(AUDIT_LOG, "a") as fh:
        fh.write(f"[{ts}] BLOCKED — {reason}\n")


def _block(message: str) -> None:
    _audit(message)
    print(message, file=sys.stderr)
    sys.exit(2)


def _allow(additional_context: str | None = None) -> None:
    if additional_context:
        print(json.dumps({"additionalContext": additional_context}))
    sys.exit(0)


# ---------------------------------------------------------------------------
# Hook 1 — Microservice boundary guard
# Fires on Write/Edit of .csproj files.
# Blocks any <ProjectReference> whose path resolves to another Functions
# project (path segment matches *Functions* or filename ends *Function.csproj).
# ---------------------------------------------------------------------------

_FUNCTIONS_RE = re.compile(
    r"<ProjectReference[^>]+Include\s*=\s*[\"']([^\"']+)[\"']",
    re.IGNORECASE,
)

_BAD_PATH_RE = re.compile(
    r"(Functions[/\\]|Function\.csproj$)",
    re.IGNORECASE,
)


def _guard_microservice_boundary(tool_name: str, tool_input: dict) -> None:
    file_path: str = tool_input.get("file_path", tool_input.get("path", ""))
    if not file_path.endswith(".csproj"):
        return

    content: str = tool_input.get("content", "") or tool_input.get("new_string", "")
    if not content:
        return

    for match in _FUNCTIONS_RE.finditer(content):
        ref_path = match.group(1)
        if _BAD_PATH_RE.search(ref_path):
            _block(
                "BLOCKED: Direct project reference between Azure Function microservices "
                "violates the Apex Air boundary rule. Communicate via HTTP endpoints or "
                "Azure Service Bus only. See design.md section on integration principles."
            )


# ---------------------------------------------------------------------------
# Hook 2 — Secret exposure guard
# Fires on Write/Edit/Bash.
# Blocks connection strings, SAS tokens, hardcoded API keys, and .env writes.
# ---------------------------------------------------------------------------

_SECRET_PATTERNS: list[tuple[str, re.Pattern]] = [
    ("connection string (Server=)", re.compile(r"\bServer\s*=\s*.+Database\s*=", re.IGNORECASE)),
    ("connection string (Password=)", re.compile(r"\bPassword\s*=\s*[^\s;\"']{4,}", re.IGNORECASE)),
    ("Azure SAS token (sig=)", re.compile(r"\bsig\s*=\s*[A-Za-z0-9%+/]{8,}", re.IGNORECASE)),
    ("hardcoded API key", re.compile(r"\bapi[_-]?key\s*[=:]\s*[\"']?[A-Za-z0-9\-_]{8,}[\"']?", re.IGNORECASE)),
]

_DOTENV_RE = re.compile(r"\.env\b")
_SAFE_PATH_RE = re.compile(r"(local-settings|local\.settings\.json|\.gitignore)", re.IGNORECASE)


def _guard_secrets(tool_name: str, tool_input: dict) -> None:
    if tool_name == "Bash":
        text = tool_input.get("command", "")
    else:
        file_path: str = tool_input.get("file_path", tool_input.get("path", ""))
        # .env file write outside approved locations
        if _DOTENV_RE.search(os.path.basename(file_path)) and not _SAFE_PATH_RE.search(file_path):
            _block(
                "BLOCKED: Potential secret or connection string detected outside approved "
                "config pattern. Use Azure Key Vault references or local.settings.json "
                "(gitignored) only."
            )
        text = tool_input.get("content", "") or tool_input.get("new_string", "") or ""

    for label, pattern in _SECRET_PATTERNS:
        if pattern.search(text):
            _block(
                "BLOCKED: Potential secret or connection string detected outside approved "
                "config pattern. Use Azure Key Vault references or local.settings.json "
                "(gitignored) only."
            )


# ---------------------------------------------------------------------------
# Hook 3 — Domain rule context injection
# Fires on Write/Edit when the file name contains domain keywords.
# Allows the operation but injects a domain reminder via additionalContext.
# ---------------------------------------------------------------------------

_DOMAIN_KEYWORDS = re.compile(
    r"(Coupon|Ticket|Fare|PNR|Order)",
    re.IGNORECASE,
)

_DOMAIN_REMINDER = (
    "Apex Air domain reminder: Coupon status follows the ONE Order state machine "
    "(Offered > Booked > Ticketed > Lifted > Flown). "
    "Fare calculations must include base fare, carrier-imposed surcharges, and ATPCO taxes separately. "
    "PNR and ticket number are distinct identifiers. "
    "Check design.md before modifying these domain objects."
)


def _inject_domain_context(tool_name: str, tool_input: dict) -> None:
    file_path: str = tool_input.get("file_path", tool_input.get("path", ""))
    basename = os.path.basename(file_path)
    if _DOMAIN_KEYWORDS.search(basename):
        _allow(_DOMAIN_REMINDER)


# ---------------------------------------------------------------------------
# Dispatcher
# ---------------------------------------------------------------------------

def main() -> None:
    try:
        payload = json.load(sys.stdin)
    except json.JSONDecodeError as exc:
        print(f"pre_tool_use: invalid JSON from stdin: {exc}", file=sys.stderr)
        sys.exit(0)  # don't block on parse failure

    tool_name: str = payload.get("tool_name", "")
    tool_input: dict = payload.get("tool_input", {})

    write_edit = tool_name in ("Write", "Edit")
    bash = tool_name == "Bash"

    # Hook 2 runs on Write, Edit, and Bash
    if write_edit or bash:
        _guard_secrets(tool_name, tool_input)

    if write_edit:
        # Hook 1 — csproj boundary check
        _guard_microservice_boundary(tool_name, tool_input)
        # Hook 3 — domain context injection (exits 0 with context if matched)
        _inject_domain_context(tool_name, tool_input)

    _allow()


if __name__ == "__main__":
    main()
