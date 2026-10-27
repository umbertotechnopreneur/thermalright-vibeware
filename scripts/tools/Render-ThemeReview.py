# VBWR B
#
# Project: Thermalright VibeWare
# Repository: https://github.com/umbertotechnopreneur/thermalright-vibeware
# Creator: Umberto Giacobbi | https://umbertogiacobbi.biz
#
# File: Render-ThemeReview.py
# Purpose: Renders explicitly requested theme review images without physical USB output.
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

"""One owner-approved, local 40-frame review. Never call from automated CI.

Creates twenty themes x light/dark, using only original synthetic fixtures.
No USB access, app/media queries, hook installation, or external network.
The calling interpreter needs the bundled theme-runtime requirements.
"""
from __future__ import annotations

import argparse
import base64
from collections import deque
import contextlib
import copy
import hashlib
import json
import os
from pathlib import Path
import queue
import re
import subprocess
import sys
import threading
import time

sys.dont_write_bytecode = True
with contextlib.redirect_stdout(sys.stderr):
    import skia

WIDTH, HEIGHT, MAX_JPEG = 1280, 480, 450_000
SAMPLING = skia.SamplingOptions(skia.FilterMode.kLinear)

def tint(value):
    """Parse the review fixture's hexadecimal RGB color.

    Args:
        value: Fixture RGB hex text converted into color channels.
    """
    value = value.lstrip("#")
    return skia.ColorSetRGB(*[int(value[index:index + 2], 16) for index in (0, 2, 4)])

def fixture_artwork():
    """An original geometric landscape drawn in memory, not an external image."""
    surface = skia.Surface(640, 640)
    canvas = surface.getCanvas()
    shader = skia.GradientShader.MakeLinear([(0, 0), (640, 640)],
        [tint("#101A43"), tint("#9279C5"), tint("#E5AC9D")])
    canvas.drawPaint(skia.Paint(Shader=shader))
    canvas.drawCircle(437, 186, 94, skia.Paint(Color=tint("#F9C694"), AntiAlias=True))
    for points, shade in (
        ([(0, 451), (176, 201), (399, 451)], "#7777B0"),
        ([(101, 505), (348, 173), (619, 505)], "#343F73"),
        ([(348, 173), (454, 356), (337, 300), (294, 340)], "#E6D8E9"),
        ([(0, 474), (260, 358), (640, 508), (640, 640), (0, 640)], "#162D4A"),
    ):
        path = skia.Path()
        path.moveTo(*points[0])
        for point in points[1:]:
            path.lineTo(*point)
        path.close()
        canvas.drawPath(path, skia.Paint(Color=tint(shade), AntiAlias=True))
    font = skia.Font(skia.Typeface.MakeFromName("monospace", skia.FontStyle.Normal()), 23)
    canvas.drawString("COLOR STUDY / DEMO", 36, 590, font, skia.Paint(Color=tint("#F0E9F5"), AntiAlias=True))
    encoded = surface.makeImageSnapshot().encodeToData(skia.EncodedImageFormat.kPNG, 100)
    if encoded is None:
        raise RuntimeError("Cannot create the synthetic artwork fixture.")
    raw = bytes(encoded)
    return hashlib.sha256(raw).hexdigest(), base64.b64encode(raw).decode("ascii")

def base_state(cover_key):
    """Build explicit offline review data without reading host media or sensors.

    Args:
        cover_key: Explicit fixture artwork identity used to reuse its decoded cover.
    """
    return {
        "preview": True,
        "media": {"title": "After the Blue Hour", "artist": "Demo Artist",
                  "album": "Generated Color Study", "source": "Demo media",
                  "playing": True, "positionSeconds": 94, "durationSeconds": 252,
                  "coverKey": cover_key, "shuffle": True, "quality": None},
        "audio": {"volumePercent": 65, "peak": .32,
                  "sessions": [{"name": "Demo media", "volumePercent": 65, "peak": .32, "muted": False}]},
        "clock": {"iso": "2026-10-16T21:42:24+07:00", "locale": "en-US"},
        "desktop": {"palette": ["#686DB4", "#B67EA7", "#529BA5", "#CAA57D"]},
        "codex": {"preview": True, "chatTitle": "Thermalright VibeWare · Theme engine",
                  "status": "Working on the theme", "event": "PreToolUse", "phase": "working",
                  "isLive": True, "stale": False, "atUtc": "2026-10-16T14:42:24Z",
                  "tool": "terminal", "model": "Demo model",
                  "events": [
                      {"event": "UserPromptSubmit", "status": "Request received", "atUtc": "2026-10-16T14:41:20Z"},
                      {"event": "PreToolUse", "status": "Reading the project", "tool": "terminal", "atUtc": "2026-10-16T14:41:30Z"},
                      {"event": "PostToolUse", "status": "Tool completed", "tool": "terminal", "atUtc": "2026-10-16T14:42:00Z"},
                      {"event": "PreToolUse", "status": "Working on the theme", "tool": "terminal", "atUtc": "2026-10-16T14:42:24Z"},
                  ]},
    }

def read_catalog(repo):
    """Read the repository's local theme pack definitions for the requested review.

    Args:
        repo: Repository root containing the first-party theme manifests.
    """
    result = []
    for pack, category in (("media", "multimedia"), ("codex", "codex")):
        root = (repo / "themes" / pack).resolve()
        package = json.loads((root / "pack.json").read_text(encoding="utf-8-sig"))
        if package.get("schema") != 1 or len(package.get("themes", [])) != 10:
            raise ValueError(f"Expected exactly ten schema-1 themes in {pack}.")
        for relative in package["themes"]:
            folder = (root / relative).resolve(strict=True)
            if not folder.is_relative_to(root):
                raise ValueError("Bundled theme path is outside its pack.")
            manifest = json.loads((folder / "theme.json").read_text(encoding="utf-8-sig"))
            if manifest.get("schema") != 1 or manifest.get("category") != category:
                raise ValueError("Theme category or schema does not match its pack.")
            result.append({"id": manifest["id"], "name": manifest["name"], "category": category, "folder": folder})
    if len(result) != 20 or len({theme["id"] for theme in result}) != 20:
        raise ValueError("Expected twenty distinct bundled themes.")
    return result

class ReviewWorker:
    def __init__(self, python, script, timeout):
        """Start one explicitly configured worker for offline review rendering.

        Args:
            python: Explicit Python executable used to start the review worker.
            script: Explicit repository worker script path.
            timeout: Maximum wait for one worker response in seconds.
        """
        self.timeout = timeout
        self.lines = queue.Queue()
        self.diagnostics = deque(maxlen=12)
        options = {"stdin": subprocess.PIPE, "stdout": subprocess.PIPE, "stderr": subprocess.PIPE,
                   "text": True, "encoding": "utf-8", "bufsize": 1,
                   "cwd": str(script.parent), "env": {**os.environ, "PYTHONIOENCODING": "utf-8", "PYTHONDONTWRITEBYTECODE": "1"}}
        if os.name == "nt":
            options["creationflags"] = subprocess.CREATE_NO_WINDOW
        self.process = subprocess.Popen([str(python), "-u", str(script)], **options)
        threading.Thread(target=self._read_stdout, daemon=True).start()
        threading.Thread(target=self._read_stderr, daemon=True).start()

    def _read_stdout(self):
        """Drain worker protocol lines into the review process's bounded response queue."""
        try:
            while True:
                line = self.process.stdout.readline(900_001)
                if not line:
                    self.lines.put(RuntimeError("Worker closed its output."))
                    return
                if len(line) > 900_000 or not line.endswith("\n"):
                    self.lines.put(RuntimeError("Worker returned an oversized or unterminated response."))
                    return
                self.lines.put(line)
        except Exception as exc:
            self.lines.put(exc)

    def _read_stderr(self):
        """Drain worker diagnostics without blocking the review worker's stderr pipe."""
        try:
            for line in self.process.stderr:
                self.diagnostics.append(line.rstrip()[:500])
        except (ValueError, OSError):
            pass

    def request(self, payload):
        """Send one bounded offline review request and wait for its correlated response.

        Args:
            payload: Untrusted hook input; only allow-listed activity fields are retained.
        """
        self.process.stdin.write(json.dumps(payload, ensure_ascii=True, separators=(",", ":")) + "\n")
        self.process.stdin.flush()
        try:
            line = self.lines.get(timeout=self.timeout)
        except queue.Empty as exc:
            raise TimeoutError(f"Worker exceeded {self.timeout:g}s; this review does not retry.") from exc
        if isinstance(line, Exception):
            raise line
        response = json.loads(line)
        if response.get("version") != 1 or response.get("id") != payload["id"]:
            raise ValueError("Worker version or response ID does not match.")
        return response

    def close(self):
        """Stop the owned review worker and release its pipes."""
        if self.process.poll() is None:
            self.process.terminate()
            try:
                self.process.wait(timeout=5)
            except subprocess.TimeoutExpired:
                self.process.kill()
                self.process.wait(timeout=5)
        for stream in (self.process.stdin, self.process.stdout, self.process.stderr):
            stream.close()

def draw_contacts(output, results):
    """Compose rendered theme review images into a labeled contact sheet.

    Args:
        output: Destination path for generated review images.
        results: Rendered theme review images and labels to arrange on the contact sheet.
    """
    sheets = []
    font = skia.Font(skia.Typeface.MakeFromName("monospace", skia.FontStyle.Normal()), 17)
    title_font = skia.Font(skia.Typeface.MakeFromName("monospace", skia.FontStyle.Bold()), 29)
    small_font = skia.Font(skia.Typeface.MakeFromName("monospace", skia.FontStyle.Normal()), 13)
    for category in ("multimedia", "codex"):
        for appearance in ("dark", "light"):
            items = [item for item in results if item["category"] == category and item["appearance"] == appearance]
            light = appearance == "light"
            background = tint("#E9EDF4" if light else "#0C111D")
            foreground = tint("#192236" if light else "#E9EDF8")
            muted = tint("#647087" if light else "#99AAC4")
            surface = skia.Surface(1280, 1480)
            canvas = surface.getCanvas()
            canvas.clear(background)
            canvas.drawString(f"THERMALRIGHT VIBEWARE / {category.upper()} / {appearance.upper()}", 24, 43, title_font, skia.Paint(Color=foreground, AntiAlias=True))
            canvas.drawString("DEMO DATA · 10 THEMES · 1280 x 480 EACH · NO LIVE APPS OR DEVICES", 24, 71, small_font, skia.Paint(Color=muted, AntiAlias=True))
            for index, item in enumerate(items):
                x = 24 + (index % 2) * 628
                y = 101 + (index // 2) * 273
                canvas.drawString(f"{index + 1:02} / {item['name']}", x, y + 18, font, skia.Paint(Color=foreground, AntiAlias=True))
                rect = skia.Rect.MakeXYWH(x, y + 30, 604, 226.5)
                if item["ok"]:
                    raw = (output / item["file"]).read_bytes()
                    image = skia.Image.MakeFromEncoded(skia.Data.MakeWithCopy(raw))
                    canvas.drawImageRect(image, rect, SAMPLING)
                else:
                    canvas.drawRect(rect, skia.Paint(Color=tint("#6C354B"), AntiAlias=True))
                    canvas.drawString("RENDER FAILED — see summary.json", x + 20, y + 143, font, skia.Paint(Color=tint("#FFFFFF"), AntiAlias=True))
            filename = f"contact-{category}-{appearance}.png"
            png = surface.makeImageSnapshot().encodeToData(skia.EncodedImageFormat.kPNG, 100)
            if png is None:
                raise RuntimeError("Cannot encode contact sheet.")
            with (output / filename).open("xb") as handle:
                handle.write(bytes(png))
            sheets.append(filename)
    return sheets

def main():
    """Render the explicitly selected offline theme fixtures into review artifacts."""
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--repo", type=Path, default=Path(__file__).resolve().parents[2])
    parser.add_argument("--output", type=Path)
    parser.add_argument("--python", type=Path, default=Path(sys.executable))
    parser.add_argument("--timeout", type=float, default=20)
    parser.add_argument("--compare-quality", action="store_true", help="Add three Aurora JPEG samples: Low, Medium and High. Requires owner approval too.")
    args = parser.parse_args()
    repo = args.repo.resolve(strict=True)
    output = (args.output or repo / "artifacts" / "theme-review").resolve()
    if args.timeout <= 0 or args.timeout > 60:
        raise ValueError("Timeout must be between 0 and 60 seconds.")
    if output.exists() and any(output.iterdir()):
        raise ValueError("Review output must be empty; existing review artifacts are never overwritten.")
    catalog = read_catalog(repo)
    output.mkdir(parents=True, exist_ok=True)
    cover_key, cover_base64 = fixture_artwork()
    fixture = base_state(cover_key)
    results, attempted, cover_sent = [], 0, False
    quality_samples = []
    worker = ReviewWorker(args.python, repo / "theme-runtime" / "worker.py", args.timeout)
    fatal = None
    started = time.monotonic()
    try:
        for theme in catalog:
            for appearance in ("dark", "light"):
                state = copy.deepcopy(fixture)
                if theme["category"] == "codex":
                    state["media"], state["audio"] = {}, {}
                    if theme["id"] == "codex.approval":
                        state["codex"].update(status="Approval requested", event="PermissionRequest", phase="waiting", tool="terminal")
                    elif theme["id"] in ("codex.focus", "codex.quiet"):
                        state["codex"].update(status="Response ready", event="Stop", phase="ready", isLive=False, tool="")
                elif not cover_sent:
                    state["media"]["coverBase64"] = cover_base64
                request_id = f"{theme['id']}-{appearance}"
                payload = {"version": 1, "id": request_id, "method": "render",
                           "themePath": str(theme["folder"]), "width": WIDTH, "height": HEIGHT,
                           "light": appearance == "light", "fontFamily": "Cascadia Mono", "fontScale": 1.0,
                           "ambientDrift": False, "state": state}
                attempted += 1
                frame_started = time.monotonic()
                response = worker.request(payload)
                item = {"id": theme["id"], "name": theme["name"], "category": theme["category"],
                        "appearance": appearance, "ok": False, "milliseconds": round((time.monotonic() - frame_started) * 1000, 2)}
                if response.get("ok") is not True:
                    item["error"] = response.get("error", "Worker reported a render failure.")
                else:
                    raw = base64.b64decode(response["jpegBase64"], validate=True)
                    if len(raw) > MAX_JPEG or not raw.startswith(b"\xff\xd8") or not raw.endswith(b"\xff\xd9"):
                        item["error"] = "Invalid or oversized JPEG."
                    else:
                        image = skia.Image.MakeFromEncoded(skia.Data.MakeWithCopy(raw))
                        if image is None or image.width() != WIDTH or image.height() != HEIGHT:
                            item["error"] = "JPEG dimensions do not match 1280 x 480."
                        else:
                            filename = re.sub(r"[^A-Za-z0-9._-]", "_", request_id) + ".jpg"
                            with (output / filename).open("xb") as handle:
                                handle.write(raw)
                            item.update(ok=True, file=filename, bytes=len(raw), width=image.width(), height=image.height())
                            if theme["category"] == "multimedia":
                                cover_sent = True
                results.append(item)
                print(f"[{attempted:02}/40] {request_id}: {'OK' if item['ok'] else item.get('error')}", file=sys.stderr, flush=True)
        if args.compare_quality:
            sample_theme = next(theme for theme in catalog if theme["id"] == "media.aurora")
            for quality in ("low", "medium", "high"):
                state = copy.deepcopy(fixture)
                state["media"]["coverBase64"] = cover_base64
                request_id = f"quality-{quality}"
                payload = {"version": 1, "id": request_id, "method": "render",
                           "themePath": str(sample_theme["folder"]), "width": WIDTH, "height": HEIGHT,
                           "light": False, "fontFamily": "Cascadia Mono", "fontScale": 1.0,
                           "ambientDrift": False, "jpegQuality": quality, "state": state}
                response = worker.request(payload)
                item = {"quality": quality, "ok": False}
                if response.get("ok") is not True:
                    item["error"] = response.get("error", "JPEG quality sample failed.")
                else:
                    raw = base64.b64decode(response["jpegBase64"], validate=True)
                    image = skia.Image.MakeFromEncoded(skia.Data.MakeWithCopy(raw))
                    if len(raw) > MAX_JPEG or not raw.startswith(b"\xff\xd8") or not raw.endswith(b"\xff\xd9") or image is None or image.width() != WIDTH or image.height() != HEIGHT:
                        item["error"] = "Invalid JPEG quality sample."
                    else:
                        filename = request_id + ".jpg"
                        with (output / filename).open("xb") as handle:
                            handle.write(raw)
                        item.update(ok=True, file=filename, bytes=len(raw))
                quality_samples.append(item)
                print(f"[quality] {quality}: {'OK' if item['ok'] else item.get('error')}", file=sys.stderr, flush=True)
    except Exception as exc:
        fatal = f"{type(exc).__name__}: {exc}"
    finally:
        worker.close()
    passed = sum(item["ok"] for item in results)
    summary = {"approvedScope": "One local 40-frame synthetic review; no USB, live media or hooks.",
               "expected": 40, "attempted": attempted, "passed": passed,
               "complete": attempted == 40 and len(results) == 40 and passed == 40,
               "elapsedSeconds": round(time.monotonic() - started, 3), "output": str(output),
               "fatalError": fatal, "workerDiagnostics": list(worker.diagnostics), "frames": results,
               "contactSheets": [], "qualitySamples": quality_samples}
    if args.compare_quality and (len(quality_samples) != 3 or not all(item["ok"] for item in quality_samples)):
        summary["complete"] = False
    if len(results) == 40:
        try:
            summary["contactSheets"] = draw_contacts(output, results)
        except Exception as exc:
            summary["contactSheetError"] = f"{type(exc).__name__}: {exc}"
            summary["complete"] = False
    with (output / "summary.json").open("x", encoding="utf-8") as handle:
        json.dump(summary, handle, ensure_ascii=False, indent=2)
    print(json.dumps(summary, ensure_ascii=True, separators=(",", ":")))
    return 0 if summary["complete"] else 1

if __name__ == "__main__":
    raise SystemExit(main())
# VBWR E
