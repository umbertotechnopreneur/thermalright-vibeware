# VBWR B
#
# Project: Thermalright VibeWare
# Repository: https://github.com/umbertotechnopreneur/thermalright-vibeware
# Creator: Umberto Giacobbi | https://umbertogiacobbi.biz
#
# File: palette.py
# Purpose: Resolves validated semantic palettes from defaults, manifests and overrides.
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

"""Semantic palette v1. Numeric series remain compatible with existing themes."""
import re

NAMES = ("textPrimary", "textSecondary", "accent", "warning", "info")

def validate(values):
    """Validate semantic palette override names and hexadecimal color values.

    Args:
        values: Current measured values or semantic palette fields.
    """
    if not isinstance(values, dict) or any(key not in NAMES for key in values):
        raise ValueError("Palette must contain only supported color names.")
    for value in values.values():
        if value is not None and (not isinstance(value, str) or not re.fullmatch(r"#[0-9A-Fa-f]{6}", value)):
            raise ValueError("Palette colors must be #RRGGBB or null to inherit.")
    return {key: value.upper() for key, value in values.items() if value is not None}

class ThemePalette(tuple):
    def __new__(cls, series, defaults, manifest, overrides, light):
        """Resolve positional colors and semantic palette overrides for the current appearance.

        Args:
            series: Positional theme colors retained alongside the semantic palette fields.
            defaults: Semantic palette defaults derived from the current theme identity.
            manifest: Selected theme's parsed manifest and optional page/palette metadata.
            overrides: Optional per-theme semantic colors supplied by the studio.
            light: Whether the current theme uses a light appearance.
        """
        if manifest.get("paletteVersion", 1) != 1:
            raise ValueError("Unsupported palette version.")
        definition = manifest.get("palette", {})
        if not isinstance(definition, dict) or any(k not in (*NAMES, "light", "dark") for k in definition):
            raise ValueError("Invalid theme palette.")
        values = dict(defaults)
        declared = validate({k: v for k, v in definition.items() if k in NAMES})
        declared.update(validate(definition.get("light" if light else "dark", {})))
        declared.update(validate({} if overrides is None else overrides))
        values.update(declared)
        if "accent" in declared:
            series = (values["accent"], *series[1:])
        result = super().__new__(cls, series)
        result.overrides = declared
        for name in NAMES:
            setattr(result, name, values[name])
        return result

    def to_json(self):
        """Return the semantic palette fields used in worker responses and cache keys."""
        return {name: getattr(self, name) for name in NAMES}

# VBWR E
