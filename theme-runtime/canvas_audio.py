# VBWR B
#
# Project: Thermalright VibeWare
# Repository: https://github.com/umbertotechnopreneur/thermalright-vibeware
# Creator: Umberto Giacobbi | https://umbertogiacobbi.biz
#
# File: canvas_audio.py
# Purpose: Draws available output spectrum and stereo/session meters using shared Canvas primitives.
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

"""AudioDrawing compositions built on the Canvas primitive API."""
from __future__ import annotations

import math
import skia

from drawing_support import color, number, clip, progress_color, optional_number, db_label

class AudioDrawing:
    def vu(self, x, y, width=190, height=18, label=True):
        """Draw the available session peak using the shared dBFS display scale.

        Args:
            x: Horizontal origin in logical canvas units.
            y: Vertical origin in logical canvas units.
            width: Available width in logical canvas units; None uses the helper's default.
            height: Available height in logical canvas units.
            label: Whether to include the measured peak label.
        """
        peak = self.ctx.peak
        if peak is None:
            self.caption("LEVEL —", x, y + height, width=width)
            if label:
                value = f"{clip(self.ctx.volume, 0, 100):.0f}%" if self.ctx.volume is not None else "—"
                self.text(value, x + width + 16, y + height, self.text_size("caption"), self.ctx.fg)
            return
        normalized = clip((20 * math.log10(max(.001, number(peak))) + 60) / 60) if peak is not None else 0
        count = max(8, min(48, int(width / 9)))
        for index in range(count):
            shade = progress_color(self.ctx.palette[2], index / max(1, count - 1), self.ctx.light)
            self.rect(x + index * width / count, y, width / count - 3, height, shade, 2,
                      1 if index / count < normalized else .18)
        if label:
            value = f"{clip(self.ctx.volume, 0, 100):.0f}%" if self.ctx.volume is not None else "—"
            self.text(value, x + width + 16, y + height, self.text_size("caption"), self.ctx.fg)

    def app_volume(self, x, y, width=180, stacked=False):
        """The media application's gain setting, never the system-output meter.

        Args:
            x: Horizontal origin in logical canvas units.
            y: Vertical origin in logical canvas units.
            width: Available width in logical canvas units; None uses the helper's default.
            stacked: Whether to place the volume label below its value.
        """
        value = optional_number(self.ctx.volume)
        value = f"{clip(value, 0, 100):.0f}%" if value is not None else "—"
        label_width = width if stacked else width - 82
        self.caption("APP VOL" if label_width < 140 else "APP VOLUME", x, y, width=label_width)
        self.text(value, x if stacked else x + width, y + 36 if stacked else y,
                  min(25, 31 / self.ctx.font_scale) if stacked else self.text_size("body"),
                  width=width if stacked else 72, align="left" if stacked else "right", bold=stacked)

    def playback_indicators(self, x, y, size=36):
        """Large read-only glyphs, drawn locally without icon fonts or controls.

        Args:
            x: Horizontal origin in logical canvas units.
            y: Vertical origin in logical canvas units.
            size: Text size or square image size in logical canvas units.
        """
        caption = self.text_size("instrument")
        tint = self.ctx.accent if self.ctx.playing else self.ctx.fg
        if not self.ctx.has_media or (not self.ctx.playing and self.ctx.playback_state != "paused"):
            self.circle(x + size / 2, y + size / 2, size * .42, self.ctx.muted, .6, 2)
            self.line(x + size * .31, y + size / 2, x + size * .69, y + size / 2, self.ctx.muted, 2)
        elif self.ctx.playing:
            self.polygon([(x + size * .26, y + size * .10), (x + size * .26, y + size * .90),
                          (x + size * .90, y + size * .50)], tint)
        else:
            self.rect(x + size * .20, y + size * .12, size * .20, size * .76, tint, 2)
            self.rect(x + size * .60, y + size * .12, size * .20, size * .76, tint, 2)
        status = {"NO SESSION": "NO MEDIA", "NOT PLAYING": "INACTIVE"}.get(
            self.ctx.playback_status(), self.ctx.playback_status())
        self.text(status, x + size / 2, y + size + 23, caption,
                  self.ctx.muted, 96, align="center")
        left = x + size + 62
        active = self.ctx.shuffle is True
        known = self.ctx.shuffle is not None
        tint = self.ctx.accent if active else self.ctx.muted
        opacity = 1 if active else .6 if known else .35
        for inverted in (False, True):
            top, bottom = (.22, .78) if not inverted else (.78, .22)
            path = skia.Path()
            path.moveTo(left + size * .06, y + size * top)
            path.cubicTo(left + size * .45, y + size * top,
                         left + size * .48, y + size * bottom,
                         left + size * .86, y + size * bottom)
            self.native.drawPath(path, self.paint(tint, opacity, max(2, size * .065)))
            self.polygon([(left + size, y + size * bottom),
                          (left + size * .77, y + size * (bottom - .16)),
                          (left + size * .77, y + size * (bottom + .16))], tint, opacity)
        state = "ON" if active else "OFF" if known else "—"
        self.text("SHFL " + state, left + size / 2, y + size + 23, caption,
                  tint, 96, align="center")

    def spectrum(self, x, y, width, height=130):
        """Measured 32-band log-frequency spectrum. No input means no bars.

        Args:
            x: Horizontal origin in logical canvas units.
            y: Vertical origin in logical canvas units.
            width: Available width in logical canvas units; None uses the helper's default.
            height: Available height in logical canvas units.
        """
        if not self.ctx.show_spectrum:
            return
        analysis = self.ctx.analysis
        small = self.text_size("instrument")
        # Keep the source name and units readable; omit optional FFT metadata in
        # narrow slots instead of shrinking the source into an eight-pixel label.
        heading = analysis.caption() if width >= 600 else "SYSTEM OUTPUT"
        self.text(heading, x, y + 17, small, self.ctx.muted, width - 65)
        self.text("dBFS", x + width, y + 17, small, self.ctx.muted, 58, align="right")
        left, top = x + 41, y + 38
        plot_width, plot_height = width - 41, height - 65
        bottom = top + plot_height
        for level in ((-60, -30, 0) if plot_height >= 54 else (-60, 0)):
            line_y = bottom - (level + 60) / 60 * plot_height
            self.line(left, line_y, x + width, line_y, self.ctx.fg, opacity=.13)
            self.text(str(level), left - 8, line_y + 5, small,
                      self.ctx.muted, 33, align="right")
        if not analysis.bands:
            baseline = top + plot_height / 2 + 6
            self.text("Spectrum unavailable", left + plot_width / 2, baseline,
                      small, self.ctx.muted, plot_width - 16, align="center")
            if plot_height >= 70:
                self.text(analysis.status, left + plot_width / 2, baseline + 25,
                          small, self.ctx.muted, plot_width - 16, align="center")
            return
        logs = [math.log10(value) for value in analysis.frequencies]
        low, high = logs[0] - (logs[1] - logs[0]) / 2, logs[-1] + (logs[-1] - logs[-2]) / 2
        span = high - low
        shader = skia.GradientShader.MakeLinear([(left, top), (left, bottom)],
            [color(progress_color(self.ctx.palette[2], value, self.ctx.light)) for value in (1, .5, 0)])
        paint = skia.Paint(Shader=shader, AntiAlias=True)
        for index, value in enumerate(analysis.display_bands):
            a = low if index == 0 else (logs[index - 1] + logs[index]) / 2
            b = high if index == 31 else (logs[index] + logs[index + 1]) / 2
            bar_x = left + (a - low) / span * plot_width
            bar_width = max(1, (b - a) / span * plot_width - 2)
            bar_height = clip((value + 60) / 60) * plot_height
            if bar_height > .1:
                self.native.drawRoundRect(skia.Rect.MakeXYWH(bar_x + 1, bottom - bar_height,
                                                            bar_width, bar_height), 1.5, 1.5, paint)
        # Only frequency labels within the provider's measured range are shown.
        first_x = left + (logs[0] - low) / span * plot_width
        final_x = left + (logs[-1] - low) / span * plot_width
        labels = [(analysis.frequencies[0], first_x, "left")]
        last_x = first_x
        for frequency in (100, 1000, 10000):
            if analysis.frequencies[0] < frequency < analysis.frequencies[-1]:
                label_x = left + (math.log10(frequency) - low) / span * plot_width
                if label_x - last_x >= 62 and final_x - label_x >= 92:
                    labels.append((frequency, label_x, "center"))
                    last_x = label_x
        labels.append((analysis.frequencies[-1], final_x, "right"))
        for frequency, label_x, align in labels:
            value = f"{frequency / 1000:.3g}k" if frequency >= 1000 else f"{frequency:.3g}"
            if align == "right":
                value += " Hz"
            self.text(value, label_x, y + height - 4, small, self.ctx.muted, 88 if align == "right" else 58, align=align)

    def stereo_vu(self, x, y, width, height=100):
        """RMS fill, instantaneous peak line and provider peak-hold marker.

        Args:
            x: Horizontal origin in logical canvas units.
            y: Vertical origin in logical canvas units.
            width: Available width in logical canvas units; None uses the helper's default.
            height: Available height in logical canvas units.
        """
        analysis = self.ctx.analysis
        small = self.text_size("instrument")
        numerical_width = 204
        column_width = numerical_width / 3
        meter_x = x + 25
        meter_width = width - numerical_width - 41
        title = "SYSTEM OUTPUT" if width >= 420 else "SYSTEM MIX"
        if analysis.channels > 2:
            title = "SYSTEM · FRONT L/R" if width >= 600 else "FRONT L/R"
        self.text(title, x, y + 17, small, self.ctx.muted, width - numerical_width - 12)
        for column, name in enumerate(("RMS", "PK", "HOLD")):
            self.text(name, x + width - numerical_width + (column + 1) * column_width,
                      y + 17, small, self.ctx.muted, column_width - 6, align="right")
        row_height = (height - 52) / 2
        bar_height = min(18, row_height * .63)
        for index, channel in enumerate(("L", "R")):
            bar_y = y + 31 + index * row_height
            baseline = bar_y + bar_height - 1
            self.text(channel, x, baseline, small, self.ctx.muted, 18)
            self.rect(meter_x, bar_y, meter_width, bar_height, self.ctx.fg, 3, .09)
            rms = analysis.display_rms[index]
            if rms is not None and rms > -60:
                fill = meter_width * clip((rms + 60) / 60)
                self.native.save()
                self.native.clipRect(skia.Rect.MakeXYWH(meter_x, bar_y, fill, bar_height))
                self.gradient(meter_x, bar_y, meter_width, bar_height,
                              [progress_color(self.ctx.palette[2], value, self.ctx.light) for value in (0, .5, 1)], radius=3)
                self.native.restore()
            peak, hold = analysis.display_peaks[index], analysis.holds[index]
            if peak is not None and peak >= -60:
                peak_x = meter_x + clip((peak + 60) / 60) * meter_width
                self.line(peak_x, bar_y - 1, peak_x, bar_y + bar_height + 1,
                          progress_color(self.ctx.palette[2], (peak + 60) / 60, self.ctx.light), 2)
            if hold is not None and hold >= -60:
                hold_x = meter_x + clip((hold + 60) / 60) * meter_width
                self.line(hold_x, bar_y - 3, hold_x, bar_y + bar_height + 3, self.ctx.fg, 2)
            for column, value in enumerate((analysis.rms[index], analysis.peaks[index], analysis.holds[index])):
                self.text(db_label(value), x + width - numerical_width + (column + 1) * column_width,
                          baseline, small, self.ctx.fg, column_width - 6, align="right")
        ticks = (-60, -30, -12, 0) if meter_width >= 330 else (-60, -30, 0) if meter_width >= 170 else (-60, 0)
        for level in ticks:
            tick_x = meter_x + (level + 60) / 60 * meter_width
            self.line(tick_x, y + height - 23, tick_x, y + height - 20, self.ctx.muted, opacity=.5)
            align = "left" if level == -60 else "right" if level == 0 else "center"
            self.text(str(level), tick_x, y + height - 3, small,
                      self.ctx.muted, 34, align=align)
        if not analysis.available:
            summary = "UNAVAILABLE · dBFS"
        else:
            summary = "dBFS" + (" · " + analysis.status if analysis.status and analysis.status != "Live" else "")
        self.text(summary, x + width, y + height - 3, small, self.ctx.muted,
                  numerical_width, align="right")
