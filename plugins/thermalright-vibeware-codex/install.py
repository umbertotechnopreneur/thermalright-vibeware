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

def restrict_private_file(path: Path) -> None:
    """Remove shared permissions from an existing user-owned POSIX file."""
    if os.name != "posix" or checked_path(path) is None:
        return
    flags = os.O_RDONLY | os.O_NOFOLLOW | os.O_NONBLOCK
    fd = os.open(path, flags)
    try:
        info = os.fstat(fd)
        if not stat.S_ISREG(info.st_mode) or info.st_uid != os.geteuid() or info.st_nlink != 1:
            raise ValueError(f"Expected a private, user-owned regular file at {path}.")
        permissions = stat.S_IMODE(info.st_mode) & 0o600
        if stat.S_IMODE(info.st_mode) != permissions:
            os.fchmod(fd, permissions)
    finally:
        os.close(fd)

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

def portable_manifests(runtime: Path, *, require_complete: bool = True) -> list[Path]:
    """Check owned runtime files and their explicit, local-only import paths."""
    checked_path(runtime, directory=True)
    manifests = []
    count = 0
    total = 0
    for item in runtime.iterdir():
        count += 1
        info = checked_path(item)
        if info is None or info.st_nlink != 1:
            raise ValueError("The staged Python runtime contains missing or linked files.")
        total += info.st_size
        if count > 128 or total > 80 * 1024 * 1024:
            raise ValueError("The staged Python runtime exceeds its size limit.")
        if item.name.startswith("python") and item.suffix == "._pth":
            manifests.append(item)
        elif item.suffix.lower() not in {".exe", ".dll", ".pyd", ".zip", ".txt"}:
            raise ValueError(f"Unexpected file in the staged Python runtime: {item.name}.")
    if not manifests:
        raise ValueError("The staged Python runtime has no isolated path file. Prepare again from the current app package.")
    for manifest in manifests:
        tag = manifest.stem
        minor = tag.removeprefix("python3")
        if not tag.startswith("python3") or not minor.isascii() or not minor.isdigit() or int(minor) < 10:
            raise ValueError("The staged Python runtime has an unsupported version. Prepare again from the current app package.")
        if require_complete:
            for name in ("python.exe", f"{tag}.dll", f"{tag}.zip", manifest.name):
                info = checked_path(runtime / name)
                if info is None or info.st_nlink != 1 or info.st_size == 0:
                    raise ValueError("The staged Python runtime is incomplete or contains linked files. Prepare again from the current app package.")
        paths = read_regular(manifest).decode("ascii").splitlines()
        if paths not in ([f"{tag}.zip", "."], [f"{tag}.zip", ".", "import site"]):
            raise ValueError("The staged Python runtime has unexpected import paths. Prepare again from the current app package.")
    return manifests

def existing_portable_python(destination: Path) -> bool:
    """Retain only an unambiguous runtime in the plugin's fixed private folder."""
    runtime = destination / "python"
    if checked_path(runtime, directory=True) is None:
        return False
    manifests = portable_manifests(runtime)
    if len(manifests) != 1:
        raise ValueError("The staged Python runtime has several versions. Prepare again from the current app package.")
    manifest = manifests[0]
    write_file(manifest, f"{manifest.stem}.zip\n.\n".encode("ascii"))
    return True

def stage_portable_python(destination: Path) -> bool:
    """Keep the optional hook working after the portable Thermalright VibeWare ZIP is moved.

    Args:
        destination: Local plugin/runtime staging directory owned by this operation.
    """
    runtime = Path(sys.executable).resolve().parent
    tag = f"python{sys.version_info.major}{sys.version_info.minor}"
    if checked_path(runtime / f"{tag}._pth") is None:
        return existing_portable_python(destination)
    if checked_path(runtime / f"{tag}.zip") is None:
        raise ValueError("The current portable Python runtime is incomplete.")
    files = [item for item in runtime.iterdir() if item.is_file() and not item.is_symlink()
             and (item.suffix.lower() in {".exe", ".dll", ".pyd", ".zip", ".txt"} or item.name == f"{tag}._pth")]
    if len(files) > 128 or sum(item.stat().st_size for item in files) > 80 * 1024 * 1024:
        raise ValueError("The bundled Python runtime is unexpectedly large; no hook runtime was staged.")
    target = destination / "python"
    exists = checked_path(target, directory=True) is not None
    if exists:
        manifests = portable_manifests(target, require_complete=False)
        if any(int(item.stem.removeprefix("python3")) > sys.version_info.minor for item in manifests):
            raise ValueError("The staged Python version is newer than this package. Use a package with the same or a newer Python version.")
        if runtime.samefile(target):
            return existing_portable_python(destination)
    temporary = tempfile.mkdtemp(prefix=".python-", dir=destination)
    preserve_previous = False
    try:
        staged = Path(temporary) / "python"
        staged.mkdir()
        for item in files:
            shutil.copy2(item, staged / item.name)
        write_file(staged / f"{tag}._pth", f"{tag}.zip\n.\n".encode("ascii"))
        portable_manifests(staged)
        previous = Path(temporary) / "previous"
        if exists:
            os.rename(target, previous)
        try:
            os.rename(staged, target)
        except OSError:
            if exists:
                try:
                    os.rename(previous, target)
                except OSError as error:
                    preserve_previous = True
                    raise OSError(f"Could not restore the staged runtime. Its previous files remain at {previous}.") from error
            raise
    finally:
        if not preserve_previous:
            shutil.rmtree(temporary)
    return True

def portable_hooks(data: bytes, destination: Path) -> bytes:
    """Point Windows hooks at their final portable interpreter using literal PowerShell paths."""
    interpreter = destination / "python" / "python.exe"
    script = destination / "hooks" / "thermalright-vibeware_hook.py"
    command = "& '{}' -I -B '{}'".format(str(interpreter).replace("'", "''"), str(script).replace("'", "''"))
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
    backup = marketplace_path.with_name("marketplace.json.thermalright-vibeware.bak")
    restrict_private_file(marketplace_path)
    restrict_private_file(backup)
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
