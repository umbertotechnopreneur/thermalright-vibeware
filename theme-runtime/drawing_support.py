# VBWR B
#
# Project: Thermalright VibeWare
# Repository: https://github.com/umbertotechnopreneur/thermalright-vibeware
# Creator: Umberto Giacobbi | https://umbertogiacobbi.biz
#
# File: drawing_support.py
# Purpose: Supplies shared colors, finite-value conversions and bounded drawing-layer caching.
#
# VibeWare: Human intent, AI execution, and plenty of tokens
# Manifesto: https://umbertogiacobbi.biz/vibeware/manifesto
#
# Created with AI: OpenAI Codex; extracted existing first-party source; 2026-10-27.
# Modified with AI: OpenAI Codex; source documentation and organization; 2026-10-27.
# Human guidance: Umberto Giacobbi; maintainability and standard file headers.
#
# Copyright (c) 2026 Umberto Giacobbi
# SPDX-License-Identifier: MIT
#
# VBWR E

"""Shared value conversions, colors and bounded drawing cache; no theme state."""
from __future__ import annotations

import math
from collections import OrderedDict
import skia

_CACHE = OrderedDict()
FALLBACK_PALETTE = ("#6677CC", "#A575BC", "#4AABB1", "#C8A48C")
SAMPLING = skia.SamplingOptions(skia.FilterMode.kLinear)

def cached(key, factory):
    """Return a shared drawing layer and evict the oldest entries above the cache limit.

    Args:
        key: Stable cache, metric or data identity used by this helper.
        factory: Callback creating a drawing layer only on a cache miss.
    """
    if key in _CACHE:
        _CACHE.move_to_end(key)
        return _CACHE[key]
    value = factory()
    _CACHE[key] = value
    while len(_CACHE) > 12:
        _CACHE.popitem(last=False)
    return value

def rgb(value):
    """Parse a six-digit RGB color, using the shared fallback for invalid input.

    Args:
        value: RGB hex text, optionally prefixed with #; invalid text uses the shared fallback color.
    """
    value = str(value).lstrip("#")
    if len(value) != 6:
        return (102, 119, 204)
    try:
        return tuple(int(value[i:i + 2], 16) for i in (0, 2, 4))
    except ValueError:
        return (102, 119, 204)

def color(value, opacity=1):
    """Convert an RGB color and opacity into a native Skia color.

    Args:
        value: RGB hex text converted to a Skia color before opacity is applied.
        opacity: Alpha fraction from transparent zero to opaque one.
    """
    red, green, blue = rgb(value)
    return skia.ColorSetARGB(round(max(0, min(1, opacity)) * 255), red, green, blue)

def blend(a, b, amount):
    """Interpolate two RGB colors by the supplied fraction.

    Args:
        a: Starting RGB color for interpolation.
        b: Ending RGB color for interpolation.
        amount: Interpolation/fade fraction, with zero retaining the original value.
    """
    return "#" + "".join(f"{round(x + (y - x) * amount):02X}" for x, y in zip(rgb(a), rgb(b)))

def number(value, default=0):
    """Convert a finite numeric value or return the supplied default.

    Args:
        value: Numeric input to convert; invalid or non-finite input uses default.
        default: Value used when numeric conversion is invalid or non-finite.
    """
    try:
        value = float(value)
        return value if math.isfinite(value) else default
    except (ValueError, TypeError):
        return default

def clip(value, low=0, high=1):
    """Clamp a numeric input between the supplied bounds.

    Args:
        value: Numeric input to clamp; failed conversion uses zero before clamping.
        low: Lower bound of the displayed range.
        high: Upper bound of the displayed range.
    """
    return max(low, min(high, number(value)))

def progress_color(tint, fraction, light=False):
    """Keep the theme's low end, then move through amber to red at full scale.

    Args:
        tint: RGB hex color used by this drawing.
        fraction: Normalized progress; zero starts at the accent and one reaches red.
        light: Whether the current theme uses a light appearance.
    """
    amount = clip(fraction)
    warm = blend(tint, "#AE6A16" if light else "#F4B557", .8)
    red = "#C73542" if light else "#FF5963"
    return blend(tint, warm, amount / .6) if amount <= .6 else blend(warm, red, (amount - .6) / .4)

def clean(value, fallback=""):
    """Normalize text whitespace and bound its displayed length.

    Args:
        value: Text to normalize; empty input uses fallback before the display-length cap.
        fallback: Text used when the source value is absent.
    """
    return " ".join(str(value or fallback).split())[:300]

def duration(seconds):
    """Format nonnegative playback time as m:ss, including hours when needed.

    Args:
        seconds: Playback time in seconds.
    """
    value = max(0, int(number(seconds)))
    if value >= 3600:
        return f"{value // 3600}:{value // 60 % 60:02}:{value % 60:02}"
    return f"{value // 60}:{value % 60:02}"

def optional_number(value):
    """Preserve an available finite numeric reading; otherwise return None.

    Args:
        value: Optional measured number; None, booleans and non-finite input are unavailable.
    """
    if value is None or isinstance(value, bool):
        return None
    try:
        result = float(value)
        return result if math.isfinite(result) else None
    except (TypeError, ValueError):
        return None

def dbfs(value):
    """Validate a measured dBFS value against the renderer's display floor.

    Args:
        value: Optional measured dBFS level, clamped to the -120..0 display range.
    """
    result = optional_number(value)
    return max(-120, min(0, result)) if result is not None else None

def db_label(value):
    """Format an available dBFS reading or show its unavailable marker.

    Args:
        value: Optional dBFS reading; None displays an unavailable marker.
    """
    return "—" if value is None else f"{value:.1f}"
