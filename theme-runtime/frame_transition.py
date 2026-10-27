# VBWR B
#
# Project: Thermalright VibeWare
# Repository: https://github.com/umbertotechnopreneur/thermalright-vibeware
# Creator: Umberto Giacobbi | https://umbertogiacobbi.biz
#
# File: frame_transition.py
# Purpose: Composes cached track/theme transitions with bounded in-memory images.
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

"""Blend a changed page or track from the last visible frame; retain no frame queue."""
from __future__ import annotations

import time

import skia

class FrameTransition:
    DURATION_SECONDS = 2.0

    def __init__(self, clock=time.monotonic):
        """Initialize an empty bounded transition with the supplied monotonic clock.

        Args:
            clock: Monotonic clock callback used to time image transitions.
        """
        self.clock = clock
        self.reset()

    def reset(self):
        """Release cached transition images and clear the active transition scope."""
        self.scope = None
        self.track = None
        self.previous = None
        self.origin = None
        self.started = 0.0

    def compose(self, image, scope, track):
        """Cross-fade real images when the track/page changes within the same scope.

        Args:
            image: Current rendered Skia image retained only for the bounded transition.
            scope: Identity of the theme, dimensions, appearance and controls for transition reuse.
            track: Identity of the current track/page; a change starts a real-image transition.
        """
        if scope != self.scope:
            self.reset()
            self.scope = scope
        now = self.clock()
        if self.track is not None and track != self.track and self.previous is not None:
            # A second skip starts from what was visible, not an obsolete target.
            self.origin = self.previous
            self.started = now
        self.track = track
        if self.origin is not None:
            progress = max(0.0, (now - self.started) / self.DURATION_SECONDS)
            if progress >= 1:
                self.origin = None
            elif progress == 0:
                image = self.origin
            else:
                # Smooth the endpoints, then interpolate premultiplied RGB/alpha
                # in Skia. No JPEG decoding, Python pixel loop or GPU surface.
                progress = progress * progress * (3 - 2 * progress)
                surface = skia.Surface.MakeRaster(image.imageInfo())
                if surface is None:
                    raise RuntimeError("Cannot allocate the transition canvas.")
                native = surface.getCanvas()
                native.drawImage(self.origin, 0, 0, skia.SamplingOptions(),
                                 skia.Paint(Alphaf=1 - progress, BlendMode=skia.BlendMode.kSrc))
                native.drawImage(image, 0, 0, skia.SamplingOptions(),
                                 skia.Paint(Alphaf=progress, BlendMode=skia.BlendMode.kPlus))
                image = surface.makeImageSnapshot()
        self.previous = image
        return image, self.origin is not None
# VBWR E
