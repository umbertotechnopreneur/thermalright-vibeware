# VBWR B
#
# Project: Thermalright VibeWare
# Repository: https://github.com/umbertotechnopreneur/thermalright-vibeware
# Creator: Umberto Giacobbi | https://umbertogiacobbi.biz
#
# File: worker.py
# Purpose: Handles bounded JSON-lines requests and executes the selected local Python theme.
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

"""Persistent, offscreen theme worker. stdin/stdout are JSON Lines protocol only."""
from __future__ import annotations

import base64
import contextlib
import hashlib
import importlib.util
import json
import math
import os
import pathlib
import sys

sys.dont_write_bytecode = True
# Tiny palette arrays do not need NumPy's large default native thread pool.
# Set this before canvas imports NumPy; it applies only to this worker process.
os.environ["OPENBLAS_NUM_THREADS"] = "1"
os.environ["OMP_NUM_THREADS"] = "1"
# Windows embeddable Python may omit the script folder from its isolated path.
sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))
MAX_LINE = 20_000_000
MAX_JPEG = 450_000
MODULES: dict[str, tuple[int, object]] = {}
IMPORT_ERROR = None
try:
    with contextlib.redirect_stdout(sys.stderr):
        import skia
        from canvas import Canvas, ThemeContext
        from color_adjustments import apply_color_adjustments
        from font_catalog import list_monospace_fonts
        from frame_transition import FrameTransition
        from theme_pages import select_page
        FRAME_TRANSITION = FrameTransition()
except ImportError as exc:
    IMPORT_ERROR = str(exc)

def load_theme(folder: pathlib.Path):
    """Validate the manifest entry path and cache the local theme module by modification time.

    Args:
        folder: Resolved local folder containing the selected theme manifest and entry point.
    """
    manifest = json.loads((folder / "theme.json").read_text(encoding="utf-8-sig"))
    if manifest.get("schema") != 1:
        raise ValueError("This theme needs an unsupported manifest version.")
    entry = (folder / manifest.get("entry", "render.py")).resolve()
    if not entry.is_relative_to(folder) or entry.suffix.lower() != ".py":
        raise ValueError("Theme entry must be a Python file inside its theme folder.")
    stamp = entry.stat().st_mtime_ns
    key = str(entry)
    if key not in MODULES or MODULES[key][0] != stamp:
        name = "thermalright-vibeware_theme_" + hashlib.sha256(key.encode()).hexdigest()[:16]
        spec = importlib.util.spec_from_file_location(name, entry)
        if spec is None or spec.loader is None:
            raise ValueError("Theme entry cannot be loaded.")
        module = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(module)
        if not callable(getattr(module, "render", None)):
            raise ValueError("Theme must define render(canvas, context).")
        if len(MODULES) >= 32:
            MODULES.pop(next(iter(MODULES)))
        MODULES[key] = (stamp, module)
    return manifest, MODULES[key][1]

def render(request: dict) -> dict:
    """Render one local theme into a bounded JPEG response without USB access.

    Args:
        request: Validated worker request with dimensions, controls and theme selection.
    """
    if IMPORT_ERROR:
        raise RuntimeError("Python theme dependencies are missing. Install theme-runtime/requirements.txt in the configured Python runtime. " + IMPORT_ERROR)
    if request.get("version", 1) != 1 or request.get("method", "render") != "render":
        raise ValueError("Unsupported theme worker request.")
    if request.get("paletteVersion", 1) != 1:
        raise ValueError("Unsupported palette version.")
    # Reject invalid controls before importing a theme. The resolved theme overrides
    # are checked again by the native filter helper after render/finish.
    for name, default, minimum, maximum in (
        ("saturationPercent", 100, 0, 200),
        ("hueDegrees", 0, -180, 180),
        ("brightnessPercent", 0, -100, 100),
        ("contrastPercent", 100, 0, 200),
    ):
        value = request.get(name, default)
        if isinstance(value, bool) or not isinstance(value, (int, float)):
            raise ValueError(f"{name} must be a finite number from {minimum} to {maximum}.")
        try:
            value = float(value)
        except (OverflowError, ValueError):
            raise ValueError(f"{name} must be a finite number from {minimum} to {maximum}.") from None
        if not math.isfinite(value) or not minimum <= value <= maximum:
            raise ValueError(f"{name} must be a finite number from {minimum} to {maximum}.")
    if not isinstance(request.get("showSpectrum", True), bool):
        raise ValueError("showSpectrum must be a boolean.")
    page_index = request.get("pageIndex", 0)
    page_revision = request.get("pageRevision", 0)
    if (isinstance(page_index, bool) or not isinstance(page_index, int) or not 0 <= page_index <= 7
            or isinstance(page_revision, bool) or not isinstance(page_revision, int) or page_revision < 0):
        raise ValueError("Invalid theme page selection.")
    filters_enabled = request.get("colorFiltersEnabled", True)
    if not isinstance(filters_enabled, bool):
        raise ValueError("colorFiltersEnabled must be a boolean.")
    quality_name = request.get("jpegQuality", "high")
    if quality_name not in ("poor", "low", "medium", "high"):
        raise ValueError("JPEG quality must be poor, low, medium, or high.")
    target_quality = {"poor": 20, "low": 50, "medium": 75, "high": 90}[quality_name]
    width, height = int(request.get("width", 1280)), int(request.get("height", 480))
    if not (128 <= width <= 4096 and 128 <= height <= 4096 and width * height <= 8_388_608):
        raise ValueError("Canvas dimensions must be 128–4096 pixels and at most 8 megapixels.")
    scale = float(request.get("fontScale", 1))
    if not math.isfinite(scale):
        raise ValueError("Font scale must be finite.")
    # Resolve local theme code only after validating controls and the output allocation limit.
    folder = pathlib.Path(request["themePath"]).resolve(strict=True)
    manifest, module = load_theme(folder)
    state = request.get("state") or {}
    if not isinstance(state, dict):
        raise ValueError("Theme state must be an object.")
    # One context and one canvas belong to this frame; expensive reusable layers stay cached.
    context = ThemeContext(state, request, manifest)
    page_id = select_page(context, manifest)
    surface = skia.Surface(width, height)
    if surface is None:
        raise RuntimeError("Cannot allocate the theme canvas.")
    canvas = Canvas(surface.getCanvas(), width, height, context)
    module.render(canvas, context)
    canvas.finish()
    # A fresh context starts from the UI/CLI controls on each frame. Themes may
    # override context.colors; those values do not change the host's controls.
    snapshot = surface.makeImageSnapshot()
    transition_active = False
    if context.category == "multimedia" or manifest.get("pages"):
        scope = (str(folder), manifest.get("id"), width, height, context.light,
                 context.font_family, context.font_scale, tuple(context.palette.to_json().items()))
        track = (page_id, context.source, context.title, context.artist, context.album, context.cover_key) if context.category == "multimedia" else (page_id,)
        snapshot, transition_active = FRAME_TRANSITION.compose(snapshot, scope, track)
    else:
        FRAME_TRANSITION.reset()
    # Off means no color surface, matrix or raster pass, including theme overrides.
    # Keep the saved settings in the context so enabling filters restores them.
    if filters_enabled:
        colors = context.colors
        snapshot = apply_color_adjustments(snapshot, colors.saturation_percent,
                                          colors.hue_degrees, colors.brightness_percent, colors.contrast_percent)
    # Keep the physical profile resolution exact; reduce JPEG quality, never resize.
    qualities = (target_quality,) + tuple(value for value in (82, 72, 60, 45, 30, 18, 8) if value < target_quality)
    for quality in qualities:
        data = snapshot.encodeToData(skia.EncodedImageFormat.kJPEG, quality)
        if data is not None and data.size() <= MAX_JPEG:
            return {"version": 1, "id": request.get("id"), "ok": True,
                    "jpegBase64": base64.b64encode(bytes(data)).decode("ascii"),
                    "accentColor": context.accent, "transitionActive": transition_active,
                    "pageId": context.page_id, "pageTitle": context.page_title,
                    "pageIndex": context.page_index, "pageCount": context.page_count}
    raise ValueError("Rendered frame exceeds the panel's 450,000-byte JPEG limit.")

def dispatch(request: dict) -> dict:
    """Route supported worker requests without mixing diagnostics into protocol stdout.

    Args:
        request: Validated worker request with dimensions, controls and theme selection.
    """
    if request.get("version", 1) != 1:
        raise ValueError("Unsupported theme worker request.")
    method = request.get("method", "render")
    if method == "fonts":
        if IMPORT_ERROR:
            raise RuntimeError("Python theme dependencies are missing. Install theme-runtime/requirements.txt in the configured Python runtime. " + IMPORT_ERROR)
        result = list_monospace_fonts()
        return {"version": 1, "id": request.get("id"), "ok": True,
                "families": result["families"], "notice": result.get("notice")}
    if method == "render":
        return render(request)
    raise ValueError("Unsupported theme worker method.")

def main():
    """Read bounded stdio requests and keep theme output off protocol stdout."""
    if hasattr(sys.stdin, "reconfigure"):
        sys.stdin.reconfigure(encoding="utf-8")
        sys.stdout.reconfigure(encoding="utf-8", write_through=True)
    while True:
        line = sys.stdin.readline(MAX_LINE + 1)
        if not line:
            break
        request = {}
        try:
            if len(line) > MAX_LINE:
                while line and not line.endswith("\n"):
                    line = sys.stdin.readline(MAX_LINE + 1)
                raise ValueError("Theme request is too large.")
            request = json.loads(line)
            if not isinstance(request, dict):
                request = {}
                raise ValueError("Theme request must be an object.")
            with contextlib.redirect_stdout(sys.stderr):
                result = dispatch(request)
        except Exception as exc:
            result = {"version": 1, "id": request.get("id"), "ok": False,
                      "error": f"{type(exc).__name__}: {exc}"[:1000]}
        sys.stdout.write(json.dumps(result, ensure_ascii=True, separators=(",", ":")) + "\n")
        sys.stdout.flush()

if __name__ == "__main__":
    main()
# VBWR E
