# VBWR B
#
# Project: Thermalright VibeWare
# Repository: https://github.com/umbertotechnopreneur/thermalright-vibeware
# Creator: Umberto Giacobbi | https://umbertogiacobbi.biz
#
# File: render.py
# Purpose: Composes the ribbon theme in the media pack using shared drawing helpers.
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
    """Compose the ribbon theme using the shared Canvas API.

    Args:
        c: Shared Canvas facade; all coordinates use its logical theme space.
        s: Current ThemeContext containing available data, palette and page timing.
    """
    if s.page_id == "artist":
        from artist_page import render as render_artist
        render_artist(c, s)
        return
    if c.compact:
        c.compact_media("ribbon")
        return
    c.backdrop("ribbon")
    c.header("MEDIA / RIBBON")
    c.panel(80, 83, 1120, 233, radius=35)
    c.cover(105, 105, 190, radius=24)
    c.label(s.source, 340, 113, 640, s.accent)
    c.text(s.title, 340, 169, min(40, 52 / s.font_scale), width=640, bold=True)
    c.text(s.artist, 340, 210, min(23, 30 / s.font_scale), s.muted, 810)
    c.caption(s.album, 340, 244, width=810)
    c.playback_indicators(1035, 105, 36)
    c.caption(s.timing(), 340, 290, width=420)
    c.app_volume(904, 290, 245)
    c.progress(105, 327, 1045, 4, labels=False)
    c.spectrum(105, 340, 490, 100)
    c.stereo_vu(654, 336, 496, 104)
    c.footer()
# VBWR E
