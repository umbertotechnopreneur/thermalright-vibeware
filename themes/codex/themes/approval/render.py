# VBWR B
#
# Project: Thermalright VibeWare
# Repository: https://github.com/umbertotechnopreneur/thermalright-vibeware
# Creator: Umberto Giacobbi | https://umbertogiacobbi.biz
#
# File: render.py
# Purpose: Composes the approval theme in the codex pack using shared drawing helpers.
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
    """Compose the approval theme using the shared Canvas API.

    Args:
        c: Shared Canvas facade; all coordinates use its logical theme space.
        s: Current ThemeContext containing available data, palette and page timing.
    """
    if c.compact:
        c.compact_codex("approval")
        return
    c.backdrop("approval")
    c.header("OPENAI CODEX / APPROVAL", extended=True)
    c.panel(134, 92, 1008, 302, radius=25, opacity=.42 if not s.light else .64)
    c.rect(134, 92, 1008, 302, s.signal, 25, .46 if s.approval else .18, 1)
    c.glow(277, 244, 137, s.signal, .32 if s.approval or s.ready else .12)
    c.status_symbol(277, 244, 77)
    c.label(s.chat_title, 420, 139, 678)
    c.text(s.status, 420, 213, min(43, 58 / s.font_scale), s.signal, 678, bold=True)
    c.text(s.activity_detail(), 420, 285, min(35, 45 / s.font_scale), width=678)
    c.label(s.tool or s.event or "WAITING FOR EVENTS", 420, 356, 678)
    c.footer()
# VBWR E
