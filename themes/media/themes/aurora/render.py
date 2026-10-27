# VBWR B
#
# Project: Thermalright VibeWare
# Repository: https://github.com/umbertotechnopreneur/thermalright-vibeware
# Creator: Umberto Giacobbi | https://umbertogiacobbi.biz
#
# File: render.py
# Purpose: Composes the aurora theme in the media pack using shared drawing helpers.
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
    """Compose the aurora theme using the shared Canvas API.

    Args:
        c: Shared Canvas facade; all coordinates use its logical theme space.
        s: Current ThemeContext containing available data, palette and page timing.
    """
    if s.page_id == "artist":
        from artist_page import render as render_artist
        render_artist(c, s)
        return
    if c.compact:
        c.compact_media("aurora")
        return
    c.backdrop("aurora", artwork=True)
    c.header("MEDIA / AURORA GLASS")
    c.cover(48, 74, 286, radius=16)
    c.label(s.source, 382, 89, 625, s.accent)
    c.text(s.title, 382, 151, min(41, 55 / s.font_scale), width=625, bold=True)
    c.text(s.artist, 382, 193, min(24, 31 / s.font_scale), s.muted, 830)
    c.caption(s.album, 382, 225, width=830)
    c.playback_indicators(1060, 80, 38)
    c.app_volume(48, 399, 265)
    c.spectrum(382, 262, 450, 145)
    c.stereo_vu(862, 263, 350, 144)
    c.progress(48, 431, 1180, 6, labels=False)
    c.caption(s.timing(), 1228, 470, width=340, align="right")
    c.footer(s.source, width=800)
# VBWR E
