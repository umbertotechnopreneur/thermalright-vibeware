# VBWR B
#
# Project: Thermalright VibeWare
# Repository: https://github.com/umbertotechnopreneur/thermalright-vibeware
# Creator: Umberto Giacobbi | https://umbertogiacobbi.biz
#
# File: render.py
# Purpose: Composes the prism theme in the media pack using shared drawing helpers.
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
    """Compose the prism theme using the shared Canvas API.

    Args:
        c: Shared Canvas facade; all coordinates use its logical theme space.
        s: Current ThemeContext containing available data, palette and page timing.
    """
    if s.page_id == "artist":
        from artist_page import render as render_artist
        render_artist(c, s)
        return
    if c.compact:
        c.compact_media("prism")
        return
    c.backdrop("prism")
    c.header("MEDIA / PRISM")
    c.polygon([(394, 84), (649, 84), (484, 371), (394, 371)], s.palette[1], .08)
    c.cover(56, 86, 286, radius=2)
    c.media_text(400, 177, 812, 50)
    c.playback_indicators(80, 383, 36)
    c.app_volume(973, 295, 239)
    c.spectrum(400, 310, 812, 92)
    c.progress(400, 422, 812, 5, labels=False)
    c.caption(s.timing(), 1212, 470, width=340, align="right")
    c.footer(s.source, width=800)
# VBWR E
