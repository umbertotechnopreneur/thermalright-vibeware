# VBWR B
#
# Project: Thermalright VibeWare
# Repository: https://github.com/umbertotechnopreneur/thermalright-vibeware
# Creator: Umberto Giacobbi | https://umbertogiacobbi.biz
#
# File: render.py
# Purpose: Composes the editorial theme in the media pack using shared drawing helpers.
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
    """Compose the editorial theme using the shared Canvas API.

    Args:
        c: Shared Canvas facade; all coordinates use its logical theme space.
        s: Current ThemeContext containing available data, palette and page timing.
    """
    if s.page_id == "artist":
        from artist_page import render as render_artist
        render_artist(c, s)
        return
    if c.compact:
        c.compact_media("editorial")
        return
    c.backdrop("editorial")
    c.header("MEDIA / EDITORIAL")
    c.rect(893, 95, 290, 290, s.palette[1], opacity=.45)
    c.cover(877, 79, 290, radius=1)
    c.media_text(56, 161, 745, 50)
    c.playback_indicators(80, 264, 36)
    c.label(s.source, 343, 311, 448)
    c.stereo_vu(56, 333, 735, 100)
    c.app_volume(877, 415, 290)
    c.progress(56, 440, 735, 4, labels=False)
    c.caption(s.timing(), 1167, 470, width=340, align="right")
    c.footer(width=735)
# VBWR E
