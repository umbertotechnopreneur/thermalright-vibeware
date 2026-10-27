# VBWR B
#
# Project: Thermalright VibeWare
# Repository: https://github.com/umbertotechnopreneur/thermalright-vibeware
# Creator: Umberto Giacobbi | https://umbertogiacobbi.biz
#
# File: canvas_media.py
# Purpose: Composes media artwork, playback progress and compact Music layouts.
#
# VibeWare: Human intent, AI execution, and plenty of tokens
# Manifesto: https://umbertogiacobbi.biz/vibeware/manifesto
#
# Created with AI: OpenAI Codex; extracted existing first-party source; 2026-10-27.
# Modified with AI: OpenAI Codex; source documentation and organization; 2026-10-27.
# Human guidance: Umberto Giacobbi; maintainability and standard file headers.
#
# Copyright (c) 2026 Umberto Giacobbi
# SPDX-License-Identifier: MIT
#
# VBWR E

"""MediaDrawing compositions built on the Canvas primitive API."""
from __future__ import annotations

import skia

from drawing_support import cached, blend, clip, progress_color, duration, SAMPLING

class MediaDrawing:
    def cover(self, x, y, size, radius=16, circular=False, shadow=True):
        """Draw the available cover image with the requested crop and shape.

        Args:
            x: Horizontal origin in logical canvas units.
            y: Vertical origin in logical canvas units.
            size: Text size or square image size in logical canvas units.
            radius: Radius or corner radius in logical canvas units.
            circular: Whether to clip the cover to a circle.
            shadow: Whether to draw the cached decorative cover shadow.
        """
        if shadow:
            key = ("shadow", size, radius, circular, self.ctx.dominant)
            def create_shadow():
                """Build a cached cover shadow layer for the requested geometry."""
                surface = skia.Surface(int(size + 96), int(size + 96))
                surface.getCanvas().clear(skia.ColorTRANSPARENT)
                paint = self.paint(self.ctx.dominant, .65)
                paint.setImageFilter(skia.ImageFilters.Blur(16, 16))
                r = size / 2 if circular else radius
                surface.getCanvas().drawRoundRect(skia.Rect.MakeXYWH(48, 64, size, size), r, r, paint)
                return surface.makeImageSnapshot()
            self.native.drawImage(cached(key, create_shadow), x - 48, y - 48)
        rect = skia.Rect.MakeXYWH(x, y, size, size)
        r = size / 2 if circular else radius
        self.native.save()
        self.native.clipRRect(skia.RRect.MakeRectXY(rect, r, r), doAntiAlias=True)
        if self.ctx.cover:
            image = self.ctx.cover
            side = min(image.width(), image.height())
            source = skia.Rect.MakeXYWH((image.width() - side) / 2, (image.height() - side) / 2, side, side)
            self.native.drawImageRect(image, source, rect, SAMPLING)
        else:
            self.gradient(x, y, size, size, [self.ctx.palette[0], self.ctx.palette[2]], True)
            self.circle(x + size / 2, y + size / 2, size * .25, "#FFFFFF", .5, 2)
            self.circle(x + size / 2, y + size / 2, size * .05, "#FFFFFF", .7)
        self.native.restore()
        self.rect(x, y, size, size, self.ctx.fg, r, .16, 1)

    def progress(self, x, y, width, height=5, labels=True):
        """Draw actual playback progress using the shared accent-to-red scale.

        Args:
            x: Horizontal origin in logical canvas units.
            y: Vertical origin in logical canvas units.
            width: Available width in logical canvas units; None uses the helper's default.
            height: Available height in logical canvas units.
            labels: Whether to draw playback position/duration labels.
        """
        self.rect(x, y, width, height, self.ctx.fg, height / 2, .14)
        if self.ctx.progress is not None:
            fill = width * self.ctx.progress
            if fill > .5:
                active = progress_color(self.ctx.accent, self.ctx.progress, self.ctx.light)
                self.gradient(x, y, fill, height, [blend(active, self.ctx.fg, .12), active], radius=height / 2)
        if labels:
            self.caption(duration(self.ctx.position), x, y + 31)
            self.caption(duration(self.ctx.length) if self.ctx.length else "—", x + width, y + 31, align="right")

    def media_text(self, x, y, width, title_size=44, album=True):
        # Keep a fixed-height title slot even with the font-size override.
        """Draw the current title, artist and optional album in the supplied area.

        Args:
            x: Horizontal origin in logical canvas units.
            y: Vertical origin in logical canvas units.
            width: Available width in logical canvas units; None uses the helper's default.
            title_size: Base title text size before applying the theme's font scale.
            album: Whether to include the available album line.
        """
        safe_title_size = min(title_size, 58 / self.ctx.font_scale)
        safe_artist_size = min(24, 32 / self.ctx.font_scale)
        self.label(self.ctx.playback_status(), x, y - 44, width, self.ctx.accent)
        self.text(self.ctx.title, x, y + 12, safe_title_size, width=width, bold=True)
        self.text(self.ctx.artist, x, y + 57, safe_artist_size, self.ctx.muted, width)
        if album and self.ctx.album:
            self.caption(self.ctx.album, x, y + 92, width=width)

    def compact_media(self, variant):
        """Compose the selected Music style on the square canvas.

        Args:
            variant: Selected composition's stable style identifier.
        """
        self.backdrop(variant)
        self.header("MEDIA / " + variant)
        circular = variant in ("orbit", "halo")
        self.cover(24, 75, 124, radius=2 if variant in ("editorial", "prism", "duotone") else 14,
                   circular=circular)
        if circular:
            self.ring(86, 137, 69, self.ctx.progress, stroke=2)
        self.text(self.ctx.title, 164, 107, clip(25 * self.ctx.font_scale, 24, 32) / self.ctx.font_scale,
                  width=292, bold=True)
        self.text(self.ctx.artist, 164, 141, self.text_size("body"), self.ctx.muted, 292)
        self.caption(self.ctx.album, 164, 168, width=292)
        self.text(self.ctx.timing(), 24, 223, self.text_size("instrument"), self.ctx.muted, 132)
        self.playback_indicators(178, 184, 29)
        self.app_volume(348, 195, 108, stacked=True)
        self.spectrum(24, 245, 432, 90)
        self.stereo_vu(24, 339, 432, 98)
        self.progress(24, 444, 432, 3, labels=False)
        self.footer()
