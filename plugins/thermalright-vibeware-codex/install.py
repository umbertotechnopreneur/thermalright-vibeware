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
import stat
import sys
import tempfile

PLUGIN_NAME = "thermalright-vibeware-codex"
SOURCE_PATH = "./.codex/plugins/thermalright-vibeware-codex"
PLUGIN_FILES = ("README.md", "install.py", "hooks/thermalright-vibeware_hook.py",
                "hooks/hooks.json", "plugin.json")

def locations() -> tuple[Path, Path]:
    """Resolve the per-user Codex plugin and marketplace file locations."""
    home = Path.home()
    return (home / ".codex" / "plugins" / PLUGIN_NAME,
            home / ".agents" / "plugins" / "marketplace.json")

def checked_path(path: Path, *, directory: bool = False) -> os.stat_result | None:
    """Accept only an existing plain file/directory or an absent final component."""
    try:
        info = path.lstat()
    except FileNotFoundError:
        return None
    expected = stat.S_ISDIR if directory else stat.S_ISREG
    if not expected(info.st_mode) or getattr(info, "st_file_attributes", 0) & stat.FILE_ATTRIBUTE_REPARSE_POINT:
        raise ValueError(f"Refusing a link or unexpected file type at {path}.")
    return info

def read_regular(path: Path) -> bytes:
    """Read at most one MiB without following a POSIX link or waiting on a FIFO."""
    if checked_path(path) is None:
        raise FileNotFoundError(path)
    flags = os.O_RDONLY | getattr(os, "O_NOFOLLOW", 0) | getattr(os, "O_NONBLOCK", 0) | getattr(os, "O_BINARY", 0)
    with os.fdopen(os.open(path, flags), "rb") as stream:
        if not stat.S_ISREG(os.fstat(stream.fileno()).st_mode):
            raise ValueError(f"Expected a regular file at {path}.")
        data = stream.read(1_048_577)
    if len(data) > 1_048_576:
        raise ValueError(f"The file exceeds the 1 MiB limit: {path}.")
    return data

def read_marketplace(path: Path) -> dict[str, object]:
    """Read the bounded local marketplace file or create an empty supported structure.

    Args:
        path: Local state or marketplace JSON file path.
    """
    if checked_path(path) is None:
        return {"name": "thermalright-vibeware-local", "plugins": []}
    data = json.loads(read_regular(path))
    if not isinstance(data, dict) or not isinstance(data.get("name"), str) \
            or not isinstance(data.get("plugins"), list):
        raise ValueError("Personal marketplace has an unsupported shape; no changes were made.")
    return data

def write_file(path: Path, data: bytes, *, backup_of: Path | None = None) -> None:
    """Publish a complete private sibling file; never overwrite an existing backup."""
    previous = checked_path(path)
    if backup_of is not None and previous is not None:
        return
    path.parent.mkdir(mode=0o700, parents=True, exist_ok=True)
    fd, temporary = tempfile.mkstemp(dir=path.parent)
    try:
        with os.fdopen(fd, "wb") as stream:
            source_info = checked_path(backup_of) if backup_of is not None else previous
            if os.name == "posix" and source_info is not None:
                os.fchmod(stream.fileno(), source_info.st_mode & 0o600)
            stream.write(data)
        if backup_of is None:
            os.replace(temporary, path)
        else:
            try:
                if os.name == "nt":
                    os.rename(temporary, path)  # Windows rename fails when the destination exists.
                else:
                    os.link(temporary, path)
            except FileExistsError:
                checked_path(path)
    finally:
        Path(temporary).unlink(missing_ok=True)

def json_bytes(value: dict[str, object]) -> bytes:
    """Encode the staged JSON as readable UTF-8 with a trailing newline."""
    return (json.dumps(value, indent=2, ensure_ascii=False) + "\n").encode("utf-8")

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
    runtime = Path(sys.executable).resolve().parent
    tag = f"python{sys.version_info.major}{sys.version_info.minor}"
    if not (runtime / f"{tag}.zip").is_file() or not (runtime / f"{tag}._pth").is_file():
        return False  # A normal Python install remains the user's own prerequisite.
    files = [item for item in runtime.iterdir() if item.is_file() and not item.is_symlink()
             and (item.suffix.lower() in {".exe", ".dll", ".pyd", ".zip", ".txt"} or item.name == f"{tag}._pth")]
    if len(files) > 128 or sum(item.stat().st_size for item in files) > 80 * 1024 * 1024:
        raise ValueError("The bundled Python runtime is unexpectedly large; no hook runtime was staged.")
    target = destination / "python"
    checked_path(target, directory=True)
    target.mkdir(exist_ok=True)
    for item in files:
        output = target / item.name
        if checked_path(output) is None or not item.samefile(output):
            shutil.copy2(item, output)
    write_file(target / f"{tag}._pth", f"{tag}.zip\n.\nimport site\n".encode("ascii"))
    if checked_path(target / "python.exe") is None:
        raise ValueError("The staged portable hook runtime is incomplete.")
    return True

def portable_hooks(data: bytes, destination: Path) -> bytes:
    """Point Windows hooks at their final portable interpreter using literal PowerShell paths."""
    interpreter = destination / "python" / "python.exe"
    script = destination / "hooks" / "thermalright-vibeware_hook.py"
    command = "& '{}' -B '{}'".format(str(interpreter).replace("'", "''"), str(script).replace("'", "''"))
    hooks = json.loads(data)
    for matchers in hooks.get("hooks", {}).values():
        for matcher in matchers:
            for hook in matcher.get("hooks", []):
                if hook.get("type") == "command":
                    hook["commandWindows"] = command
    return json_bytes(hooks)

def stage_plugin(source: Path, destination: Path) -> None:
    """Stage the bundled plugin and its portable Python runtime.

    Args:
        source: Bundled plugin source directory to copy.
        destination: Local plugin/runtime staging directory owned by this operation.
    """
    checked_path(source / "hooks", directory=True)
    files = {name: read_regular(source / name) for name in PLUGIN_FILES}
    existing = checked_path(destination, directory=True)
    if existing is not None:
        manifest = destination / "plugin.json"
        manifest_data = json.loads(read_regular(manifest)) if checked_path(manifest) else None
        if not isinstance(manifest_data, dict) or manifest_data.get("name") != PLUGIN_NAME:
            raise ValueError("The destination already contains another plugin; no changes were made.")
    destination.parent.mkdir(parents=True, mode=0o700, exist_ok=True)
    with tempfile.TemporaryDirectory(prefix=".thermalright-vibeware-", dir=destination.parent) as staging_root:
        staged = destination if existing is not None else Path(staging_root) / PLUGIN_NAME
        checked_path(staged / "hooks", directory=True)
        # Inspect every managed destination before refreshing any file. Keep unrelated files.
        previous = {name: read_regular(staged / name) if checked_path(staged / name) else None for name in files}
        (staged / "hooks").mkdir(mode=0o700, parents=True, exist_ok=True)
        if os.name == "nt":
            checked_path(staged / "python", directory=True)
            if stage_portable_python(staged):
                files["hooks/hooks.json"] = portable_hooks(files["hooks/hooks.json"], destination)
        for name, data in files.items():
            if previous[name] != data:
                write_file(staged / name, data)
        if existing is None:
            os.rename(staged, destination)

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
            print("Refreshed the staged plugin. Restart Codex, update the local plugin, then review and trust its updated hooks in Codex.")
            return
    stage_plugin(source, destination)
    plugins.append(plugin_entry())
    if marketplace_path.exists():
        backup = marketplace_path.with_name("marketplace.json.thermalright-vibeware.bak")
        write_file(backup, read_regular(marketplace_path), backup_of=marketplace_path)
    write_file(marketplace_path, json_bytes(marketplace))
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
    write_file(marketplace_path, json_bytes(marketplace))
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
