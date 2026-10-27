# VBWR B
#
# Project: Thermalright VibeWare
# Repository: https://github.com/umbertotechnopreneur/thermalright-vibeware
# Creator: Umberto Giacobbi | https://umbertogiacobbi.biz
#
# File: render.py
# Purpose: Composes the beacon theme in the codex pack using shared drawing helpers.
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

def render(c, s):
    """Compose the beacon theme using the shared Canvas API.

    Args:
        c: Shared Canvas facade; all coordinates use its logical theme space.
        s: Current ThemeContext containing available data, palette and page timing.
    """
    if c.compact:
        c.compact_codex("beacon")
        return
    c.backdrop("beacon")
    c.header("OPENAI CODEX / BEACON", extended=True)
    c.status_symbol(194, 242, 104)
    c.label(s.chat_title, 365, 139, 832, s.accent)
    c.text(s.status, 365, 229, min(58, 74 / s.font_scale), s.signal, 830, bold=True)
    c.text(s.activity_detail(), 365, 291, min(24, 32 / s.font_scale), s.muted, 830)
    c.line(365, 338, 1190, 338, s.fg, opacity=.12)
    c.caption(s.model or s.phase, 365, 376, s.muted, 830)
    c.footer("DEMO DATA · PREVIEW" if s.preview else "THERMALRIGHT VIBEWARE · OPENAI CODEX")
# VBWR E
