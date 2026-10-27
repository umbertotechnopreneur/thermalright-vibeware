# VBWR B
#
# Project: Thermalright VibeWare
# Repository: https://github.com/umbertotechnopreneur/thermalright-vibeware
# Creator: Umberto Giacobbi | https://umbertogiacobbi.biz
#
# File: render.py
# Purpose: Composes the quiet theme in the codex pack using shared drawing helpers.
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
    """Compose the quiet theme using the shared Canvas API.

    Args:
        c: Shared Canvas facade; all coordinates use its logical theme space.
        s: Current ThemeContext containing available data, palette and page timing.
    """
    if c.compact:
        c.compact_codex("quiet")
        return
    c.backdrop("quiet")
    c.header("OPENAI CODEX / QUIET", extended=True)
    c.status_symbol(147, 246, 46)
    c.text(s.status, 245, 228, min(56, 72 / s.font_scale), s.signal, 939, bold=True)
    c.text(s.chat_title, 245, 288, min(27, 34 / s.font_scale), width=939)
    c.text(s.activity_detail(), 245, 336, c.text_size("body"), s.muted, 939)
    c.line(245, 371, 344, 371, s.signal, 2, .5)
    c.footer()
# VBWR E
