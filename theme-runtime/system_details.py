# VBWR B
#
# Project: Thermalright VibeWare
# Repository: https://github.com/umbertotechnopreneur/thermalright-vibeware
# Creator: Umberto Giacobbi | https://umbertogiacobbi.biz
#
# File: system_details.py
# Purpose: Projects available sensor groups and rotating detail rows.
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

"""Small groups of real readings for System pages, without per-core noise."""
from __future__ import annotations

import re
from collections import defaultdict

from canvas import clean, optional_number

_INDIVIDUAL_CORE = re.compile(r"\b(?:core|thread)\s*#?\s*\d+\b", re.IGNORECASE)
_POWER_LIMIT = re.compile(r"\b(?:limit|maximum|minimum|tdp)\b", re.IGNORECASE)
_CORE_READING = re.compile(r"\b(?:cpu\s+)?core\s*#\s*(\d+)\b", re.IGNORECASE)
_UNITS = {"Voltage": "V", "Current": "A", "Power": "W", "Temperature": "°C", "Clock": "MHz", "Timing": "ns"}
HARDWARE_KINDS = ("Power", "Voltage", "Current", "Clock")

def core_heatmap_values(sensors, maximum=32):
    """Return matched physical-core power/load values; average SMT load rows.

    Args:
        sensors: Hardware sensor snapshot already provided by the engine.
        maximum: Upper setting bound, scale limit or item cap used by this helper.
    """
    cpu = sensors.get("cpu") or {}
    grouped = {"Power": defaultdict(list), "Load": defaultdict(list)}
    for item in (cpu.get("readings") or [])[:256]:
        if not isinstance(item, dict) or item.get("type") not in grouped:
            continue
        match = _CORE_READING.search(clean(item.get("name")))
        value = optional_number(item.get("value"))
        if match is None or value is None:
            continue
        core = int(match.group(1))
        if core < 1:
            continue
        kind = item["type"]
        grouped[kind][core].append(max(0, min(100, value)) if kind == "Load" else max(0, value))
    common = sorted(set(grouped["Power"]) & set(grouped["Load"]))[:maximum]
    return [(core,
             sum(grouped["Power"][core]) / len(grouped["Power"][core]),
             sum(grouped["Load"][core]) / len(grouped["Load"][core]))
            for core in common]

def reading_rows(raw, kinds, aggregate=False):
    """Project finite readings of allowed types, optionally omitting core/thread rows.

    Args:
        raw: Raw observation dictionary received from the engine.
        kinds: Allowed sensor types for this detail page.
        aggregate: Whether to omit individual core/thread channels.
    """
    groups = {kind: [] for kind in kinds}
    for item in (raw.get("readings") or [])[:256]:
        if not isinstance(item, dict) or item.get("type") not in kinds:
            continue
        name, kind = clean(item.get("name")), item["type"]
        value = optional_number(item.get("value"))
        if not name or value is None or (aggregate and _INDIVIDUAL_CORE.search(name)):
            continue
        groups[kind].append((name, value, _UNITS.get(kind, clean(item.get("unit")))))
    return [row for group in groups.values() for row in group]

def hardware_devices(sensors):
    """Enumerate available hardware groups with stable display keys.

    Args:
        sensors: Hardware sensor snapshot already provided by the engine.
    """
    candidates = []
    cpu = sensors.get("cpu")
    if isinstance(cpu, dict):
        candidates.append(("cpu", "CPU", cpu))
    gpus = [gpu for gpu in sensors.get("gpus", []) if isinstance(gpu, dict)]
    for index, gpu in enumerate(gpus):
        candidates.append((f"gpu-{index}", "GPU" if len(gpus) == 1 else f"GPU {index + 1}", gpu))
    board = sensors.get("motherboard")
    if isinstance(board, dict):
        candidates.append(("motherboard", "BOARD", board))
    return candidates

def sensor_groups(sensors, kinds):
    """Group available readings by hardware device and requested sensor types.

    Args:
        sensors: Hardware sensor snapshot already provided by the engine.
        kinds: Allowed sensor types for this detail page.
    """
    result = []
    for key, label, raw in hardware_devices(sensors):
        rows = reading_rows(raw, kinds, aggregate=key == "cpu")
        if "Temperature" in kinds and not rows:
            if key == "motherboard":
                rows = [(clean(t.get("name")), value, "°C") for t in raw.get("temperatures", [])
                        if isinstance(t, dict) and (value := optional_number(t.get("temperatureC"))) is not None]
            elif (value := optional_number(raw.get("temperatureC"))) is not None:
                rows = [(clean(raw.get("temperatureLabel")) or "Temperature", value, "°C")]
        if rows:
            result.append((key, label, clean(raw.get("name")), rows))
    return result

def cooling_values(sensors):
    """Select real fan and power readings for the cooling page.

    Args:
        sensors: Hardware sensor snapshot already provided by the engine.
    """
    thermal, powers, electrical, fans = [], [], [], []
    for key, label, raw in hardware_devices(sensors):
        if optional_number(raw.get("temperatureC")) is not None:
            thermal.append((key, label, raw))
        prefix = "CPU " if key == "cpu" else "GPU " if key.startswith("gpu-") else ""
        for name, value, unit in reading_rows(raw, ("Power",), aggregate=key == "cpu"):
            if not _POWER_LIMIT.search(name):
                powers.append((label + " · " + name.removeprefix(prefix), value, unit))
        if key == "cpu":
            for name, value, unit in reading_rows(raw, ("Voltage", "Current"), aggregate=True):
                electrical.append((label + " · " + name.removeprefix(prefix), value, unit))
        for fan in raw.get("fans") or []:
            if isinstance(fan, dict) and (value := optional_number(fan.get("rpm"))) is not None:
                fans.append((label + " · " + clean(fan.get("name")).removeprefix(prefix), value, "RPM"))
    def power_priority(row):
        """Rank CPU/package power rows ahead of other available power readings.

        Args:
            row: Projected reading row being ranked for display.
        """
        label = row[0].casefold()
        if label in ("cpu · package", "cpu · cpu package"):
            return 0
        if label.startswith("cpu · "):
            return 1
        return 2

    powers.sort(key=power_priority)
    # Keep electrical values after actual consumption; they retain their own units.
    powers.extend(electrical)
    # A measured stop remains visible after the engine has observed this fan running.
    fans.sort(key=lambda item: item[1] == 0)
    return thermal, powers, fans

def spd_groups(sensors):
    """Project separately labeled SPD module limits and identities.

    Args:
        sensors: Hardware sensor snapshot already provided by the engine.
    """
    memory = sensors.get("memory") or {}
    modules = [module for module in (memory.get("spdModules") or [])[:16] if isinstance(module, dict)]
    groups = []
    for index, module in enumerate(modules):
        rows = reading_rows(module, ("Timing",))
        if rows:
            # Preserve the SPD source name; its SMBus index does not identify a physical slot.
            groups.append((f"memory-spd-{index}", f"SPD LIMITS · SOURCE {index + 1}", clean(module.get("name")), rows))
    return groups

def page_available(page_id, sensors):
    """Decide whether a System page has enough available source data to show.

    Args:
        page_id: Stable manifest page identifier whose source availability is checked.
        sensors: Hardware sensor snapshot already provided by the engine.
    """
    kinds = {"electrical": HARDWARE_KINDS, "temperatures": ("Temperature",)}
    if page_id in kinds:
        return bool(sensor_groups(sensors, kinds[page_id]))
    if page_id == "memory":
        return bool(memory_sections(sensors))
    if page_id == "cooling":
        return any(cooling_values(sensors))
    if page_id == "cores":
        return len(core_heatmap_values(sensors)) >= 8
    return True

def value_text(value, unit):
    """Format an available reading with the correct unit precision.

    Args:
        value: Available measured reading, or None for the unavailable marker.
        unit: Reading unit shown alongside the available value.
    """
    digits = 3 if unit == "V" else 2 if unit == "ns" else 1 if unit in ("A", "°C", "GiB") else 0
    return f"{value:.{digits}f} {unit}"

def detail_step(s):
    # At most two groups per visit, with no new timer or sampling request.
    """Read the shared theme page cycle used for detail rotation.

    Args:
        s: Current ThemeContext containing available data, palette and page timing.
    """
    return s.page_cycle * 2 + min(1, int(s.page_progress * 2))

def detail_rows(rows, step, maximum=4):
    """Balance the last bank instead of leaving a lone trailing reading.

    Args:
        rows: Available projected reading rows, preserving their source units.
        step: Shared detail-rotation index; None uses the context's current page clock.
        maximum: Upper setting bound, scale limit or item cap used by this helper.
    """
    if not rows:
        return [], 0
    banks = (len(rows) + maximum - 1) // maximum
    bank = step % banks
    size, extra = divmod(len(rows), banks)
    start = bank * size + min(bank, extra)
    return rows[start:start + size + (bank < extra)], start

def memory_sections(sensors):
    """Separate Windows RAM identities from independently reported SPD groups.

    Args:
        sensors: Hardware sensor snapshot already provided by the engine.
    """
    memory = sensors.get("memory") or {}
    sections = []
    if (any(isinstance(module, dict) and module for module in memory.get("modules") or [])
            or clean(memory.get("type"))
            or any(optional_number(memory.get(key)) is not None
                   for key in ("usedGiB", "totalGiB", "configuredSpeedMTs", "clockMHz"))):
        sections.append("modules")
    if spd_groups(sensors):
        sections.append("spd")
    return sections

def memory_view(sensors, step):
    """Select rotating physical RAM and SPD sections without guessing slot matches.

    Args:
        sensors: Hardware sensor snapshot already provided by the engine.
        step: Shared detail-rotation index; None uses the context's current page clock.
    """
    sections = memory_sections(sensors)
    if not sections:
        return None, 0
    # Each section advances separately, so alternating sections never skip a bank.
    return sections[step % len(sections)], step // len(sections)
# VBWR E
