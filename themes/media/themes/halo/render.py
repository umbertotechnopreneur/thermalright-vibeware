# VBWR B
#
# Project: Thermalright VibeWare
# Repository: https://github.com/umbertotechnopreneur/thermalright-vibeware
# Creator: Umberto Giacobbi | https://umbertogiacobbi.biz
#
# File: render.py
# Purpose: Composes the halo theme in the media pack using shared drawing helpers.
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
    """Compose the halo theme using the shared Canvas API.

    Args:
        c: Shared Canvas facade; all coordinates use its logical theme space.
        s: Current ThemeContext containing available data, palette and page timing.
    """
    if s.page_id == "artist":
        from artist_page import render as render_artist
        render_artist(c, s)
        return
    if c.compact:
        c.compact_media("halo")
        return
    c.backdrop("halo")
    c.header("MEDIA / HALO")
    c.cover(490, 85, 252, circular=True)
    c.ring(616, 211, 143, s.progress, stroke=3)
    c.label(s.source, 44, 89, 386, s.accent)
    c.text(s.title, 44, 144, min(35, 47 / s.font_scale), width=386, bold=True)
    c.text(s.artist, 44, 184, min(21, 29 / s.font_scale), s.muted, 386)
    c.caption(s.album, 44, 216, width=386)
    c.playback_indicators(70, 254, 42)
    c.app_volume(44, 360, 235, stacked=True)
    c.text(s.timing(), 616, 405, min(23, 30 / s.font_scale), s.muted, 350, align="center")
    c.spectrum(804, 97, 408, 176)
    c.stereo_vu(804, 288, 408, 139)
    c.footer()
# VBWR E
