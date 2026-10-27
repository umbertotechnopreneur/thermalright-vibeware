# VBWR B
#
# Project: Thermalright VibeWare
# Repository: https://github.com/umbertotechnopreneur/thermalright-vibeware
# Creator: Umberto Giacobbi | https://umbertogiacobbi.biz
#
# File: render.py
# Purpose: Composes the timeline theme in the codex pack using shared drawing helpers.
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
    """Compose the timeline theme using the shared Canvas API.

    Args:
        c: Shared Canvas facade; all coordinates use its logical theme space.
        s: Current ThemeContext containing available data, palette and page timing.
    """
    if c.compact:
        c.compact_codex("timeline")
        return
    c.backdrop("timeline")
    c.header("OPENAI CODEX / TIMELINE", extended=True)
    c.label("CHAT", 56, 111, 610)
    c.text(s.chat_title, 56, 175, min(43, 52 / s.font_scale), width=610, bold=True)
    c.label("CURRENT STATUS", 56, 249, 610)
    c.text(s.status, 56, 307, min(36, 46 / s.font_scale), s.signal, 610)
    c.text(s.activity_detail(), 56, 354, c.text_size("body"), s.muted, 610)
    c.line(721, 95, 721, 392, s.fg, opacity=.12)
    c.activity_rows(772, 116, 448, 72, 4)
    c.line(40, 420, 1240, 420, s.fg, opacity=.12)
    c.footer()
# VBWR E
