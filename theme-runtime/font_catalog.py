# VBWR B
#
# Project: Thermalright VibeWare
# Repository: https://github.com/umbertotechnopreneur/thermalright-vibeware
# Creator: Umberto Giacobbi | https://umbertogiacobbi.biz
#
# File: font_catalog.py
# Purpose: Lists renderer font families and resolves monospace fallback choices.
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

"""A cached, bounded catalog from the renderer's actual system font backend.

API references:
https://skia-python.github.io/skia-python/reference/skia.FontMgr.html
https://skia-python.github.io/skia-python/reference/skia.FontStyleSet.html
https://skia-python.github.io/skia-python/reference/skia.Typeface.html
"""
from __future__ import annotations

from functools import lru_cache

import skia

_MAX_INSPECTED_FAMILIES = 4096
_MAX_RETURNED_FAMILIES = 512
_MAX_BACKEND_ERRORS = 32

@lru_cache(maxsize=1)
def _catalog() -> tuple[tuple[str, ...], str | None]:
    """Inspect a bounded installed-font catalog and cache fixed-pitch families."""
    try:
        manager = skia.FontMgr.RefDefault()
        if manager is None:
            return (), "The renderer's system font backend is unavailable."
        total = max(0, int(manager.countFamilies()))
        normal, bold = skia.FontStyle.Normal(), skia.FontStyle.Bold()
    except Exception:
        # Keep implementation details and local paths out of the public notice.
        return (), "The renderer could not read the installed font catalog."

    names: dict[str, str] = {}
    errors = invalid_names = 0
    stopped_early = False
    limit = min(total, _MAX_INSPECTED_FAMILIES)
    for index in range(limit):
        try:
            name = manager.getFamilyName(index)
            if not isinstance(name, str):
                invalid_names += 1
                continue
            # Preserve the backend's exact family spelling for later selection.
            if not name or name != name.strip() or len(name) > 128 or not name.isprintable():
                invalid_names += 1
                continue
            if name.casefold() in names:
                continue
            # Match inside this enumerated family, never a fallback lookup by name.
            styles = manager.createStyleSet(index)
            if styles is None or styles.count() == 0:
                continue
            regular_face = styles.matchStyle(normal)
            if regular_face is None or not regular_face.isFixedPitch():
                continue
            bold_face = styles.matchStyle(bold)
            if bold_face is None or not bold_face.isFixedPitch():
                continue
            names[name.casefold()] = name
        except Exception:
            errors += 1
            if errors >= _MAX_BACKEND_ERRORS:
                stopped_early = True
                break

    ordered = sorted(names.values(), key=lambda name: (name.casefold(), name))
    notes = []
    if total > limit:
        notes.append(f"Font discovery inspected the first {limit} installed families.")
    if stopped_early:
        notes.append("Font discovery stopped after repeated backend errors.")
    elif errors:
        notes.append("Some installed families could not be inspected.")
    if invalid_names:
        notes.append("Some font names could not be shown in this catalog.")
    if len(ordered) > _MAX_RETURNED_FAMILIES:
        ordered = ordered[:_MAX_RETURNED_FAMILIES]
        notes.append(f"The list is limited to {_MAX_RETURNED_FAMILIES} monospace families.")
    if not ordered:
        notes.append("No installed family was reported as fixed-pitch for normal and bold text.")
    return tuple(ordered), " ".join(notes) or None

def list_monospace_fonts() -> dict:
    """List installed fixed-pitch families once per worker process.

    This uses the font's native isFixedPitch metadata, not a name containing
    'Mono'. It makes no directory scans or downloads. Restart the worker after
    installing fonts. Metadata is not a guarantee of equal width for every glyph.
    """
    families, notice = _catalog()
    # Fresh containers prevent an RPC caller from changing the cached result.
    return {"families": list(families), "notice": notice}
# VBWR E
