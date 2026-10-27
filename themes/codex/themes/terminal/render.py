# VBWR B
#
# Project: Thermalright VibeWare
# Repository: https://github.com/umbertotechnopreneur/thermalright-vibeware
# Creator: Umberto Giacobbi | https://umbertogiacobbi.biz
#
# File: render.py
# Purpose: Composes the terminal theme in the codex pack using shared drawing helpers.
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
    """Compose the terminal theme using the shared Canvas API.

    Args:
        c: Shared Canvas facade; all coordinates use its logical theme space.
        s: Current ThemeContext containing available data, palette and page timing.
    """
    if c.compact:
        c.compact_codex("terminal")
        return
    c.backdrop("terminal")
    c.header("OPENAI CODEX / TERMINAL", extended=True)
    c.panel(42, 82, 1196, 337, radius=12)
    c.circle(67, 107, 4, s.palette[1])
    c.circle(84, 107, 4, s.palette[3])
    c.circle(101, 107, 4, s.palette[2])
    c.label(s.chat_title, 131, 112, 1035)
    c.line(43, 130, 1237, 130, s.fg, opacity=.1)
    c.text("> " + s.status, 68, 188, min(31, 42 / s.font_scale), s.signal, 1144, bold=True)
    c.label("LATEST EVENT", 68, 236, 200)
    c.text(s.event or "—", 294, 237, c.text_size("body"), width=880)
    c.label("TOOL", 68, 279, 200)
    c.text(s.tool or "—", 294, 280, c.text_size("body"), width=880)
    c.label("MODEL", 68, 322, 200)
    c.text(s.model or "—", 294, 323, c.text_size("body"), width=880)
    c.caption(s.activity_detail(), 68, 391, s.muted, 1140)
    c.footer()
# VBWR E
