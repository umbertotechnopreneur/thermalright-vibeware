# VBWR B
#
# Project: Thermalright VibeWare
# Repository: https://github.com/umbertotechnopreneur/thermalright-vibeware
# Creator: Umberto Giacobbi | https://umbertogiacobbi.biz
#
# File: render.py
# Purpose: Composes the chronicle theme in the codex pack using shared drawing helpers.
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
    """Compose the chronicle theme using the shared Canvas API.

    Args:
        c: Shared Canvas facade; all coordinates use its logical theme space.
        s: Current ThemeContext containing available data, palette and page timing.
    """
    if c.compact:
        c.compact_codex("chronicle")
        return
    c.backdrop("chronicle")
    c.header("OPENAI CODEX / CHRONICLE", extended=True)
    c.text(s.now.strftime("%H:%M"), 55, 203, min(93, 116 / s.font_scale), s.accent, 480, bold=True)
    c.text(s.chat_title, 60, 273, min(28, 36 / s.font_scale), width=485)
    c.text(s.status, 60, 325, min(29, 36 / s.font_scale), s.signal, 485)
    c.caption(s.activity_detail(), 60, 367, s.muted, 485)
    c.line(591, 93, 591, 396, s.fg, opacity=.14)
    c.label("ACTIVITY JOURNAL", 635, 111, 580)
    c.activity_rows(632, 147, 580, 64, 4)
    c.footer()
# VBWR E
