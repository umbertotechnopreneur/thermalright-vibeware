# VBWR B
#
# Project: Thermalright VibeWare
# Repository: https://github.com/umbertotechnopreneur/thermalright-vibeware
# Creator: Umberto Giacobbi | https://umbertogiacobbi.biz
#
# File: thermalright-vibeware_hook.py
# Purpose: Stores bounded local Codex activity without prompts or tool payloads.
#
# VibeWare: Human intent, AI execution, and plenty of tokens
# Manifesto: https://umbertogiacobbi.biz/vibeware/manifesto
#
# Modified with AI: OpenAI Codex; source documentation and organization; 2026-10-27.
# Human guidance: Umberto Giacobbi; maintainability and standard file headers.
#
# Copyright (c) 2026 Umberto Giacobbi
# SPDX-License-Identifier: MIT
#
# VBWR E

"""Record minimal Codex lifecycle status for the local ThermalrightVibeWare display."""

from __future__ import annotations

import hashlib
import json
import os
from pathlib import Path
import sys
import time
from datetime import datetime, timezone
from uuid import uuid4

MAX_INPUT_BYTES = 1_048_576
MAX_SESSIONS = 64
KNOWN_EVENTS = {
    "SessionStart": ("Chat ready", "ready", False),
    "UserPromptSubmit": ("Thinking", "working", True),
    "PreToolUse": ("Using a tool", "working", True),
    "PermissionRequest": ("Approval requested on PC", "waiting", True),
    "PostToolUse": ("Thinking", "working", True),
    "PreCompact": ("Refreshing context", "working", True),
    "PostCompact": ("Continuing work", "working", True),
    "SubagentStart": ("Agent working", "working", True),
    "SubagentStop": ("Agent finished; continuing", "working", True),
    "Stop": ("Reply ready", "ready", False),
    "Interrupt": ("Interrupted", "interrupted", False),
    "SessionEnd": ("Chat closed", "closed", False),
}

def state_directory() -> Path:
    """Use the same user-local location as the ThermalrightVibeWare theme host."""
    if os.name == "nt":
        local = os.environ.get("LOCALAPPDATA")
        base = Path(local) if local else Path.home() / "AppData" / "Local"
        return base / "ThermalrightVibeWare" / "codex"
    if sys.platform == "darwin":
        return Path.home() / "Library" / "Application Support" / "ThermalrightVibeWare" / "codex"
    xdg = os.environ.get("XDG_STATE_HOME", "")
    base = Path(xdg) if xdg and Path(xdg).is_absolute() else Path.home() / ".local" / "state"
    return base / "ThermalrightVibeWare" / "codex"

def opaque_key(value: object) -> str | None:
    """Hash an identity into a bounded opaque key without retaining its private text.

    Args:
        value: Private source identity converted to a bounded opaque key before retention.
    """
    if not isinstance(value, str) or not value or len(value) > 256:
        return None
    return hashlib.sha256(value.encode("utf-8")).hexdigest()[:20]

def tool_kind(value: object) -> str | None:
    """Reduce a tool name to the allow-listed display category.

    Args:
        value: Untrusted tool name reduced to an allow-listed display category.
    """
    if not isinstance(value, str):
        return None
    if value in {"Bash", "exec_command"}:
        return "terminal"
    if value in {"apply_patch", "Edit", "Write"}:
        return "files"
    if value.startswith("mcp__"):
        return "connected-app"
    return "tool"

def display_status(event: str, kind: str | None) -> tuple[str, str, bool]:
    """Map an allowed Codex event to brief display text and activity state.

    Args:
        event: Allow-listed local Codex lifecycle event name.
        kind: Sensor type or allow-listed Codex tool category used by this helper.
    """
    label, phase, is_live = KNOWN_EVENTS[event]
    if event == "PreToolUse":
        label = {
            "terminal": "Running a command",
            "files": "Editing files",
            "connected-app": "Using a connected app",
        }.get(kind, label)
    return label, phase, is_live

def event_from_input(payload: object) -> dict[str, object] | None:
    """Extract only the allow-listed activity fields from an untrusted hook payload.

    Args:
        payload: Untrusted hook input; only allow-listed activity fields are retained.
    """
    if not isinstance(payload, dict):
        return None
    event = payload.get("hook_event_name")
    session_key = opaque_key(payload.get("session_id"))
    if not isinstance(event, str) or event not in KNOWN_EVENTS or session_key is None:
        return None

    kind = tool_kind(payload.get("tool_name")) if event in {
        "PreToolUse", "PermissionRequest", "PostToolUse"
    } else None
    status, phase, is_live = display_status(event, kind)
    now_ns = time.time_ns()
    now_utc = datetime.fromtimestamp(now_ns / 1_000_000_000, timezone.utc)
    result: dict[str, object] = {
        "version": 1,
        "kind": "codex.activity",
        "sessionKey": session_key,
        "turnKey": opaque_key(payload.get("turn_id")),
        "event": event,
        "status": status,
        "phase": phase,
        "isLive": is_live,
        "atUtc": now_utc.isoformat(timespec="milliseconds").replace("+00:00", "Z"),
        "atUnixNs": now_ns,
        "toolKind": kind,
        "chatName": None,
    }
    source = payload.get("source")
    if event == "SessionStart" and isinstance(source, str) and source in {
        "startup", "resume", "clear", "compact", "fork"
    }:
        result["source"] = source
    trigger = payload.get("trigger")
    if event in {"PreCompact", "PostCompact"} and isinstance(trigger, str) and trigger in {
        "auto", "manual"
    }:
        result["trigger"] = trigger
    return result

def read_timestamp(path: Path) -> int:
    """Read the last bounded event timestamp, treating absent data as zero.

    Args:
        path: Local state or marketplace JSON file path.
    """
    try:
        with path.open("rb") as stream:
            previous = json.loads(stream.read(4096))
        value = previous.get("atUnixNs") if isinstance(previous, dict) else None
        return value if isinstance(value, int) else 0
    except (OSError, ValueError, TypeError):
        return 0

def atomic_json(path: Path, value: dict[str, object]) -> None:
    """Replace a local JSON file atomically after writing its temporary sibling.

    Args:
        path: Local state or marketplace JSON file path.
        value: JSON-compatible state object written to a temporary sibling before atomic replacement.
    """
    temporary = path.with_name(path.name + "." + uuid4().hex + ".tmp")
    try:
        with temporary.open("x", encoding="utf-8") as stream:
            json.dump(value, stream, ensure_ascii=True, separators=(",", ":"))
            stream.write("\n")
        os.replace(temporary, path)
    finally:
        try:
            temporary.unlink(missing_ok=True)
        except OSError:
            pass

def acquire_lock(root: Path) -> Path | None:
    """Claim the local state lock or recover only an expired lock.

    Args:
        root: Per-user local activity-state directory.
    """
    lock = root / ".write-lock"
    deadline = time.monotonic() + 0.2
    while time.monotonic() < deadline:
        try:
            lock.mkdir()
            return lock
        except FileExistsError:
            try:
                if time.time() - lock.stat().st_mtime > 10:
                    lock.rmdir()
            except OSError:
                pass
            time.sleep(0.01)
    return None

def retain_recent_sessions(directory: Path) -> None:
    """Remove only obsolete session records above the bounded retention policy.

    Args:
        directory: Local session-state directory subject to bounded retention.
    """
    files = list(directory.glob("*.json"))
    if len(files) <= MAX_SESSIONS:
        return
    files.sort(key=lambda path: path.stat().st_mtime_ns)
    for old in files[: len(files) - MAX_SESSIONS]:
        old.unlink(missing_ok=True)

def store_event(value: dict[str, object]) -> None:
    """Persist a bounded sanitized event and update the latest session observation.

    Args:
        value: Sanitized allow-listed event fields to retain in bounded local state.
    """
    root = state_directory()
    sessions = root / "sessions"
    sessions.mkdir(parents=True, exist_ok=True, mode=0o700)
    lock = acquire_lock(root)
    if lock is None:
        return
    try:
        session_file = sessions / (str(value["sessionKey"]) + ".json")
        event_time = int(value["atUnixNs"])
        if event_time > read_timestamp(session_file):
            atomic_json(session_file, value)
        current_file = root / "current.json"
        if event_time > read_timestamp(current_file):
            atomic_json(current_file, value)
        retain_recent_sessions(sessions)
    finally:
        try:
            lock.rmdir()
        except OSError:
            pass

def main() -> None:
    """Read and sanitize one bounded Codex hook event before local persistence."""
    try:
        raw = sys.stdin.buffer.read(MAX_INPUT_BYTES + 1)
        if len(raw) > MAX_INPUT_BYTES:
            # Drain a large tool result without keeping it or closing Codex's pipe early.
            while sys.stdin.buffer.read(65_536):
                pass
        else:
            value = event_from_input(json.loads(raw))
            if value is not None:
                store_event(value)
    except Exception:
        # Display telemetry is advisory: never alter the Codex operation.
        pass
    finally:
        # Stop, SubagentStop, and Interrupt require JSON, and {} is safe for all events.
        sys.stdout.write("{}\n")

if __name__ == "__main__":
    main()
