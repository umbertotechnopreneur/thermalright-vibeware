# VBWR B
#
# Project: Thermalright VibeWare
# Repository: https://github.com/umbertotechnopreneur/thermalright-vibeware
# Creator: Umberto Giacobbi | https://umbertogiacobbi.biz
#
# File: render.py
# Purpose: Composes the dispatch theme in the codex pack using shared drawing helpers.
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
    """Compose the dispatch theme using the shared Canvas API.

    Args:
        c: Shared Canvas facade; all coordinates use its logical theme space.
        s: Current ThemeContext containing available data, palette and page timing.
    """
    if c.compact:
        c.compact_codex("dispatch")
        return
    c.backdrop("dispatch")
    c.header("OPENAI CODEX / DISPATCH", extended=True)
    c.panel(40, 86, 748, 329, radius=22)
    c.label("CURRENT CHAT", 73, 125, 680)
    c.text(s.chat_title, 73, 184, min(36, 46 / s.font_scale), width=680, bold=True)
    c.text(s.status, 73, 259, min(43, 56 / s.font_scale), s.signal, 680, bold=True)
    c.text(s.activity_detail(), 73, 311, c.text_size("body"), s.muted, 680)
    c.label(s.model or s.phase or s.event, 73, 375, 680)
    c.panel(810, 86, 430, 153, radius=22)
    c.label("LATEST EVENT", 839, 124, 365)
    c.text(s.event or "—", 839, 174, min(23, 30 / s.font_scale), width=365)
    c.caption(s.tool, 839, 213, s.muted, 365)
    c.panel(810, 261, 430, 154, radius=22)
    c.label("RECEIVED ACTIVITY", 839, 300, 365)
    c.activity_rows(835, 335, 365, 64, 1)
    c.footer()
# VBWR E
