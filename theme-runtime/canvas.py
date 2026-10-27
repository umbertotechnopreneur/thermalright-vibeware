# VBWR B
#
# Project: Thermalright VibeWare
# Repository: https://github.com/umbertotechnopreneur/thermalright-vibeware
# Creator: Umberto Giacobbi | https://umbertogiacobbi.biz
#
# File: canvas.py
# Purpose: Supplies shared Skia primitives, bounded visual caches and theme context data.
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

"""Shared Skia primitives. Theme scripts own composition; expensive layers are cached."""
from __future__ import annotations

import base64
import hashlib
import math
import time
from collections import OrderedDict
from datetime import datetime
from pathlib import Path

import numpy as np
import skia

from color_adjustments import ColorAdjustments
from palette import ThemePalette
from drawing_support import (cached, rgb, color, blend, number, clip, progress_color, clean,
                             duration, optional_number, dbfs, db_label, SAMPLING, FALLBACK_PALETTE)
from theme_styles import CODEX_IDENTITIES, SYSTEM_IDENTITIES, MEDIA_TREATMENTS
from canvas_audio import AudioDrawing
from canvas_media import MediaDrawing
from canvas_composition import CompositionDrawing

_COVERS = OrderedDict()
_FONTS = OrderedDict()
_METER_HISTORY = OrderedDict()
_ACTIVE_THEME_TIMING = None

class AudioAnalysis:
    """Measured system-output data, with bounded display ballistics only.

    This renderer does not capture audio or compute an FFT. Values and holds come
    from the native provider. The smoother follows actual values; it never creates
    random bands, waves, peaks, or a spectrum from a session's volume percentage.
    """
    def __init__(self, raw):
        """Validate measured output levels and retain bounded display smoothing state.

        Args:
            raw: Raw observation dictionary received from the engine.
        """
        raw = raw if isinstance(raw, dict) else {}
        self.available = raw.get("available") is True and raw.get("source") == "system-output"
        self.status = clean(raw.get("status"), "Audio analysis unavailable")
        self.sample_rate = max(0, int(number(raw.get("sampleRate"))))
        self.fft_size = max(0, int(number(raw.get("fftSize"))))
        self.channels = max(0, int(number(raw.get("channels"))))
        self.updated_at = clean(raw.get("updatedAtUtc"))
        self.source = "system-output"
        # Accept the spectrum only when all measured bands and ordered frequencies agree.
        self.bands, self.frequencies = [], []
        values = raw.get("bandDbfs")
        frequencies = raw.get("frequenciesHz")
        if self.available and isinstance(values, list) and isinstance(frequencies, list) and len(values) == len(frequencies) == 32:
            measured = [dbfs(value) for value in values]
            centers = [optional_number(value) for value in frequencies]
            if all(value is not None for value in measured) and all(value is not None and value > 0 for value in centers):
                if all(centers[index] < centers[index + 1] for index in range(31)):
                    self.bands, self.frequencies = measured, centers
        self.rms = [dbfs(raw.get("leftRmsDbfs")), dbfs(raw.get("rightRmsDbfs"))]
        self.peaks = [dbfs(raw.get("leftPeakDbfs")), dbfs(raw.get("rightPeakDbfs"))]
        self.holds = [dbfs(raw.get("leftPeakHoldDbfs")), dbfs(raw.get("rightPeakHoldDbfs"))]
        if self.channels < 2:
            self.rms[1], self.peaks[1], self.holds[1] = None, None, None
        if not self.available:
            self.rms, self.peaks, self.holds = [None, None], [None, None], [None, None]
        # Keep source measurements separate from their visual attack/release smoothing.
        self.display_bands = list(self.bands)
        self.display_rms = list(self.rms)
        self.display_peaks = list(self.peaks)
        key = (self.source, self.sample_rate, self.fft_size, self.channels)
        if not self.available:
            _METER_HISTORY.clear()
            return
        now = time.monotonic()
        previous = _METER_HISTORY.get(key)
        elapsed = now - previous["at"] if previous else 0
        if previous and 0 < elapsed < 2:
            def follow(values, old, attack, release):
                """Smooth available meter values with separate attack and release time constants.

                Args:
                    values: Current measured values or semantic palette fields.
                    old: Previous smoothed meter values matching the current channel layout.
                    attack: Smoothing time constant in seconds when a measured level rises.
                    release: Smoothing time constant in seconds when a measured level falls.
                """
                if len(values) != len(old):
                    return list(values)
                result = []
                for target, current in zip(values, old):
                    if target is None or current is None:
                        result.append(target)
                    else:
                        tau = attack if target > current else release
                        result.append(current + (target - current) * (1 - math.exp(-elapsed / tau)))
                return result
            self.display_bands = follow(self.bands, previous["bands"], .025, .32)
            self.display_rms = follow(self.rms, previous["rms"], .055, .36)
            self.display_peaks = follow(self.peaks, previous["peaks"], .008, .45)
        _METER_HISTORY[key] = {"at": now, "bands": self.display_bands,
                               "rms": self.display_rms, "peaks": self.display_peaks}
        _METER_HISTORY.move_to_end(key)
        while len(_METER_HISTORY) > 4:
            _METER_HISTORY.popitem(last=False)

    def caption(self):
        """Describe the actual output sample rate and FFT size for the meter heading."""
        parts = ["SYSTEM OUTPUT"]
        if self.sample_rate:
            parts.append(f"{self.sample_rate / 1000:g} kHz")
        if self.fft_size:
            parts.append(f"FFT {self.fft_size}")
        return " · ".join(parts)

def decoded_cover(media):
    """Decode bounded cover art and cache its image, palette and dominant color.

    Args:
        media: Current media fields, including bounded cover payload/key when available.
    """
    key = clean(media.get("coverKey"))
    encoded = media.get("coverBase64")
    if "coverBase64" in media and encoded is None:
        return None, "", FALLBACK_PALETTE, FALLBACK_PALETTE[0]
    if encoded:
        if not isinstance(encoded, str) or len(encoded) > 16_000_000:
            raise ValueError("Cover payload is too large.")
        key = key or hashlib.sha256(encoded.encode("ascii")).hexdigest()
        if key not in _COVERS:
            raw = base64.b64decode(encoded, validate=True)
            image = skia.Image.MakeFromEncoded(skia.Data.MakeWithCopy(raw))
            if image is None or image.width() * image.height() > 16_777_216:
                raise ValueError("Cover is not a supported image or is too large.")
            ratio = min(1, 1024 / max(image.width(), image.height()))
            image = image.resize(max(1, round(image.width() * ratio)), max(1, round(image.height() * ratio)), SAMPLING)
            pixels = image.resize(16, 16, SAMPLING).toarray(colorType=skia.ColorType.kRGBA_8888_ColorType)
            palette = []
            for row, column in ((0, 0), (0, 8), (8, 0), (8, 8)):
                mean = pixels[row:row + 8, column:column + 8, :3].mean(axis=(0, 1))
                palette.append("#" + "".join(f"{round(float(channel)):02X}" for channel in mean))
            samples = pixels[:, :, :3].reshape(-1, 3).astype(np.uint16)
            opaque = pixels[:, :, 3].reshape(-1) >= 128
            samples = samples[opaque]
            dominant = palette[0]
            if len(samples):
                # Quantize to 8 bins/channel, then average the most populous cluster.
                bins = samples // 32
                keys = bins[:, 0] * 64 + bins[:, 1] * 8 + bins[:, 2]
                largest = int(np.bincount(keys, minlength=512).argmax())
                mean = samples[keys == largest].mean(axis=0)
                dominant = "#" + "".join(f"{round(float(channel)):02X}" for channel in mean)
            _COVERS[key] = (image, tuple(palette), dominant)
            while len(_COVERS) > 4:
                _COVERS.popitem(last=False)
    if key in _COVERS:
        _COVERS.move_to_end(key)
        image, palette, dominant = _COVERS[key]
        return image, key, palette, dominant
    return None, "", FALLBACK_PALETTE, FALLBACK_PALETTE[0]

class ThemeContext:
    def __init__(self, state, request, manifest):
        """Resolve one frame's source observations, controls, theme clock and semantic palette.

        Args:
            state: Dashboard fields received with the current worker request.
            request: Validated worker request with dimensions, controls and theme selection.
            manifest: Selected theme's parsed manifest and optional page/palette metadata.
        """
        global _ACTIVE_THEME_TIMING

        self.media = state.get("media") or {}
        self.audio = state.get("audio") or {}
        self.codex = state.get("codex") or {}
        self.sensors = state.get("sensors") if isinstance(state.get("sensors"), dict) else {}
        self.artist_profile = state.get("artistProfile") if isinstance(state.get("artistProfile"), dict) else {}
        self.desktop = state.get("desktop") or {}
        self.clock = state.get("clock") or {}
        self.light = bool(request.get("light"))
        self.font_family = clean(request.get("fontFamily"), "Cascadia Mono")
        self.font_scale = clip(request.get("fontScale", 1), .65, 1.6)
        self.ambient_drift = bool(request.get("ambientDrift"))
        self.show_spectrum = request.get("showSpectrum", True)
        self.page_start_index = request.get("pageIndex", 0)
        self.page_revision = request.get("pageRevision", 0)
        self.color_filters_enabled = request.get("colorFiltersEnabled", True)
        # A fresh object for each frame: theme overrides cannot mutate UI input
        # or leak into another theme/frame through the static backdrop caches.
        self.colors = ColorAdjustments(
            saturation_percent=request.get("saturationPercent", 100),
            hue_degrees=request.get("hueDegrees", 0),
            brightness_percent=request.get("brightnessPercent", 0),
            contrast_percent=request.get("contrastPercent", 100),
        ).validate()
        # Page timing belongs to the most recently rendered theme, never every installed theme.
        self.category = manifest.get("category", "multimedia")
        self.style = str(manifest.get("id", "")).rsplit(".", 1)[-1]
        self.theme_directory = str(request.get("themePath", ""))
        theme_key = (self.category, str(manifest.get("id", "")), self.theme_directory, self.page_revision)
        frame_time = time.monotonic()
        previous = _ACTIVE_THEME_TIMING
        same_theme = previous and previous[0] == theme_key and frame_time >= previous[2]
        started = previous[1] if same_theme else frame_time
        # Retain only the most recent theme, never one timer per installed theme.
        _ACTIVE_THEME_TIMING = (theme_key, started, frame_time)
        self.elapsed_seconds = max(0, frame_time - started)
        self.preview = bool(state.get("preview") or self.codex.get("preview"))
        self.cover, self.cover_key, cover_palette, self.dominant = decoded_cover(self.media)
        desktop_palette = self.desktop.get("palette")
        desktop_palette = tuple(desktop_palette[:4]) if isinstance(desktop_palette, list) and desktop_palette else FALLBACK_PALETTE
        source_palette = cover_palette if self.category == "multimedia" and self.cover else desktop_palette
        self.palette = tuple(source_palette[i % len(source_palette)] for i in range(4))
        # Resolve the visual identity first, then apply validated semantic palette overrides.
        self.fg = "#182133" if self.light else "#F5F5FC"
        self.muted = "#586378" if self.light else "#ACB7CD"
        self.base = "#F2F1ED" if self.light else "#101522"
        if self.category == "codex":
            dark_base, light_base, anchors = CODEX_IDENTITIES.get(self.style, CODEX_IDENTITIES["beacon"])
            self.palette = tuple(blend(anchor, self.palette[index], .22) for index, anchor in enumerate(anchors))
            self.base = light_base if self.light else dark_base
            self.fg = blend("#172035" if self.light else "#F9F8FD", anchors[0], .10)
            self.muted = blend("#626471" if self.light else "#B9BBCD", anchors[2], .18)
        elif self.category == "system":
            dark_base, light_base, anchors = SYSTEM_IDENTITIES.get(self.style, SYSTEM_IDENTITIES["signal"])
            self.palette = tuple(blend(anchor, "#263D41", .42) if self.light else anchor for anchor in anchors)
            self.base = light_base if self.light else dark_base
            self.fg = "#25332E" if self.light else "#EDF4F1"
            self.muted = "#5E6E66" if self.light else "#9AAEA7"
        elif self.style in ("editorial", "orbit", "duotone"):
            self.base = "#FFF7ED" if self.light else blend("#101924", self.dominant, .18)
        self.accent = blend(self.palette[0], "#193B68" if self.light else "#CBEAFF", .42)
        self.palette = ThemePalette(self.palette, {
            "textPrimary": self.fg, "textSecondary": self.muted, "accent": self.accent,
            "warning": "#9E5218" if self.light else "#FFC16E",
            "info": self.accent,
        }, manifest, request.get("palette"), self.light)
        self.fg = self.palette.textPrimary
        self.muted = self.palette.textSecondary
        self.accent = self.palette.accent
        # Preserve actual media availability separately from the friendly display labels.
        self.title = clean(self.media.get("title"), "Nothing playing")
        self.artist = clean(self.media.get("artist"), "Waiting for a media session")
        self.album = clean(self.media.get("album"))
        self.source = clean(self.media.get("source"), "MEDIA")
        self.playing = bool(self.media.get("playing"))
        self.playback_state = clean(self.media.get("playbackStatus")).lower()
        self.has_media = bool(self.media.get("source") or self.cover or self.playing or number(self.media.get("durationSeconds")) > 0)
        self.shuffle = self.media.get("shuffle") if isinstance(self.media.get("shuffle"), bool) else None
        self.position = max(0, number(self.media.get("positionSeconds")))
        self.length = max(0, number(self.media.get("durationSeconds")))
        self.progress = clip(self.position / self.length) if self.length else None
        self.volume = self.audio.get("volumePercent")
        self.peak = self.audio.get("peak")
        self.analysis = AudioAnalysis(self.audio.get("analysis"))
        self.chat_title = clean(self.codex.get("chatTitle"), "OpenAI Codex")
        self.status = clean(self.codex.get("status"), "Waiting for Codex")
        self.last_status = self.status
        self.event = clean(self.codex.get("event"))
        self.phase = clean(self.codex.get("phase"))
        self.is_live = bool(self.codex.get("isLive"))
        self.stale = bool(self.codex.get("stale"))
        if not self.event and not self.preview:
            self.status = "Waiting for Codex"
            self.is_live = False
        elif self.stale:
            self.status = "Last activity"
            self.is_live = False
        elif self.event == "SessionEnd":
            self.status = "Session closed"
            self.is_live = False
        elif self.event == "Interrupt":
            self.status = "Interrupted"
            self.is_live = False
        self.tool = clean(self.codex.get("tool") or self.codex.get("toolKind"))
        self.model = clean(self.codex.get("model"))
        self.approval = self.event == "PermissionRequest" and self.is_live and not self.stale
        self.ready = self.event == "Stop" and not self.stale
        if self.approval:
            self.signal = self.palette.warning
        elif self.ready:
            self.signal = "#26754D" if self.light else "#8DE2AE"
        elif self.event == "Interrupt" and not self.stale:
            self.signal = "#AA3950" if self.light else "#F7A4AC"
        elif not self.event or self.stale or self.event == "SessionEnd":
            self.signal = self.muted
        else:
            self.signal = self.palette.info
        try:
            self.now = datetime.fromisoformat(str(self.clock.get("iso", "")).replace("Z", "+00:00"))
        except ValueError:
            self.now = datetime.now().astimezone()

    def timing(self):
        """Format the current playback position and duration."""
        return duration(self.position) + " / " + (duration(self.length) if self.length else "—")

    def activity_detail(self):
        """Describe the latest available local Codex activity."""
        if self.approval:
            return "Respond on your computer"
        if self.stale:
            return self.last_status + " · last received event"
        if not self.event:
            return "Connect the Codex hooks to see activity"
        if self.event == "SessionEnd":
            return "No active session"
        if self.event == "Interrupt":
            return "Work was interrupted"
        return self.tool or self.event

    def playback_status(self):
        """Describe the actual media session state without inferring playback."""
        if self.playing:
            return "PLAYING"
        if not self.has_media:
            return "NO SESSION"
        return {"paused": "PAUSED", "stopped": "STOPPED", "closed": "CLOSED"}.get(self.playback_state, "NOT PLAYING")

    def events(self):
        """Return the last bounded Codex event rows or the current event fallback."""
        items = self.codex.get("events") or []
        items = [item for item in items if isinstance(item, dict)][-4:]
        if not items and self.event:
            items = [self.codex]
        return items

class Canvas(AudioDrawing, MediaDrawing, CompositionDrawing):
    def __init__(self, native, width, height, context):
        """Fit the logical theme canvas into the exact output pixel dimensions.

        Args:
            native: Native Skia canvas owned by the current frame surface.
            width: Target frame width in pixels.
            height: Target frame height in pixels.
            context: Current per-frame ThemeContext; callers retain its ownership.
        """
        self.native, self.ctx = native, context
        self.compact = width / height < 2.0
        self.width = 480 if self.compact else 1280
        self.height = 480
        native.clear(color(context.base))
        self.scale = min(width / self.width, height / self.height)
        native.save()
        native.translate((width - self.width * self.scale) / 2, (height - self.height * self.scale) / 2)
        native.scale(self.scale, self.scale)
        native.clipRect(skia.Rect.MakeWH(self.width, self.height))

    def finish(self):
        """Restore the native canvas transform after the theme has finished drawing."""
        self.native.restore()

    def paint(self, tint, opacity=1, stroke=0):
        """Build an antialiased Skia fill or stroke paint.

        Args:
            tint: RGB hex color used by this drawing.
            opacity: Alpha fraction from transparent zero to opaque one.
            stroke: Outline thickness; zero selects a filled shape.
        """
        paint = skia.Paint(Color=color(tint, opacity), AntiAlias=True)
        if stroke:
            paint.setStyle(skia.Paint.kStroke_Style)
            paint.setStrokeWidth(stroke)
        return paint

    def rect(self, x, y, width, height, tint, radius=0, opacity=1, stroke=0):
        """Draw an optionally rounded rectangle with the supplied fill/stroke.

        Args:
            x: Horizontal origin in logical canvas units.
            y: Vertical origin in logical canvas units.
            width: Available width in logical canvas units; None uses the helper's default.
            height: Available height in logical canvas units.
            tint: RGB hex color used by this drawing.
            radius: Radius or corner radius in logical canvas units.
            opacity: Alpha fraction from transparent zero to opaque one.
            stroke: Outline thickness; zero selects a filled shape.
        """
        rect = skia.Rect.MakeXYWH(x, y, width, height)
        paint = self.paint(tint, opacity, stroke)
        if radius:
            self.native.drawRoundRect(rect, radius, radius, paint)
        else:
            self.native.drawRect(rect, paint)

    def line(self, x1, y1, x2, y2, tint, width=1, opacity=1):
        """Draw a line between two logical canvas coordinates.

        Args:
            x1: Horizontal start coordinate in logical canvas units.
            y1: Vertical start coordinate in logical canvas units.
            x2: Horizontal end coordinate in logical canvas units.
            y2: Vertical end coordinate in logical canvas units.
            tint: RGB hex color used by this drawing.
            width: Stroke thickness in logical canvas units.
            opacity: Alpha fraction from transparent zero to opaque one.
        """
        self.native.drawLine(x1, y1, x2, y2, self.paint(tint, opacity, width))

    def circle(self, x, y, radius, tint, opacity=1, stroke=0):
        """Draw a filled or stroked circle.

        Args:
            x: Horizontal origin in logical canvas units.
            y: Vertical origin in logical canvas units.
            radius: Radius or corner radius in logical canvas units.
            tint: RGB hex color used by this drawing.
            opacity: Alpha fraction from transparent zero to opaque one.
            stroke: Outline thickness; zero selects a filled shape.
        """
        self.native.drawCircle(x, y, radius, self.paint(tint, opacity, stroke))

    def polygon(self, points, tint, opacity=1):
        """Draw and close a polygon from the supplied logical points.

        Args:
            points: Ordered logical coordinate pairs forming the polygon.
            tint: RGB hex color used by this drawing.
            opacity: Alpha fraction from transparent zero to opaque one.
        """
        path = skia.Path()
        path.moveTo(*points[0])
        for point in points[1:]:
            path.lineTo(*point)
        path.close()
        self.native.drawPath(path, self.paint(tint, opacity))

    def gradient(self, x, y, width, height, colors, vertical=False, radius=0):
        """Draw a linear gradient in an optionally rounded rectangle.

        Args:
            x: Horizontal origin in logical canvas units.
            y: Vertical origin in logical canvas units.
            width: Available width in logical canvas units; None uses the helper's default.
            height: Available height in logical canvas units.
            colors: Ordered RGB gradient stops across the drawn rectangle.
            vertical: Whether the gradient runs top to bottom instead of left to right.
            radius: Radius or corner radius in logical canvas units.
        """
        end = (x, y + height) if vertical else (x + width, y)
        shader = skia.GradientShader.MakeLinear([(x, y), end], [color(c) for c in colors])
        paint = skia.Paint(Shader=shader, AntiAlias=True)
        self.native.drawRoundRect(skia.Rect.MakeXYWH(x, y, width, height), radius, radius, paint)

    def text(self, value, x, y, size=24, tint=None, width=None, align="left", bold=False):
        """Draw bounded text with cached fonts and the selected alignment.

        Args:
            value: Display text converted to a string and fitted to the supplied width.
            x: Horizontal origin in logical canvas units.
            y: Vertical origin in logical canvas units.
            size: Text size or square image size in logical canvas units.
            tint: Optional RGB hex override; None uses this drawing helper's semantic theme color.
            width: Available width in logical canvas units; None uses the helper's default.
            align: Text anchor: left, center or right.
            bold: Whether to select the bold cached font face.
        """
        value = clean(value)
        if not value:
            return
        size *= self.ctx.font_scale
        key = (self.ctx.font_family, round(size, 2), bold)
        if key not in _FONTS:
            style = skia.FontStyle.Bold() if bold else skia.FontStyle.Normal()
            face = skia.Typeface.MakeFromName(self.ctx.font_family, style)
            face = face or skia.Typeface.MakeFromName("monospace", style)
            _FONTS[key] = skia.Font(face, size)
            while len(_FONTS) > 96:
                _FONTS.popitem(last=False)
        font = _FONTS[key]
        if width is not None and font.measureText(value) > width:
            left, right = 0, len(value)
            while left < right:
                mid = (left + right + 1) // 2
                if font.measureText(value[:mid] + "…") <= width:
                    left = mid
                else:
                    right = mid - 1
            value = value[:left] + "…"
        measured = font.measureText(value)
        if align == "right":
            x -= measured
        elif align == "center":
            x -= measured / 2
        if not self.ctx.light and self.ctx.category == "multimedia" and MEDIA_TREATMENTS.get(self.ctx.style, {}).get("photo"):
            self.native.drawString(value, x + 1, y + 2, font, self.paint("#000000", .55))
        self.native.drawString(value, x, y, font, self.paint(tint or self.ctx.fg))

    def text_size(self, role="caption"):
        """Readable secondary type in logical pixels; titles keep their own scale.

        Floors intentionally protect instrument labels when fontScale is reduced.
        Caps reserve a predictable slot at the largest supported user setting.

        Args:
            role: Semantic text role used to select the theme's base text size.
        """
        base, minimum, maximum = {
            "instrument": (16, 16, 18),
            "caption": (18, 16, 22),
            "body": (20, 18, 26),
        }[role]
        return clip(base * self.ctx.font_scale, minimum, maximum) / self.ctx.font_scale

    def caption(self, value, x, y, tint=None, width=None, align="left", bold=False):
        """Draw text using the theme's caption role and scale.

        Args:
            value: Display text drawn with the caption role and current font scale.
            x: Horizontal origin in logical canvas units.
            y: Vertical origin in logical canvas units.
            tint: Optional RGB hex override; None uses this drawing helper's semantic theme color.
            width: Available width in logical canvas units; None uses the helper's default.
            align: Text anchor: left, center or right.
            bold: Whether to select the bold cached font face.
        """
        self.text(value, x, y, self.text_size("caption"), tint or self.ctx.muted,
                  width, align=align, bold=bold)

    def label(self, value, x, y, width=None, tint=None):
        """Draw a compact uppercase field label.

        Args:
            value: Field-label text converted to uppercase before drawing.
            x: Horizontal origin in logical canvas units.
            y: Vertical origin in logical canvas units.
            width: Available width in logical canvas units; None uses the helper's default.
            tint: Optional RGB hex override; None uses this drawing helper's semantic theme color.
        """
        self.caption(str(value).upper(), x, y, tint, width)

# VBWR E
