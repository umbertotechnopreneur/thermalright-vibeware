# VBWR B
#
# Project: Thermalright VibeWare
# Repository: https://github.com/umbertotechnopreneur/thermalright-vibeware
# Creator: Umberto Giacobbi | https://umbertogiacobbi.biz
#
# File: render.py
# Purpose: Composes the orbit theme in the media pack using shared drawing helpers.
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
    """Compose the orbit theme using the shared Canvas API.

    Args:
        c: Shared Canvas facade; all coordinates use its logical theme space.
        s: Current ThemeContext containing available data, palette and page timing.
    """
    if s.page_id == "artist":
        from artist_page import render as render_artist
        render_artist(c, s)
        return
    if c.compact:
        c.compact_media("orbit")
        return
    c.backdrop("orbit")
    c.header("MEDIA / ORBIT")
    c.cover(136, 65, 332, circular=True)
    c.ring(302, 231, 181, s.progress, stroke=3, marker=True)
    c.label(s.source, 548, 85, 450, s.accent)
    c.text(s.title, 548, 155, min(38, 50 / s.font_scale), width=460, bold=True)
    c.text(s.artist, 548, 193, min(23, 29 / s.font_scale), s.muted, 664)
    c.caption(s.album, 548, 222, width=410)
    c.playback_indicators(1060, 76, 38)
    c.app_volume(991, 222, 220)
    c.spectrum(548, 233, 664, 100)
    c.stereo_vu(548, 341, 664, 100)
    c.caption(s.timing(), 302, 437, width=400, align="center")
    c.footer()
# VBWR E
