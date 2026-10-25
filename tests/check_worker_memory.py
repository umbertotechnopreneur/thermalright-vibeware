# VBWR B
# Project: ThermalrightVibeWare | https://umbertogiacobbi.biz/vibeware/manifesto
# Copyright (c) 2026 Umberto Giacobbi | SPDX-License-Identifier: MIT
# VBWR E
"""Owner-approved Windows memory check. Synthetic frames only; no USB or media APIs."""
import base64
import copy
import ctypes
from ctypes import wintypes
import importlib.util
import json
from pathlib import Path
import sys
import threading
import time

sys.dont_write_bytecode = True
repo = Path(__file__).resolve().parents[1]
spec = importlib.util.spec_from_file_location("memory_fixtures", repo / "scripts/tools/Render-ThemeReview.py")
fixtures = importlib.util.module_from_spec(spec)
spec.loader.exec_module(fixtures)


class MemoryCounters(ctypes.Structure):
    _fields_ = [("cb", wintypes.DWORD), ("pageFaults", wintypes.DWORD)] + [
        (name, ctypes.c_size_t) for name in ("peakWorkingSet", "workingSet", "peakPaged", "paged",
            "peakNonPaged", "nonPaged", "pagefile", "peakPagefile", "private")]


get_memory = ctypes.WinDLL("psapi", use_last_error=True).GetProcessMemoryInfo
get_memory.argtypes = [wintypes.HANDLE, ctypes.POINTER(MemoryCounters), wintypes.DWORD]
get_memory.restype = wintypes.BOOL


def main():
    worker = fixtures.ReviewWorker(Path(sys.executable), repo / "theme-runtime/worker.py", 20)
    stop = threading.Event()
    peak = 0
    failed = []
    reports = []

    def measure():
        nonlocal peak
        counters = MemoryCounters()
        counters.cb = ctypes.sizeof(counters)
        if not get_memory(int(worker.process._handle), ctypes.byref(counters), counters.cb):
            raise ctypes.WinError(ctypes.get_last_error())
        peak = max(peak, counters.private)
        return counters.private

    def sample():
        while not stop.wait(.05):
            try:
                if measure() > 800_000_000:
                    failed.append("Worker reached the offline check's 800 MB stop threshold.")
                    worker.process.terminate()
                    return
            except OSError:
                return

    sampler = threading.Thread(target=sample, daemon=True)
    sampler.start()

    def report(phase, frames):
        entry = {"phase": phase, "frames": frames, "privateBytes": measure(), "peakPrivateBytes": peak}
        reports.append(entry)
        print(json.dumps(entry), flush=True)

    try:
        for index in range(5):
            result = worker.request({"version": 1, "id": f"fonts-{index}", "method": "fonts"})
            if result.get("ok") is not True:
                raise RuntimeError(result.get("error", "Font request failed."))
        report("fonts-repeated", 0)
        catalog = fixtures.read_catalog(repo)
        cover_key, cover = fixtures.fixture_artwork()
        state = fixtures.base_state(cover_key)
        frames = 0
        warmup = None
        for cycle in range(10):
            for theme in catalog:
                for light in (False, True):
                    current = copy.deepcopy(state)
                    current["media"]["coverBase64"] = cover
                    # Exercise artwork eviction without reading user images.
                    current["media"]["coverKey"] = cover_key + str(cycle)
                    current["media"]["positionSeconds"] += frames
                    width, height = ((1280, 480), (480, 1280), (480, 480))[cycle % 3]
                    response = worker.request({"version": 1, "id": str(frames), "method": "render",
                        "themePath": str(theme["folder"]), "width": width, "height": height,
                        "light": light, "fontFamily": ("Cascadia Mono", "Consolas", "Courier New")[cycle % 3],
                        "fontScale": .8 if light else 1.2, "state": current, "ambientDrift": True,
                        "jpegQuality": ("poor", "low", "medium", "high")[cycle % 4],
                        "colorFiltersEnabled": cycle % 2 == 0, "saturationPercent": 120,
                        "hueDegrees": 20, "brightnessPercent": 10, "contrastPercent": 110})
                    if response.get("ok") is not True:
                        raise RuntimeError(response.get("error", "Render failed."))
                    raw = base64.b64decode(response["jpegBase64"], validate=True)
                    if len(raw) > 450_000 or not raw.startswith(b"\xff\xd8"):
                        raise RuntimeError("Invalid or oversized JPEG response.")
                    frames += 1
            report(f"theme-cycle-{cycle + 1}", frames)
            if cycle == 4:
                warmup = measure()
        response = worker.request({"version": 1, "id": "error", "method": "unsupported"})
        if response.get("ok") is not False:
            raise RuntimeError("Invalid request was accepted.")
        response = worker.request({"version": 1, "id": "recovery", "method": "fonts"})
        if response.get("ok") is not True:
            raise RuntimeError("Worker did not recover after an invalid request.")
        report("error-and-recovery", frames)
        if failed or measure() > warmup + 64 * 1024 * 1024:
            raise RuntimeError(failed[0] if failed else "Worker retained over 64 MiB beyond warmup.")
        print("Worker memory paths passed: 400 synthetic frames, all 20 themes, fonts, filters, artwork eviction and recovery.")
    finally:
        stop.set()
        sampler.join(timeout=2)
        worker.close()
        output = repo / "artifacts/memory-checks"
        output.mkdir(parents=True, exist_ok=True)
        (output / "worker-memory.json").write_text(json.dumps({"reports": reports, "peakPrivateBytes": peak, "failures": failed}, indent=2), encoding="utf-8")


if __name__ == "__main__":
    main()
