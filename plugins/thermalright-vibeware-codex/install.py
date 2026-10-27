# VBWR B
#
# Project: Thermalright VibeWare
# Repository: https://github.com/umbertotechnopreneur/thermalright-vibeware
# Creator: Umberto Giacobbi | https://umbertogiacobbi.biz
#
# File: install.py
# Purpose: Prepares the bundled Codex plugin and its local marketplace registration.
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

"""Stage the optional local Codex plugin for manual install and hook trust."""

from __future__ import annotations

import argparse
import json
import os
from pathlib import Path
import shutil
import sys
import tempfile
from uuid import uuid4

PLUGIN_NAME = "thermalright-vibeware-codex"
SOURCE_PATH = "./.codex/plugins/thermalright-vibeware-codex"

def locations() -> tuple[Path, Path]:
    """Resolve the per-user Codex plugin and marketplace file locations."""
    home = Path.home()
    return (home / ".codex" / "plugins" / PLUGIN_NAME,
            home / ".agents" / "plugins" / "marketplace.json")

def read_marketplace(path: Path) -> dict[str, object]:
    """Read the bounded local marketplace file or create an empty supported structure.

    Args:
        path: Local state or marketplace JSON file path.
    """
    if not path.exists():
        return {"name": "thermalright-vibeware-local", "plugins": []}
    if path.stat().st_size > 1_048_576:
        raise ValueError("Personal marketplace is too large to edit automatically.")
    data = json.loads(path.read_text(encoding="utf-8"))
    if not isinstance(data, dict) or not isinstance(data.get("name"), str) \
            or not isinstance(data.get("plugins"), list):
        raise ValueError("Personal marketplace has an unsupported shape; no changes were made.")
    return data

def atomic_json(path: Path, value: dict[str, object]) -> None:
    """Replace a local JSON file atomically after writing its temporary sibling.

    Args:
        path: Local state or marketplace JSON file path.
        value: JSON-compatible state object written to a temporary sibling before atomic replacement.
    """
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = path.with_name(path.name + "." + uuid4().hex + ".tmp")
    try:
        temporary.write_text(json.dumps(value, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
        os.replace(temporary, path)
    finally:
        temporary.unlink(missing_ok=True)

def plugin_entry() -> dict[str, object]:
    """Describe the bundled plugin's marketplace entry."""
    return {
        "name": PLUGIN_NAME,
        "source": {"source": "local", "path": SOURCE_PATH},
        "policy": {"installation": "AVAILABLE", "authentication": "ON_INSTALL"},
        "category": "Productivity",
        "interface": {"displayName": "Thermalright VibeWare Codex"},
    }

def stage_portable_python(destination: Path) -> bool:
    """Keep the optional hook working after the portable Thermalright VibeWare ZIP is moved.

    Args:
        destination: Local plugin/runtime staging directory owned by this operation.
    """
    if os.name != "nt":
        return False
    runtime = Path(sys.executable).resolve().parent
    tag = f"python{sys.version_info.major}{sys.version_info.minor}"
    if not (runtime / f"{tag}.zip").is_file() or not (runtime / f"{tag}._pth").is_file():
        return False  # A normal Python install remains the user's own prerequisite.
    files = [item for item in runtime.iterdir() if item.is_file() and not item.is_symlink()
             and (item.suffix.lower() in {".exe", ".dll", ".pyd", ".zip", ".txt"} or item.name == f"{tag}._pth")]
    if len(files) > 128 or sum(item.stat().st_size for item in files) > 80 * 1024 * 1024:
        raise ValueError("The bundled Python runtime is unexpectedly large; no hook runtime was staged.")
    target = destination / "python"
    target.mkdir(exist_ok=True)
    for item in files:
        shutil.copy2(item, target / item.name)
    (target / f"{tag}._pth").write_text(f"{tag}.zip\n.\nimport site\n", encoding="ascii")
    hooks_path = destination / "hooks" / "hooks.json"
    hooks = json.loads(hooks_path.read_text(encoding="utf-8"))
    interpreter = target / "python.exe"
    script = destination / "hooks" / "thermalright-vibeware_hook.py"
    if not interpreter.is_file() or not script.is_file():
        raise ValueError("The staged portable hook runtime is incomplete.")
    # Codex passes commandWindows to the session shell; Windows defaults to PowerShell.
    # Its call operator is required when an executable path is quoted.
    command = f'& "{interpreter}" -B "{script}"'
    changed = False
    for matchers in hooks.get("hooks", {}).values():
        for matcher in matchers:
            for hook in matcher.get("hooks", []):
                if hook.get("type") == "command" and hook.get("commandWindows") != command:
                    hook["commandWindows"] = command
                    changed = True
    if changed:
        atomic_json(hooks_path, hooks)
    return True

def stage_plugin(source: Path, destination: Path) -> None:
    """Stage the bundled plugin and its portable Python runtime.

    Args:
        source: Bundled plugin source directory to copy.
        destination: Local plugin/runtime staging directory owned by this operation.
    """
    if destination.exists():
        if destination.is_symlink():
            raise ValueError("The plugin destination is a link; no changes were made.")
        manifest = destination / "plugin.json"
        if not manifest.is_file() or json.loads(manifest.read_text(encoding="utf-8")).get("name") != PLUGIN_NAME:
            raise ValueError("The destination already contains another plugin; no changes were made.")
        stage_portable_python(destination)
        return
    destination.parent.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory(prefix=".thermalright-vibeware-", dir=destination.parent) as staging_root:
        staged = Path(staging_root) / PLUGIN_NAME
        shutil.copytree(source, staged, ignore=shutil.ignore_patterns("__pycache__", "*.pyc"))
        os.replace(staged, destination)
    stage_portable_python(destination)

def prepare() -> None:
    """Stage and list the bundled local plugin without enabling arbitrary theme providers."""
    if sys.version_info < (3, 10):
        raise ValueError("Python 3.10 or newer is required for the ThermalrightVibeWare Codex hook.")
    source = Path(__file__).resolve().parent
    destination, marketplace_path = locations()
    marketplace = read_marketplace(marketplace_path)
    plugins = marketplace["plugins"]
    assert isinstance(plugins, list)
    for entry in plugins:
        if isinstance(entry, dict) and entry.get("name") == PLUGIN_NAME:
            if entry.get("source") != plugin_entry()["source"]:
                raise ValueError("A different ThermalrightVibeWare Codex entry already exists; no changes were made.")
            stage_plugin(source, destination)
            print("Thermalright VibeWare Codex is already listed in the personal marketplace.")
            return
    stage_plugin(source, destination)
    plugins.append(plugin_entry())
    if marketplace_path.exists():
        backup = marketplace_path.with_name("marketplace.json.thermalright-vibeware.bak")
        if not backup.exists():
            shutil.copy2(marketplace_path, backup)
    atomic_json(marketplace_path, marketplace)
    print(f"Staged {destination}")
    print("Restart Codex, install Thermalright VibeWare Codex from your local Plugins Directory, then review and trust its hooks.")

def status() -> None:
    """Print the current local plugin preparation and listing status."""
    destination, marketplace_path = locations()
    marketplace = read_marketplace(marketplace_path)
    entries = marketplace["plugins"]
    assert isinstance(entries, list)
    listed = any(isinstance(item, dict) and item.get("name") == PLUGIN_NAME for item in entries)
    print(f"Plugin files: {'present' if destination.is_dir() else 'absent'}")
    print(f"Personal marketplace: {'listed' if listed else 'not listed'}")
    print("Codex installation and hook trust must be checked inside Codex.")

def unlist() -> None:
    """Remove only this plugin's local marketplace listing."""
    _, marketplace_path = locations()
    marketplace = read_marketplace(marketplace_path)
    entries = marketplace["plugins"]
    assert isinstance(entries, list)
    remaining = [item for item in entries if not (isinstance(item, dict) and item.get("name") == PLUGIN_NAME)]
    if len(remaining) == len(entries):
        print("ThermalrightVibeWare Codex is not listed in the personal marketplace.")
        return
    marketplace["plugins"] = remaining
    atomic_json(marketplace_path, marketplace)
    print("Removed ThermalrightVibeWare Codex from the personal marketplace; staged files were preserved.")

def main() -> int:
    """Dispatch explicit local plugin preparation, status and unlisting commands."""
    parser = argparse.ArgumentParser(description="Prepare my optional ThermalrightVibeWare Codex plugin")
    parser.add_argument("action", choices=("status", "prepare", "unlist"), nargs="?", default="status")
    args = parser.parse_args()
    try:
        {"status": status, "prepare": prepare, "unlist": unlist}[args.action]()
        return 0
    except (OSError, ValueError, json.JSONDecodeError) as error:
        print(f"ThermalrightVibeWare Codex: {error}", file=sys.stderr)
        return 1

if __name__ == "__main__":
    raise SystemExit(main())
