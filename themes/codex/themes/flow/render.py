# VBWR B
#
# Project: Thermalright VibeWare
# Repository: https://github.com/umbertotechnopreneur/thermalright-vibeware
# Creator: Umberto Giacobbi | https://umbertogiacobbi.biz
#
# File: render.py
# Purpose: Composes the flow theme in the codex pack using shared drawing helpers.
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
    """Compose the flow theme using the shared Canvas API.

    Args:
        c: Shared Canvas facade; all coordinates use its logical theme space.
        s: Current ThemeContext containing available data, palette and page timing.
    """
    if c.compact:
        c.compact_codex("flow")
        return
    c.backdrop("flow")
    c.header("OPENAI CODEX / FLOW", extended=True)
    c.text(s.chat_title, 40, 104, min(24, 32 / s.font_scale), width=1180, bold=True)
    events = s.events()
    for index in range(4):
        x = 160 + index * 320
        event = events[index] if index < len(events) else None
        current = event is not None and index == len(events) - 1 and not s.stale and bool(s.event)
        shade = s.signal if current and (s.approval or s.ready) else s.palette[1] if current and s.is_live else s.palette[0]
        if index < 3:
            c.line(x + 53, 205, x + 267, 205, s.muted, 1, .45)
        if event:
            if current:
                c.glow(x, 205, 78, shade, .35)
            c.circle(x, 205, 41, shade, .9 if current else .74)
            c.circle(x, 205, 21, s.fg, .6, 2)
            c.circle(x, 205, 7, s.fg, .95)
            c.text(event.get("status") or event.get("event"), x, 279, min(21, 28 / s.font_scale), width=280, align="center", bold=True)
            c.caption(event.get("tool") or event.get("event"), x, 318, s.muted, 278, align="center")
        else:
            c.circle(x, 205, 41, s.muted, .45, 1.5)
            c.line(x - 10, 205, x + 10, 205, s.muted, 3, .6)
            c.text("No event yet", x, 279, c.text_size("body"), s.muted, 280, align="center")
    c.label("CURRENT STATUS", 40, 360, 1120)
    c.text(s.status, 40, 418, min(38, 46 / s.font_scale), s.signal, 1160, bold=True)
    c.footer(s.event or "WAITING FOR CODEX")
# VBWR E
