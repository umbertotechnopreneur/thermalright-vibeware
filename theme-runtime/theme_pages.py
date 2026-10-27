# VBWR B
#
# Project: Thermalright VibeWare
# Repository: https://github.com/umbertotechnopreneur/thermalright-vibeware
# Creator: Umberto Giacobbi | https://umbertogiacobbi.biz
#
# File: theme_pages.py
# Purpose: Resolves rotating theme pages and explicit page timing.
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

"""Manifest-driven pages share the worker's single monotonic theme clock."""
from __future__ import annotations

def select_page(context, manifest):
    """Validate page metadata and advance the shared clock across available pages.

    Args:
        context: Current per-frame ThemeContext; callers retain its ownership.
        manifest: Selected theme's parsed manifest and optional page/palette metadata.
    """
    pages = manifest.get("pages", [])
    if not isinstance(pages, list) or len(pages) > 8:
        raise ValueError("A theme may declare up to eight pages.")
    ids = [page.get("id") for page in pages if isinstance(page, dict)]
    if len(ids) != len(pages) or any(not isinstance(value, str) or not value or len(value) > 40 for value in ids) or len(set(ids)) != len(ids):
        raise ValueError("Theme page IDs must be unique nonempty strings.")
    seconds = manifest.get("pageSeconds", 8)
    if isinstance(seconds, bool) or not isinstance(seconds, int) or not 4 <= seconds <= 120:
        raise ValueError("pageSeconds must be between 4 and 120.")
    for page in pages:
        duration = page.get("seconds", seconds)
        if isinstance(duration, bool) or not isinstance(duration, int) or not 4 <= duration <= 120:
            raise ValueError("Page seconds must be between 4 and 120.")
    if any(not isinstance(page.get("title", ""), str) or len(page.get("title", "")) > 80 for page in pages):
        raise ValueError("Page titles must be strings up to 80 characters.")
    # An artist page appears only for the exact artist whose profile was resolved.
    if context.category == "multimedia":
        profile = context.artist_profile
        if not profile or profile.get("requestedArtist") != context.artist:
            pages = [page for page in pages if page["id"] != "artist"]
    elif context.category == "system":
        from system_details import page_available
        pages = [page for page in pages if page_available(page["id"], context.sensors)]
    count = max(1, len(pages))
    durations = [page.get("seconds", seconds) for page in pages] or [seconds]
    cycle_seconds = sum(durations)
    start_index = context.page_start_index % count
    elapsed = context.elapsed_seconds + sum(durations[:start_index])
    context.page_cycle = int(elapsed // cycle_seconds)
    position = elapsed % cycle_seconds
    context.page_index = 0
    for index, duration in enumerate(durations):
        context.page_index = index
        if position < duration:
            break
        position -= duration
    context.page_count = count
    context.page_progress = position / durations[context.page_index]
    context.page_id = pages[context.page_index]["id"] if pages else "main"
    context.page_title = pages[context.page_index].get("title", context.page_id) if pages else ""
    return context.page_id
# VBWR E
