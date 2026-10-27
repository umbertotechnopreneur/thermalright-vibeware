# VBWR B
#
# Project: Thermalright VibeWare
# Repository: https://github.com/umbertotechnopreneur/thermalright-vibeware
# Creator: Umberto Giacobbi | https://umbertogiacobbi.biz
#
# File: render.py
# Purpose: Composes the focus theme in the codex pack using shared drawing helpers.
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
    """Compose the focus theme using the shared Canvas API.

    Args:
        c: Shared Canvas facade; all coordinates use its logical theme space.
        s: Current ThemeContext containing available data, palette and page timing.
    """
    if c.compact:
        c.compact_codex("focus")
        return
    c.backdrop("focus")
    c.header("OPENAI CODEX / FOCUS", extended=True)
    c.caption(s.chat_title.upper(), 640, 142, s.accent, 1080, align="center")
    c.text(s.status, 640, 251, min(70, 86 / s.font_scale), s.signal, 1160, align="center", bold=True)
    c.text(s.activity_detail(), 640, 316, min(24, 32 / s.font_scale), s.muted, 1120, align="center")
    c.line(572, 368, 708, 368, s.signal, 3, .6)
    c.caption(s.tool or s.event or "No activity received yet", 640, 408, s.muted, 1120, align="center")
    c.footer()
# VBWR E
