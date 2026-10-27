# VBWR B
#
# Project: Thermalright VibeWare
# Repository: https://github.com/umbertotechnopreneur/thermalright-vibeware
# Creator: Umberto Giacobbi | https://umbertogiacobbi.biz
#
# File: render.py
# Purpose: Composes the signal theme in the media pack using shared drawing helpers.
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
    """Compose the signal theme using the shared Canvas API.

    Args:
        c: Shared Canvas facade; all coordinates use its logical theme space.
        s: Current ThemeContext containing available data, palette and page timing.
    """
    if s.page_id == "artist":
        from artist_page import render as render_artist
        render_artist(c, s)
        return
    if c.compact:
        c.compact_media("signal")
        return
    c.backdrop("signal")
    c.header("MEDIA / SIGNAL")
    c.cover(40, 78, 156, radius=8)
    c.label(s.source, 226, 91, 750, s.accent)
    c.text(s.title, 226, 147, min(39, 51 / s.font_scale), width=750, bold=True)
    c.text(s.artist, 226, 184, min(23, 30 / s.font_scale), s.muted, 750)
    c.caption(s.album, 226, 217, width=600)
    c.playback_indicators(1060, 82, 40)
    c.app_volume(991, 215, 220)
    c.panel(40, 251, 1200, 174, radius=18)
    c.spectrum(64, 266, 548, 150)
    c.line(638, 268, 638, 407, s.fg, opacity=.12)
    c.stereo_vu(664, 269, 548, 146)
    c.progress(40, 434, 1200, 4, labels=False)
    c.caption(s.timing(), 1240, 470, width=340, align="right")
    c.footer(width=800)
# VBWR E
