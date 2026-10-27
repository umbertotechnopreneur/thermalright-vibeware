# VBWR B
#
# Project: Thermalright VibeWare
# Repository: https://github.com/umbertotechnopreneur/thermalright-vibeware
# Creator: Umberto Giacobbi | https://umbertogiacobbi.biz
#
# File: detail_history.py
# Purpose: Retains bounded per-reading chart history for rotating detail pages.
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

"""Bounded history of the readings already delivered to System themes."""
from __future__ import annotations

import time
from collections import deque
from itertools import zip_longest

from canvas import clean, optional_number
from system_details import HARDWARE_KINDS, hardware_devices, sensor_groups

_HISTORY = {}
_MAX_SERIES = 256
_MAX_SAMPLES = 60
_TREND_UNITS = {"Power": "W", "Voltage": "V", "Current": "A", "Clock": "MHz", "Temperature": "°C"}
_last_stamp = ""
_last_at = 0.0

def keyed_rows(group, sensors):
    """Pair visible detail readings with unambiguous history identities.

    Args:
        group: Projected hardware group with its stable key, device identity and rows.
        sensors: Hardware sensor snapshot already provided by the engine.
    """
    key, _, device, rows = group
    raw = next((raw for device_key, _, raw in hardware_devices(sensors) if device_key == key), {})
    identifiers, raw_counts = {}, {}
    for item in (raw.get("readings") or [])[:256]:
        if not isinstance(item, dict):
            continue
        name, unit = clean(item.get("name")), _TREND_UNITS.get(item.get("type"))
        if name and unit:
            identity = (name, unit)
            raw_counts[identity] = raw_counts.get(identity, 0) + 1
            if optional_number(item.get("value")) is not None:
                identifiers.setdefault(identity, []).append(clean(item.get("id")))
    totals = {}
    for name, _, unit in rows:
        totals[(name, unit)] = totals.get((name, unit), 0) + 1
    occurrences = {}
    for name, value, unit in rows:
        identity = (name, unit)
        ordinal = occurrences.get(identity, 0)
        occurrences[identity] = ordinal + 1
        ids = identifiers.get(identity, ())
        sensor_id = ids[ordinal] if ordinal < len(ids) else ""
        # Without an ID, ambiguous duplicate names must not share or swap traces.
        unique = max(totals[identity], raw_counts.get(identity, 0)) == 1
        history_key = (key, device, name, unit, "id", sensor_id) if sensor_id else (
            (key, device, name, unit, "name", "") if unique else None)
        yield history_key, value

def record(sensors):
    """Record new sensor observations in bounded per-reading history.

    Args:
        sensors: Hardware sensor snapshot already provided by the engine.
    """
    global _last_stamp, _last_at
    stamp = clean(sensors.get("updatedAtUtc"))
    now = time.monotonic()
    if not stamp or now - _last_at > 10:
        _HISTORY.clear()
    if not stamp:
        _last_stamp = ""
        return
    if stamp == _last_stamp:
        return
    _last_stamp, _last_at = stamp, now
    groups = (sensor_groups(sensors, HARDWARE_KINDS), sensor_groups(sensors, ("Temperature",)))
    banks = [[sample for group in bank for sample in keyed_rows(group, sensors) if sample[0] is not None]
             for bank in groups]
    samples = [sample for pair in zip_longest(*banks) for sample in pair if sample is not None][:_MAX_SERIES]
    active = {key for key, _ in samples}
    for key in tuple(_HISTORY):
        if key not in active:
            del _HISTORY[key]
    for key, value in samples:
        _HISTORY.setdefault(key, deque(maxlen=_MAX_SAMPLES)).append((now, value))

def points(key):
    """Return a copy of a detail reading's available chart points.

    Args:
        key: Stable cache, metric or data identity used by this helper.
    """
    return list(_HISTORY.get(key, ()))
# VBWR E
