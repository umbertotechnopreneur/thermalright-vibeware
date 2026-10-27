# VBWR B
#
# Project: Thermalright VibeWare
# Repository: https://github.com/umbertotechnopreneur/thermalright-vibeware
# Creator: Umberto Giacobbi | https://umbertogiacobbi.biz
#
# File: color_adjustments.py
# Purpose: Validates and applies the single final Skia color filter pass.
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

"""Full-frame native Skia color correction, before JPEG encoding.

Matrix definitions follow the public W3C filter-effects specification:
https://www.w3.org/TR/filter-effects-1/#feColorMatrixElement
Native API: https://skia-python.github.io/skia-python/reference/skia.ColorFilters.html
Normalized matrix bias: SkMatrixColorFilter::appendStages uses matrix_4x5 then
clamp_01, not a byte-domain bias. See the upstream implementation:
https://github.com/google/skia/blob/main/src/effects/colorfilters/SkMatrixColorFilter.cpp
"""
from __future__ import annotations

import math
from dataclasses import dataclass
from functools import lru_cache

import skia

def _finite_setting(value: float, name: str, minimum: float, maximum: float) -> float:
    """Convert and validate one finite runtime filter setting.

    Args:
        value: Runtime setting to validate as a finite number within the supplied bounds.
        name: Display heading or setting/reading name used by this helper.
        minimum: Minimum allowed runtime setting value.
        maximum: Upper setting bound, scale limit or item cap used by this helper.
    """
    if isinstance(value, bool) or not isinstance(value, (int, float)):
        raise ValueError(f"{name} must be a finite number from {minimum:g} to {maximum:g}.")
    try:
        result = float(value)
    except (OverflowError, ValueError):
        raise ValueError(f"{name} must be a finite number from {minimum:g} to {maximum:g}.") from None
    if not math.isfinite(result) or not minimum <= result <= maximum:
        raise ValueError(f"{name} must be a finite number from {minimum:g} to {maximum:g}.")
    return result

@dataclass
class ColorAdjustments:
    """Mutable per-frame settings; themes can read or override individual fields."""

    saturation_percent: float = 100
    hue_degrees: float = 0
    brightness_percent: float = 0
    contrast_percent: float = 100

    def validate(self) -> ColorAdjustments:
        """Validate all four fields, normalize to floats and return this object.

        Assignment stays simple for theme authors. Validation is explicit on input
        and repeated by the final filter after theme overrides. Invalid values are
        rejected, never silently clamped. No field changes if any value is invalid.
        """
        values = (
            _finite_setting(self.saturation_percent, "Saturation", 0, 200),
            _finite_setting(self.hue_degrees, "Hue", -180, 180),
            _finite_setting(self.brightness_percent, "Brightness", -100, 100),
            _finite_setting(self.contrast_percent, "Contrast", 0, 200),
        )
        (self.saturation_percent, self.hue_degrees,
         self.brightness_percent, self.contrast_percent) = values
        return self

    def fade(self, amount: float) -> ColorAdjustments:
        """Fade the current color correction toward black; preserve alpha.

        Zero gives black, one keeps the current correction. Call once per frame
        after other color overrides. The final native filter still runs only once.

        Args:
            amount: Interpolation/fade fraction, with zero retaining the original value.
        """
        amount = _finite_setting(amount, "Fade", 0, 1)
        self.validate()
        self.contrast_percent *= amount
        self.brightness_percent = amount * (self.brightness_percent + 50) - 50
        return self

@lru_cache(maxsize=64)
def _color_filter(saturation_percent: float, hue_degrees: float,
                  brightness_percent: float, contrast_percent: float) -> skia.ColorFilter:
    """Build the cached Skia color matrix for the supplied runtime settings.

    Args:
        saturation_percent: Saturation from 0 to 200 percent; 100 is neutral.
        hue_degrees: Hue rotation from -180 to 180 degrees; zero is neutral.
        brightness_percent: Brightness offset from -100 to 100 percent; zero is neutral.
        contrast_percent: Contrast from 0 to 200 percent; 100 is neutral.
    """
    saturation = saturation_percent / 100.0
    contrast = contrast_percent / 100.0
    # Contrast pivots around 50% gray, then brightness adds a normalized offset.
    # Skia's matrix bias uses 0..1 channel units, not 0..255 byte units.
    bias = .5 * (1 - contrast) + brightness_percent / 100.0
    luminance = (.213, .715, .072)
    # Saturation is a linear interpolation/extrapolation from neutral luminance.
    saturate = tuple(tuple(luminance[column] * (1 - saturation)
                           + (saturation if row == column else 0)
                           for column in range(3)) for row in range(3))
    angle = math.radians(hue_degrees)
    cosine, sine = math.cos(angle), math.sin(angle)
    hue = (
        (.213 + .787 * cosine - .213 * sine,
         .715 - .715 * cosine - .715 * sine,
         .072 - .072 * cosine + .928 * sine),
        (.213 - .213 * cosine + .143 * sine,
         .715 + .285 * cosine + .140 * sine,
         .072 - .072 * cosine - .283 * sine),
        (.213 - .213 * cosine - .787 * sine,
         .715 - .715 * cosine + .715 * sine,
         .072 + .928 * cosine + .072 * sine),
    )
    # Compose once in coefficient space, not two clamped intermediate images.
    # These bounded 3x3 operations do not inspect or loop over frame pixels.
    matrix = []
    for row in range(3):
        coefficients = list(luminance) if saturation == 0 else [
            sum(hue[row][index] * saturate[index][column] for index in range(3))
            for column in range(3)
        ]
        # Saturation/hue keep neutral grays neutral before tone adjustment.
        coefficients[2] = 1.0 - coefficients[0] - coefficients[1]
        coefficients = [value * contrast for value in coefficients]
        matrix.extend((*coefficients, 0.0, bias))
    matrix.extend((0.0, 0.0, 0.0, 1.0, 0.0))
    result = skia.ColorFilters.Matrix(matrix)
    if result is None:
        raise RuntimeError("Cannot create the native color filter.")
    return result

def apply_color_adjustments(snapshot: skia.Image, saturation_percent: float,
                            hue_degrees: float, brightness_percent: float = 0,
                            contrast_percent: float = 100) -> skia.Image:
    """Return the same image for 100/0/0/100, otherwise one native raster pass.

    Saturation accepts 0–200%; hue -180–180°; brightness -100–100%; contrast
    0–200%. RGB grays stay neutral and alpha is unchanged. Contrast pivots at
    50% gray; brightness is added last. Out-of-gamut RGB is clamped by Skia.
    No frame is resized, no pixels are copied into Python, and no files are made.

    Args:
        snapshot: Native Skia frame image before the final color pass.
        saturation_percent: Saturation from 0 to 200 percent; 100 is neutral.
        hue_degrees: Hue rotation from -180 to 180 degrees; zero is neutral.
        brightness_percent: Brightness offset from -100 to 100 percent; zero is neutral.
        contrast_percent: Contrast from 0 to 200 percent; 100 is neutral.
    """
    settings = ColorAdjustments(saturation_percent, hue_degrees,
                                brightness_percent, contrast_percent).validate()
    if (settings.saturation_percent == 100 and settings.hue_degrees == 0
            and settings.brightness_percent == 0 and settings.contrast_percent == 100):
        return snapshot
    if not isinstance(snapshot, skia.Image):
        raise TypeError("Color adjustment requires a Skia image snapshot.")
    surface = skia.Surface.MakeRaster(snapshot.imageInfo())
    if surface is None:
        raise RuntimeError("Cannot allocate the color-adjustment surface.")
    paint = skia.Paint()
    paint.setColorFilter(_color_filter(settings.saturation_percent, settings.hue_degrees,
                                       settings.brightness_percent, settings.contrast_percent))
    paint.setBlendMode(skia.BlendMode.kSrc)
    native = surface.getCanvas()
    native.clear(skia.ColorTRANSPARENT)
    native.drawImage(snapshot, 0, 0, skia.SamplingOptions(), paint)
    result = surface.makeImageSnapshot()
    if result is None:
        raise RuntimeError("Cannot create the adjusted image snapshot.")
    return result
# VBWR E
