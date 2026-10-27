# VBWR B
#
# Project: Thermalright VibeWare
# Repository: https://github.com/umbertotechnopreneur/thermalright-vibeware
# Creator: Umberto Giacobbi | https://umbertogiacobbi.biz
#
# File: canvas_composition.py
# Purpose: Draws cached backgrounds, shared page furniture and Codex compositions.
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

"""CompositionDrawing compositions built on the Canvas primitive API."""
from __future__ import annotations

import math
import skia

from drawing_support import cached, color, blend, clip, progress_color, SAMPLING
from theme_styles import MEDIA_TREATMENTS

class CompositionDrawing:
    def header(self, name=None, extended=False):
        """Draw the category heading and the available clock/status detail.

        Args:
            name: Display heading or setting/reading name used by this helper.
            extended: Whether to include the header's extended status details.
        """
        if self.compact:
            self.label(name or self.ctx.category, 24, 28, 245)
            self.text(self.ctx.now.strftime("%H:%M"), 456, 30,
                      clip(22 * self.ctx.font_scale, 22, 28) / self.ctx.font_scale, align="right")
            self.caption(self.ctx.now.strftime("%d %b %Y"), 456, 59, width=280, align="right")
        else:
            self.label(name or self.ctx.category, 40, 35, 370)
            self.text(self.ctx.now.strftime("%H:%M"), 1240, 42, 28, align="right")
            date = self.ctx.now.strftime("%A %d %B %Y" if extended else "%d %b %Y")
            if str(self.ctx.clock.get("locale", "")).lower().startswith("it"):
                days = ("Lunedì", "Martedì", "Mercoledì", "Giovedì", "Venerdì", "Sabato", "Domenica")
                months = ("gennaio", "febbraio", "marzo", "aprile", "maggio", "giugno", "luglio", "agosto", "settembre", "ottobre", "novembre", "dicembre")
                date = f"{self.ctx.now.day} {months[self.ctx.now.month - 1]} {self.ctx.now.year}"
                if extended:
                    date = days[self.ctx.now.weekday()] + " " + date
            date_right = 1240 - 170 * self.ctx.font_scale
            self.caption(date.upper(), date_right, 39, width=date_right - 450, align="right")

    def backdrop(self, style="aurora", artwork=None):
        """Draw a cached theme background with optional bounded ambient movement.

        Args:
            style: Stable visual style identifier from the selected theme.
            artwork: Optional decoded image overriding the context's cover art.
        """
        ctx = self.ctx
        treatment = MEDIA_TREATMENTS.get(style, {}) if ctx.category == "multimedia" else {}
        use_art = treatment.get("photo", False) if artwork is None else bool(artwork)
        use_art = bool(use_art and ctx.cover)
        key = ("background-v2", self.width, style, ctx.light, ctx.base, ctx.palette,
               ctx.cover_key if use_art else "", ctx.dominant if ctx.category == "multimedia" else "",
               ctx.signal if ctx.category == "codex" and (ctx.approval or ctx.ready) else "")
        def create():
            # Only cached ambience is half-resolution. Cover and glyphs stay sharp.
            """Build the expensive drawing layer only when its cache key is absent."""
            full_width, full_height = self.width + 48, self.height + 48
            surface = skia.Surface(full_width // 2, full_height // 2)
            native = surface.getCanvas()
            native.clear(color(ctx.base))
            native.scale(.5, .5)
            if use_art:
                image = ctx.cover
                ratio = max(full_width / image.width(), full_height / image.height())
                crop_width, crop_height = full_width / ratio, full_height / ratio
                source = skia.Rect.MakeXYWH((image.width() - crop_width) / 2,
                    (image.height() - crop_height) / 2, crop_width, crop_height)
                paint = self.paint("#FFFFFF")
                paint.setImageFilter(skia.ImageFilters.Blur(treatment.get("blur", 25), treatment.get("blur", 25)))
                # Aspect-fill at full opacity; the earlier double-opacity layer hid it.
                native.drawImageRect(image, source, skia.Rect.MakeWH(full_width, full_height), SAMPLING, paint)
                native.drawColor(color(ctx.base, treatment.get("light" if ctx.light else "dark", .30)))
                if not ctx.light:
                    scrim = skia.GradientShader.MakeLinear([(0, 0), (0, full_height)],
                        [color("#08101C", .05), color("#08101C", .62 if style == "cinematic" else .23)])
                    native.drawPaint(skia.Paint(Shader=scrim))
            arrangements = {
                "aurora": ((.05, .12), (.75, -.1), (1.05, .85), (.35, 1.15)),
                "editorial": ((-.1, .92), (.75, 1.25), (1.05, .9), (.1, 1.4)),
                "orbit": ((-.05, .15), (.1, 1.05), (.84, .3), (.56, 1.4)),
                "prism": ((.1, .1), (.76, -.12), (1.05, .96), (.54, 1.3)),
                "signal": ((-.2, .3), (1.15, .6), (.5, 1.6), (.6, -.9)),
                "halo": ((.5, .5), (.5, -.3), (1.1, .7), (-.1, .9)),
                "beacon": ((-.12, .1), (.78, -.25), (.02, 1.1), (1.15, 1.1)),
                "timeline": ((-.1, .6), (.8, 1.2), (1.1, -.1), (.5, 1.4)),
                "terminal": ((-.3, -.5), (1.3, 1.4), (.0, 1.7), (1.3, -.5)),
                "focus": ((.5, 1.3), (1.1, .0), (-.15, .2), (.5, -.8)),
                "pulse": ((.16, .53), (.2, .5), (.35, .6), (.96, 1.5)),
                "approval": ((.1, .0), (.56, -.5), (1.04, .75), (.02, 1.1)),
                "flow": ((-.08, 1.1), (1.1, .9), (.0, -.2), (.9, 1.4)),
                "quiet": ((-.3, .8), (1.2, 1.2), (.5, -1.0), (1.3, -.5)),
                "dispatch": ((-.1, .15), (.85, 1.1), (1.0, -.4), (.1, 1.5)),
                "chronicle": ((-.15, .4), (.7, 1.4), (1.1, -.1), (.6, -1.0)),
            }
            positions = arrangements.get(style, ((.08, .23), (.74, .11), (.94, .88), (.39, .94)))
            strength = treatment.get("blob", .45 if ctx.light else .60)
            if style in ("quiet", "terminal"):
                strength = .25
            for index, (left, top) in enumerate(positions):
                shade = ctx.palette[index]
                radius = self.width * (.30 if index % 2 else .43)
                if style == "orbit" and index < 2:
                    shade = blend(ctx.dominant, "#FFF2D8", .62 if index == 0 else .30)
                    radius = self.width * .43
                shader = skia.GradientShader.MakeRadial((left * self.width, top * self.height), radius,
                    [color(shade, .78 if style == "orbit" and index < 2 else strength), color(shade, 0)])
                native.drawPaint(skia.Paint(Shader=shader, AntiAlias=True))
            if use_art and style in ("aurora", "orbit", "nocturne") and not ctx.light:
                scrim = skia.GradientShader.MakeLinear([(self.width * .28, 0), (full_width, 0)],
                    [color("#07111D", 0), color("#07111D", .40)])
                native.drawPaint(skia.Paint(Shader=scrim))
            if style in ("prism", "duotone", "dispatch"):
                path = skia.Path()
                path.moveTo(self.width * .24, 0)
                path.lineTo(self.width * .63, self.height)
                path.lineTo(full_width, self.height * .20)
                path.lineTo(full_width, 0)
                path.close()
                native.drawPath(path, self.paint(ctx.palette[1], .17 if ctx.light else .24))
                path = skia.Path()
                path.moveTo(0, full_height)
                path.lineTo(self.width * .70, self.height * .18)
                path.lineTo(self.width * .44, full_height)
                path.close()
                native.drawPath(path, self.paint(ctx.fg, .065))
            if style in ("ribbon", "halo"):
                path = skia.Path()
                path.moveTo(-70, 420)
                path.cubicTo(350, -100, 850, 650, self.width + 100, 170)
                paint = self.paint(ctx.palette[1], .48, 94)
                paint.setImageFilter(skia.ImageFilters.Blur(28, 28))
                native.drawPath(path, paint)
            if style == "terminal":
                for column in range(0, full_width, 80):
                    native.drawLine(column, 0, column, full_height, self.paint(ctx.palette[0], .07, 1))
                for row in range(0, full_height, 60):
                    native.drawLine(0, row, full_width, row, self.paint(ctx.palette[0], .06, 1))
            if ctx.category == "codex" and (ctx.approval or ctx.ready):
                glow_color = ctx.palette.warning if ctx.approval else "#57C88D"
                for left, top, radius in ((1.03, .24, .44), (-.10, .93, .35)):
                    shader = skia.GradientShader.MakeRadial((left * self.width, top * self.height), self.width * radius,
                        [color(glow_color, .58 if ctx.light else .72), color(glow_color, 0)])
                    native.drawPaint(skia.Paint(Shader=shader, AntiAlias=True))
                if style == "approval":
                    for left, top, radius in ((.92, .25, 34), (.05, .80, 25), (.72, .08, 20), (1.02, .72, 42)):
                        paint = self.paint(glow_color, .55)
                        paint.setImageFilter(skia.ImageFilters.Blur(18, 18))
                        native.drawCircle(left * self.width, top * self.height, radius, paint)
            return surface.makeImageSnapshot()
        image = cached(key, create)
        # Translate one cached layer by <=1 pixel per second; no repeated blur.
        tick = int(self.ctx.now.timestamp()) % 48
        drift = (tick if tick <= 24 else 48 - tick) if ctx.ambient_drift else 12
        self.native.drawImageRect(image, skia.Rect.MakeXYWH(-drift, -12, self.width + 48, self.height + 48), SAMPLING)

    def panel(self, x, y, width, height, radius=24, opacity=None):
        """Draw a theme panel using the current appearance and opacity.

        Args:
            x: Horizontal origin in logical canvas units.
            y: Vertical origin in logical canvas units.
            width: Available width in logical canvas units; None uses the helper's default.
            height: Available height in logical canvas units.
            radius: Radius or corner radius in logical canvas units.
            opacity: Alpha fraction from transparent zero to opaque one.
        """
        opacity = opacity if opacity is not None else (.58 if self.ctx.light else .38)
        self.rect(x, y + 8, width, height, "#000000", radius, .06 if self.ctx.light else .12)
        self.rect(x, y, width, height, "#FFFFFF" if self.ctx.light else self.ctx.base, radius, opacity)
        self.rect(x, y, width, height, self.ctx.fg, radius, .19 if self.ctx.light else .15, 1)

    def glow(self, x, y, radius, tint=None, opacity=.45):
        """Draw a cached soft radial glow layer.

        Args:
            x: Horizontal origin in logical canvas units.
            y: Vertical origin in logical canvas units.
            radius: Radius or corner radius in logical canvas units.
            tint: Optional RGB hex override; None uses this drawing helper's semantic theme color.
            opacity: Alpha fraction from transparent zero to opaque one.
        """
        tint = tint or self.ctx.signal
        size = max(2, int(radius * 2))
        key = ("glow", size, tint, opacity)
        def create():
            """Build the expensive drawing layer only when its cache key is absent."""
            surface = skia.Surface(size // 2, size // 2)
            surface.getCanvas().clear(skia.ColorTRANSPARENT)
            shader = skia.GradientShader.MakeRadial((size / 4, size / 4), size / 4,
                [color(tint, opacity), color(tint, 0)])
            surface.getCanvas().drawPaint(skia.Paint(Shader=shader, AntiAlias=True))
            return surface.makeImageSnapshot()
        self.native.drawImageRect(cached(key, create), skia.Rect.MakeXYWH(x - radius, y - radius, radius * 2, radius * 2), SAMPLING)

    def ring(self, x, y, radius, value=None, tint=None, stroke=4, marker=False):
        """Draw an optional value arc and its decorative backing ring.

        Args:
            x: Horizontal origin in logical canvas units.
            y: Vertical origin in logical canvas units.
            radius: Radius or corner radius in logical canvas units.
            value: Optional normalized progress fraction; None draws only the decorative ring.
            tint: Optional RGB hex override; None uses this drawing helper's semantic theme color.
            stroke: Outline thickness; zero selects a filled shape.
            marker: Whether to draw an arc endpoint marker for an available value.
        """
        self.circle(x, y, radius, self.ctx.fg, .13, stroke)
        if value is not None and value > 0:
            paint = self.paint(progress_color(tint or self.ctx.accent, value, self.ctx.light), 1, stroke)
            paint.setStrokeCap(skia.Paint.kRound_Cap)
            self.native.drawArc(skia.Rect.MakeXYWH(x - radius, y - radius, radius * 2, radius * 2), -90, 360 * clip(value), False, paint)
            if marker:
                angle = math.radians(-90 + 360 * clip(value))
                self.circle(x + math.cos(angle) * radius, y + math.sin(angle) * radius, 5.5, self.ctx.fg)

    def footer(self, text=None, width=None):
        """Draw the category's footer text in the reserved bottom area.

        Args:
            text: Source text to wrap or display.
            width: Available width in logical canvas units; None uses the helper's default.
        """
        text = text or ("DEMO DATA · PREVIEW" if self.ctx.preview else "THERMALRIGHT VIBEWARE")
        if self.ctx.preview and not text.startswith("DEMO DATA"):
            text = "DEMO DATA · PREVIEW · " + text
        self.label(text, 24 if self.compact else 40, 470, width or self.width - 80)

    def status_symbol(self, x, y, radius=60):
        """Draw the Codex activity symbol from the current reported status.

        Args:
            x: Horizontal origin in logical canvas units.
            y: Vertical origin in logical canvas units.
            radius: Radius or corner radius in logical canvas units.
        """
        self.circle(x, y, radius, self.ctx.signal, .13)
        self.circle(x, y, radius, self.ctx.signal, .7, 2)
        if self.ctx.approval:
            self.line(x, y - radius * .42, x, y + radius * .08, self.ctx.signal, 7)
            self.circle(x, y + radius * .38, 4, self.ctx.signal)
        elif self.ctx.ready:
            self.line(x - radius * .38, y, x - radius * .08, y + radius * .25, self.ctx.signal, 6)
            self.line(x - radius * .08, y + radius * .25, x + radius * .42, y - radius * .30, self.ctx.signal, 6)
        elif self.ctx.stale or not self.ctx.event or self.ctx.event in ("SessionEnd", "Interrupt"):
            self.line(x - radius * .26, y, x + radius * .26, y, self.ctx.signal, 4)
        else:
            for index, offset in enumerate((-.28, 0, .28)):
                # Pulse only for received live work; these dots are not progress.
                opacity = .8
                if self.ctx.is_live:
                    opacity = .35 + .55 * (1 + math.sin(self.ctx.elapsed_seconds * 4 - index * .9)) / 2
                self.circle(x + radius * offset, y, 4, self.ctx.signal, opacity)

    def activity_rows(self, x, y, width, row_height=64, limit=4):
        """Draw a bounded list of local Codex event rows.

        Args:
            x: Horizontal origin in logical canvas units.
            y: Vertical origin in logical canvas units.
            width: Available width in logical canvas units; None uses the helper's default.
            row_height: Vertical spacing of activity rows in logical canvas units.
            limit: Maximum number of activity rows to show.
        """
        events = self.ctx.events()[-limit:]
        if not events:
            self.text("No activity received yet", x, y + 24, self.text_size("body"), self.ctx.muted, width)
            return
        for index, item in enumerate(events):
            top = y + index * row_height
            self.circle(x + 6, top + 8, 4, self.ctx.signal, 1 if index == len(events) - 1 else .4)
            if index < len(events) - 1:
                self.line(x + 6, top + 15, x + 6, top + row_height - 6, self.ctx.signal, 1, .3)
            self.text(item.get("status") or item.get("event"), x + 26, top + 19, self.text_size("body"), width=width - 35)
            detail = item.get("tool") or item.get("toolKind") or item.get("event")
            self.caption(detail, x + 26, top + 47, width=width - 35)

    def compact_codex(self, variant):
        """Compose the selected Codex style on the square canvas.

        Args:
            variant: Selected composition's stable style identifier.
        """
        self.backdrop(variant)
        self.header("CODEX / " + variant)
        if variant in ("beacon", "pulse", "approval", "quiet"):
            self.status_symbol(240, 150, 64)
            self.text(self.ctx.status, 240, 261, clip(29 * self.ctx.font_scale, 28, 42) / self.ctx.font_scale,
                      self.ctx.signal, 432, align="center", bold=True)
            self.text(self.ctx.chat_title, 240, 307, self.text_size("body"), width=432, align="center")
            self.caption(self.ctx.activity_detail(), 240, 349, width=432, align="center")
        else:
            title_size = clip(25 * self.ctx.font_scale, 24, 34) / self.ctx.font_scale
            self.text(self.ctx.chat_title, 24, 110, title_size, width=432, bold=True)
            self.text(self.ctx.status, 24, 154, title_size, self.ctx.signal, 432)
            self.activity_rows(24, 211, 420, 66, 3)
        self.line(24, 422, 456, 422, self.ctx.fg, opacity=.12)
        self.footer(self.ctx.activity_detail())
