# VBWR B
# TermalRight VibeWare | Umberto Giacobbi | SPDX-License-Identifier: MIT
# Explicit Linux stdio preview check. No USB, audio capture or installation.
# VBWR E
import argparse
import base64
import json
import os
import pathlib
import signal
import subprocess


def jpeg_size(data):
    if not data.startswith(b"\xff\xd8") or not data.endswith(b"\xff\xd9"):
        raise AssertionError("The preview is not a complete JPEG")
    offset = 2
    while offset < len(data):
        if data[offset] != 0xFF:
            raise AssertionError("Invalid JPEG marker")
        while data[offset] == 0xFF:
            offset += 1
        marker = data[offset]
        offset += 1
        size = int.from_bytes(data[offset:offset + 2], "big")
        if marker in (0xC0, 0xC1, 0xC2):
            return (int.from_bytes(data[offset + 5:offset + 7], "big"),
                    int.from_bytes(data[offset + 3:offset + 5], "big"))
        if size < 2:
            raise AssertionError("Invalid JPEG segment")
        offset += size
    raise AssertionError("Missing JPEG dimensions")


def main():
    parser = argparse.ArgumentParser(description="Check an explicitly selected Linux engine and its packaged renderer.")
    parser.add_argument("engine", type=pathlib.Path)
    parser.add_argument("--include-sensors", action="store_true", help="Also render System themes, reading local kernel sensors.")
    parser.add_argument("--sample", type=pathlib.Path, help="Save one generated JPEG for visual inspection.")
    args = parser.parse_args()
    engine = args.engine.resolve(strict=True)
    def timeout(_signal, _frame):
        raise TimeoutError("The Linux engine did not reply within 45 seconds")

    signal.signal(signal.SIGALRM, timeout)
    process = subprocess.Popen([str(engine), "--stdio"], cwd=engine.parent,
                               stdin=subprocess.PIPE, stdout=subprocess.PIPE, text=True, encoding="utf-8", start_new_session=True)
    request_id = 0

    def request(method, parameters=None, accepted=True):
        nonlocal request_id
        request_id += 1
        message = {"id": str(request_id), "version": 1, "method": method}
        if parameters is not None:
            message["parameters"] = parameters
        signal.alarm(45)
        try:
            process.stdin.write(json.dumps(message) + "\n")
            process.stdin.flush()
            # Bound both response length and time, including a missing newline.
            line = process.stdout.readline(2_000_001)
        finally:
            signal.alarm(0)
        assert line.endswith("\n") and len(line) <= 2_000_000, "Unbounded or missing engine response"
        reply = json.loads(line)
        assert reply["version"] == 1 and reply["id"] == str(request_id), "Response identity mismatch"
        assert reply["ok"] is accepted, reply.get("error", "Unexpected request success")
        return reply.get("result")

    try:
        hello = request("hello")
        assert hello["platform"] == "linux" and hello["protocol"] == 1
        methods = hello["supportedMethods"]
        assert all(method in methods for method in ("themes", "fonts", "state", "stop"))
        assert all(method not in methods for method in ("devices", "start", "present", "levels", "media"))
        themes = request("themes")["themes"]
        assert themes, "The packaged theme catalog is empty"
        assert request("fonts")["families"], "Packaged Python cannot enumerate fonts"
        request("levels", accepted=False)
        request("state", {"themeId": "missing.theme"}, accepted=False)
        rendered = 0
        for theme in themes:
            if theme["category"] == "system" and not args.include_sensors:
                continue
            for light in (False, True):
                state = request("state", {"themeId": theme["id"], "profileId": "wide-1280x480",
                                          "light": light, "includeDevices": False, "colorFiltersEnabled": light,
                                          "saturationPercent": 120, "brightnessPercent": 10})
                assert not state["displayRunning"] and state["submittedFrames"] == 0
                assert state["frameJpeg"], state["notices"]
                frame = base64.b64decode(state["frameJpeg"], validate=True)
                assert len(frame) <= 450_000 and jpeg_size(frame) == (1280, 480)
                if args.sample and rendered == 0:
                    args.sample.parent.mkdir(parents=True, exist_ok=True)
                    args.sample.write_bytes(frame)
                rendered += 1
            print(f"Rendered {theme['id']} in light and dark", flush=True)
        assert rendered > 0
        request("stop")
        process.stdin.close()
        assert process.wait(timeout=15) == 0, "Engine shutdown failed"
        print(f"Passed: capability negotiation, catalog, fonts, rejection recovery and {rendered} JPEG previews. No USB requests sent.")
    finally:
        signal.alarm(0)
        if process.poll() is None:
            os.killpg(process.pid, signal.SIGKILL)
            process.wait(timeout=5)


if __name__ == "__main__":
    main()
