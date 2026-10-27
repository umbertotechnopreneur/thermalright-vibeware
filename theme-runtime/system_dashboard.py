# VBWR B
#
# Project: Thermalright VibeWare
# Repository: https://github.com/umbertotechnopreneur/thermalright-vibeware
# Creator: Umberto Giacobbi | https://umbertogiacobbi.biz
#
# File: system_dashboard.py
# Purpose: Composes the six System pages using shared hardware readings and drawing primitives.
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

"""Paged instruments for measured hardware; no synthetic history or missing-value filler."""
from __future__ import annotations

import math
from collections import OrderedDict

import skia
from canvas import blend, clean, optional_number, clip, progress_color
from detail_history import keyed_rows, points as detail_points, record as record_details
from hardware import _components, _record, _points, _text
from system_details import HARDWARE_KINDS, cooling_values, core_heatmap_values, detail_rows, detail_step, memory_view, reading_rows, sensor_groups, spd_groups, value_text

NAMES = {"thermal": "THERMAL ATLAS", "orbit": "AIRFLOW", "terminal": "MATRIX"}
_FAN_ROTATIONS = OrderedDict()

def readings(raw, kind):
    """Select finite raw readings of the requested sensor type.

    Args:
        raw: Raw observation dictionary received from the engine.
        kind: Sensor type or allow-listed Codex tool category used by this helper.
    """
    return [(clean(item.get("name")), value) for item in raw.get("readings", [])
            if isinstance(item, dict) and item.get("type") == kind
            and (value := optional_number(item.get("value"))) is not None]

def measured(raw, kind, name):
    """Return an exact named reading of the requested type, if available.

    Args:
        raw: Raw observation dictionary received from the engine.
        kind: Sensor type or allow-listed Codex tool category used by this helper.
        name: Display heading or setting/reading name used by this helper.
    """
    return next((value for label, value in readings(raw, kind) if label == name), None)

def devices(s):
    """Select available CPU, rotating GPU and physical RAM observations.

    Args:
        s: Current ThemeContext containing available data, palette and page timing.
    """
    raw = s.sensors
    result = []
    if isinstance(raw.get("cpu"), dict):
        result.append(("cpu", "CPU", raw["cpu"]))
    gpus = [gpu for gpu in raw.get("gpus", []) if isinstance(gpu, dict)]
    if gpus:
        index = s.page_cycle % len(gpus)
        result.append((f"gpu-{index}", "GPU" if len(gpus) == 1 else f"GPU {index + 1}/{len(gpus)}", gpus[index]))
    if isinstance(raw.get("memory"), dict):
        result.append(("memory", "RAM", raw["memory"]))
    return result

def short_name(key, raw):
    """Build a compact hardware identity label without guessing missing metadata.

    Args:
        key: Stable cache, metric or data identity used by this helper.
        raw: Raw observation dictionary received from the engine.
    """
    if key == "memory":
        modules = raw.get("modules") or []
        return " · ".join(value for value in (clean(raw.get("type")), f"{len(modules)} DIMMs" if modules else "") if value)
    return clean(raw.get("name"))

def metadata(key, raw):
    """Build available identity/detail rows for a hardware device.

    Args:
        key: Stable cache, metric or data identity used by this helper.
        raw: Raw observation dictionary received from the engine.
    """
    result = []
    if key == "memory":
        used, total = optional_number(raw.get("usedGiB")), optional_number(raw.get("totalGiB"))
        if used is not None and total is not None:
            result.append(("USED / TOTAL", f"{used:.1f} / {total:.1f} GiB"))
        elif total is not None:
            result.append(("TOTAL", f"{total:.1f} GiB"))
        elif used is not None:
            result.append(("USED", f"{used:.1f} GiB"))
        if (speed := optional_number(raw.get("configuredSpeedMTs"))) is not None:
            result.append(("CONFIGURED", f"{speed:.0f} MT/s"))
    else:
        if (clock := optional_number(raw.get("clockMHz"))) is not None:
            result.append(("CLOCK", f"{clock / 1000:.2f} GHz"))
        if (temp := optional_number(raw.get("temperatureC"))) is not None:
            result.append(("TEMPERATURE", f"{temp:.1f}°C"))
    return result

def arc(c, x, y, radius, start, sweep, tint, width=7, opacity=1, segments=False):
    # Piecewise arcs use the same CPU Skia primitives at all output sizes.
    """Draw a continuous or segmented arc in logical canvas coordinates.

    Args:
        c: Shared Canvas facade; all coordinates use its logical theme space.
        x: Horizontal origin in logical canvas units.
        y: Vertical origin in logical canvas units.
        radius: Radius or corner radius in logical canvas units.
        start: Arc start angle in degrees.
        sweep: Signed arc sweep in degrees.
        tint: RGB hex color used by this drawing.
        width: Stroke thickness in logical canvas units.
        opacity: Alpha fraction from transparent zero to opaque one.
        segments: Whether the decorative arc is divided into separate strokes.
    """
    count = max(1, int(abs(sweep) / 3))
    for index in range(count):
        if segments and index % 4 == 3:
            continue
        a = math.radians(start + sweep * index / count)
        b = math.radians(start + sweep * (index + 1) / count)
        c.line(x + math.cos(a) * radius, y + math.sin(a) * radius,
               x + math.cos(b) * radius, y + math.sin(b) * radius, tint, width, opacity)

def dial(c, s, x, y, radius, value, unit, maximum, tint, style):
    """Draw an available reading on a bounded instrument scale.

    Args:
        c: Shared Canvas facade; all coordinates use its logical theme space.
        s: Current ThemeContext containing available data, palette and page timing.
        x: Horizontal origin in logical canvas units.
        y: Vertical origin in logical canvas units.
        radius: Radius or corner radius in logical canvas units.
        value: Available measured reading shown against maximum, or None when unavailable.
        unit: Reading unit shown alongside the available value.
        maximum: Upper setting bound, scale limit or item cap used by this helper.
        tint: RGB hex color used by this drawing.
        style: Stable visual style identifier from the selected theme.
    """
    start, sweep = (135, 270) if style in ("blueprint", "thermal", "ledger") else (-90, 360)
    segments = style in ("terminal", "signal", "blocks")
    arc(c, x, y, radius, start, sweep, tint, 7, .13, segments)
    if value is not None:
        active = progress_color(tint, value / maximum, s.light)
        arc(c, x, y, radius, start, sweep * clip(value / maximum), active, 7, 1, segments)
    if style in ("blueprint", "orbit", "thermal"):
        for index in range(21):
            angle = math.radians(start + sweep * index / 20)
            inner = radius + 11
            outer = inner + (6 if index % 5 == 0 else 3)
            c.line(x + math.cos(angle) * inner, y + math.sin(angle) * inner,
                   x + math.cos(angle) * outer, y + math.sin(angle) * outer, tint, 1, .4)
    if value is not None:
        _text(c, f"{value:.0f}" if unit == "%" else f"{value:.1f}", x, y + min(10, radius * .2), radius * .57, radius * 1.7, s.fg, True, "center")
        _text(c, unit, x, y + max(24, radius * .52), 15, radius * 1.5, active, align="center")
    if sweep < 360:
        _text(c, "0", x - radius * .7, y + radius + 20, 11, 50, s.muted, align="center")
        _text(c, str(maximum), x + radius * .7, y + radius + 20, 11, 50, s.muted, align="center")

def background(c, s, style):
    """Draw the selected System style's page background.

    Args:
        c: Shared Canvas facade; all coordinates use its logical theme space.
        s: Current ThemeContext containing available data, palette and page timing.
        style: Stable visual style identifier from the selected theme.
    """
    c.gradient(0, 0, c.width, 480, [blend(s.base, s.palette[0], .13), s.base, blend(s.base, s.palette[1], .10)], vertical=True)
    if style in ("blueprint", "terminal"):
        for x in range(0, int(c.width), 28):
            c.line(x, 0, x, 480, s.palette[0], opacity=.045)
        for y in range(0, 480, 28):
            c.line(0, y, c.width, y, s.palette[0], opacity=.045)
    if style == "terminal":
        # Static decorative glyphs stay outside the data regions.
        for index in range(24):
            x = 12 + index * (c.width - 24) / 24
            _text(c, f"{index * 7 % 16:X}", x, 454, 10, 15, s.palette[0])
    elif style in ("orbit", "pulse", "signal"):
        for radius in (140, 210, 280):
            c.circle(c.width - 65, 60, radius, s.palette[1], opacity=.07, stroke=1)
    elif style == "horizon":
        for index in range(7):
            c.line(0, 260 + index * 26, c.width, 160 + index * 26, s.palette[1], 2, .07)

def panel(c, s, box, tint, style):
    """Draw a theme panel using the current appearance and opacity.

    Args:
        c: Shared Canvas facade; all coordinates use its logical theme space.
        s: Current ThemeContext containing available data, palette and page timing.
        box: Logical drawing rectangle as x, y, width and height.
        tint: RGB hex color used by this drawing.
        style: Stable visual style identifier from the selected theme.
    """
    x, y, w, h = box
    c.gradient(x, y, w, h, [blend(s.base, tint, .14), blend(s.base, tint, .025)], vertical=True, radius=0 if style == "blueprint" else 12)
    if style in ("blueprint", "terminal"):
        for px in (x, x + w - 12):
            c.line(px, y, px + 12, y, tint, 2, .7)
    else:
        c.line(x + 16, y, x + w - 16, y, tint, 2, .55)

def frame(c, s, style):
    """Draw the shared System page heading and footer.

    Args:
        c: Shared Canvas facade; all coordinates use its logical theme space.
        s: Current ThemeContext containing available data, palette and page timing.
        style: Stable visual style identifier from the selected theme.
    """
    margin = 24 if c.compact else 40
    title = NAMES.get(style, style.upper())
    _text(c, title, margin, 31, 18, c.width - margin * 2 - 100, s.fg, True)
    _text(c, s.now.strftime("%H:%M"), c.width - margin, 31, 20, 90, s.muted, align="right")
    _text(c, s.page_title.upper(), margin, 52, 11, c.width - 100, s.palette[0])
    bottom = 441
    c.line(margin, bottom, c.width - margin, bottom, s.fg, opacity=.12)
    _text(c, "THERMALRIGHT VIBEWARE", margin, 468, 11, 260, s.muted)
    for index in range(s.page_count):
        x = c.width / 2 - s.page_count * 12 + index * 24
        c.rect(x, 459, 17, 3, s.palette[0], radius=1, opacity=1 if index == s.page_index else .2)
        if index == s.page_index:
            c.rect(x, 459, 17 * s.page_progress, 3, progress_color(s.palette[0], s.page_progress, s.light), radius=1)
    _text(c, f"{s.page_index + 1:02d} / {s.page_count:02d}", c.width - margin, 468, 11, 90, s.muted, align="right")

def overview(c, s, style, items):
    """Compose the System overview from available aggregate readings.

    Args:
        c: Shared Canvas facade; all coordinates use its logical theme space.
        s: Current ThemeContext containing available data, palette and page timing.
        style: Stable visual style identifier from the selected theme.
        items: Available aggregate device observations for this page.
    """
    count = len(items)
    if not count:
        return
    gap, margin = (10, 24) if c.compact else (22, 40)
    for index, (key, label, raw) in enumerate(items):
        tint = s.palette[index % 3]
        load = optional_number(raw.get("usagePercent"))
        name = short_name(key, raw)
        info = metadata(key, raw)
        if c.compact:
            height = (354 - (count - 1) * gap) / count
            y = 72 + index * (height + gap)
            panel(c, s, (margin, y, 432, height), tint, style)
            if load is not None:
                dial(c, s, 83, y + height / 2, min(37, height * .32), load, "%", 100, tint, style)
            _text(c, label + (" · " + name if name else ""), 142, y + 25, 14, 292, s.fg, True)
            for row, (_, value) in enumerate(info):
                _text(c, value, 142, y + 57 + row * 25, 18, 292, tint if row == 0 else s.muted)
        else:
            width = (1200 - gap * (count - 1)) / count
            x = margin + index * (width + gap)
            panel(c, s, (x, 77, width, 345), tint, style)
            _text(c, label, x + 22, 104, 14, width - 44, tint, True)
            _text(c, name, x + 22, 130, 17, width - 44, s.fg, True)
            if load is not None:
                dial(c, s, x + width / 2, 238, 75, load, "%", 100, tint, style)
            for column, (title, value) in enumerate(info):
                half = (width - 44) / max(1, len(info))
                xx = x + 22 + column * half
                _text(c, title, xx, 374, 10, half - 8, s.muted)
                value_size = 21 if key != "memory" else 15 if len(info) > 1 else 17
                _text(c, value, xx, 400, value_size, half - 8, tint, True)

def history(c, s, component, box, tint):
    """Draw the measured chart history for one hardware component.

    Args:
        c: Shared Canvas facade; all coordinates use its logical theme space.
        s: Current ThemeContext containing available data, palette and page timing.
        component: Projected hardware component containing the selected metric.
        box: Logical drawing rectangle as x, y, width and height.
        tint: RGB hex color used by this drawing.
    """
    x, y, w, h = box
    metric = component.metric("usagePercent") if component else None
    if metric is None:
        return
    for fraction, label in ((0, "100"), (.5, "50"), (1, "0")):
        yy = y + fraction * h
        c.line(x + 25, yy, x + w, yy, tint, opacity=.13)
        _text(c, label, x + 19, yy + 4, 9, 24, s.muted, align="right")
    points = _points(component, metric)
    if not points:
        return
    oldest, newest = points[0][0], points[-1][0]
    span = max(10, newest - oldest)
    plotted = [(x + 27 + (1 - (newest - stamp) / span) * (w - 30), y + h * (1 - clip(value / 100))) for stamp, value in points]
    if len(plotted) > 1:
        c.polygon([(plotted[0][0], y + h), *plotted, (plotted[-1][0], y + h)], tint, .07)
        for start, end in zip(plotted, plotted[1:]):
            c.line(*start, *end, tint, 2.5)
    c.circle(*plotted[-1], 3, tint)
    _text(c, f"{span:.0f}s", x + 27, y + h + 17, 10, 50, s.muted)
    _text(c, "NOW", x + w, y + h + 17, 10, 45, s.muted, align="right")

def aggregate_activity(c, s, raw, box, tint):
    """Draw available aggregate activity readings for one device.

    Args:
        c: Shared Canvas facade; all coordinates use its logical theme space.
        s: Current ThemeContext containing available data, palette and page timing.
        raw: Raw observation dictionary received from the engine.
        box: Logical drawing rectangle as x, y, width and height.
        tint: RGB hex color used by this drawing.
    """
    x, y, w, h = box
    rows = reading_rows(raw, ("Clock",), aggregate=True)
    if rows:
        (label, value, unit), = instrument_page(rows, detail_step(s), 1)[0]
        _text(c, label, x, y + 13, 12, w, s.muted)
        _text(c, value_text(value, unit), x, y + 53, 27, w, tint, True)

def occupancy(c, s, used, total, box, tint, label):
    """Draw a used/total occupancy meter with explicit unavailable handling.

    Args:
        c: Shared Canvas facade; all coordinates use its logical theme space.
        s: Current ThemeContext containing available data, palette and page timing.
        used: Available used amount in the same units as total.
        total: Available total capacity; missing or nonpositive totals stay unavailable.
        box: Logical drawing rectangle as x, y, width and height.
        tint: RGB hex color used by this drawing.
        label: Display label, or whether to show it when the parameter is a flag.
    """
    if used is None or total is None or total <= 0:
        return
    x, y, w, h = box
    _text(c, label, x, y + 13, 11, w, s.muted)
    _text(c, f"{used:.1f} / {total:.1f} GiB", x, y + 44, 23, w, tint, True)
    active = progress_color(tint, used / total, s.light)
    for index in range(32):
        filled = (index + 1) / 32 <= clip(used / total)
        c.rect(x + index * w / 32, y + h - 19, w / 32 - 3, 16, active if filled else tint, radius=2,
               opacity=1 if filled else .14)

def activity(c, s, style, items, components):
    """Compose System activity meters and measured history charts.

    Args:
        c: Shared Canvas facade; all coordinates use its logical theme space.
        s: Current ThemeContext containing available data, palette and page timing.
        style: Stable visual style identifier from the selected theme.
        items: Available aggregate device observations for this page.
        components: Available projected hardware components.
    """
    count = len(items)
    if not count:
        return
    for index, (key, label, raw) in enumerate(items):
        tint = s.palette[index % 3]
        component = next((item for item in components if item.key == key), None)
        load = optional_number(raw.get("usagePercent"))
        if c.compact:
            h = 348 / count
            y = 75 + index * h
            _text(c, label, 24, y + 13, 13, 200, tint, True)
            if load is not None:
                _text(c, f"{load:.0f}%", 455, y + 13, 20, 100, s.fg, True, "right")
            history(c, s, component, (24, y + 30, 430, h - 58), tint)
        else:
            w = (1200 - 22 * (count - 1)) / count
            x = 40 + index * (w + 22)
            panel(c, s, (x, 77, w, 345), tint, style)
            _text(c, label, x + 18, 104, 13, w - 36, tint, True)
            if load is not None:
                _text(c, f"{load:.0f}%", x + w - 18, 111, 30, 140, s.fg, True, "right")
            _text(c, short_name(key, raw), x + 18, 139, 12, w - 36, s.muted)
            history(c, s, component, (x + 14, 166, w - 32, 115), tint)
            if key == "cpu":
                aggregate_activity(c, s, raw, (x + 20, 320, w - 40, 80), tint)
            elif key == "memory":
                occupancy(c, s, optional_number(raw.get("usedGiB")), optional_number(raw.get("totalGiB")), (x + 20, 320, w - 40, 80), tint, "PHYSICAL RAM")
            else:
                used, total = measured(raw, "SmallData", "GPU Memory Used"), measured(raw, "SmallData", "GPU Memory Total")
                occupancy(c, s, used / 1024 if used is not None else None, total / 1024 if total is not None else None,
                          (x + 20, 320, w - 40, 80), tint, "VRAM")

def heat_color(value, low, high, light=False):
    """Map an available reading onto the bounded theme heat scale.

    Args:
        value: Measured reading mapped between the supplied lower and upper scale bounds.
        low: Lower bound of the displayed range.
        high: Upper bound of the displayed range.
        light: Whether the current theme uses a light appearance.
    """
    cold = "#286EDB" if light else "#2388FF"
    hot = "#D63B4C" if light else "#FF4D5E"
    fraction = 0 if high <= low else clip((value - low) / (high - low))
    return blend(cold, hot, fraction)

def heatmap_grid(c, s, rows, value_index, box, low, high, formatter, outline=None):
    """Draw available readings in a bounded heatmap grid.

    Args:
        c: Shared Canvas facade; all coordinates use its logical theme space.
        s: Current ThemeContext containing available data, palette and page timing.
        rows: Available projected reading rows, preserving their source units.
        value_index: Tuple index containing the measured value for heatmap coloring.
        box: Logical drawing rectangle as x, y, width and height.
        low: Lower bound of the displayed range.
        high: Upper bound of the displayed range.
        formatter: Callback formatting one available heatmap reading.
        outline: Optional RGB color for the heatmap cell outline.
    """
    x, y, size, _ = box
    count = len(rows)
    columns = math.ceil(math.sqrt(count))
    row_count = math.ceil(count / columns)
    gap = 3 if size < 230 else 5
    inset = 7
    cell = min((size - inset * 2 - gap * (columns - 1)) / columns,
               (size - inset * 2 - gap * (row_count - 1)) / row_count)
    grid_width = columns * cell + (columns - 1) * gap
    grid_height = row_count * cell + (row_count - 1) * gap
    start_x = x + (size - grid_width) / 2
    start_y = y + (size - grid_height) / 2
    if outline is not None:
        c.rect(x - 3, y - 3, size + 6, size + 6, outline, radius=10, stroke=3)
    for index, row in enumerate(rows):
        column, line = index % columns, index // columns
        xx = start_x + column * (cell + gap)
        yy = start_y + line * (cell + gap)
        value = row[value_index]
        tint = heat_color(value, low, high, s.light)
        c.rect(xx, yy, cell, cell, tint, radius=max(2, min(7, cell * .11)), opacity=.92)
        number_size = max(7, min(11, cell * .18))
        value_size = max(7, min(17, cell * .27))
        _text(c, f"{row[0]:02d}", xx + cell / 2, yy + cell * .32,
              number_size, cell - 4, "#FFFFFF", True, "center")
        _text(c, formatter(value), xx + cell / 2, yy + cell * .70,
              value_size, cell - 4, "#FFFFFF", True, "center")

def core_page(c, s, style):
    """Compose the available aggregate processor temperature/activity page.

    Args:
        c: Shared Canvas facade; all coordinates use its logical theme space.
        s: Current ThemeContext containing available data, palette and page timing.
        style: Stable visual style identifier from the selected theme.
    """
    rows = core_heatmap_values(s.sensors)
    if len(rows) < 8:
        return
    cpu = s.sensors.get("cpu") or {}
    temperature = optional_number(cpu.get("temperatureC"))
    outline = heat_color(temperature, 20, 100, s.light) if temperature is not None else s.muted
    margin, gap = ((24, 12) if c.compact else (40, 28))
    width = (c.width - margin * 2 - gap) / 2
    top, height = 77, 345
    size = min(198 if c.compact else 270, width - 16)
    grid_y = 125 if c.compact else 132
    powers = [row[1] for row in rows]
    power_low, power_high = min(powers), max(powers)
    if power_high - power_low < .1:
        power_high = power_low + 1
    power_subtitle = "WATTS · CPU TEMP OUTLINE" if c.compact else f"{len(rows)} CORES · WATTS · CPU TEMP OUTLINE"
    load_subtitle = "LOAD · SMT AVERAGED" if c.compact else "THREADS AVERAGED PER PHYSICAL CORE"
    for index, (title, subtitle) in enumerate((("CORE POWER", power_subtitle),
                                                ("CORE LOAD", load_subtitle))):
        x = margin + index * (width + gap)
        panel(c, s, (x, top, width, height), s.palette[index], style)
        _text(c, title, x + 18, 101, 13, width - 36, s.palette[index], True)
        _text(c, subtitle, x + 18, 121, 9 if c.compact else 10, width - 36, s.muted)
    temperature_text = f"{temperature:.1f}°C" if temperature is not None else "NO CPU TEMP"
    _text(c, temperature_text, margin + width - 18, 101, 10, 84, outline, True, "right")
    left_x = margin + (width - size) / 2
    right_x = margin + width + gap + (width - size) / 2
    heatmap_grid(c, s, rows, 1, (left_x, grid_y, size, size), power_low, power_high,
                 lambda value: f"{value:.1f}W", outline)
    heatmap_grid(c, s, rows, 2, (right_x, grid_y, size, size), 0, 100,
                 lambda value: f"{value:.0f}%")
    _text(c, f"{power_low:.1f}W  COLD", margin + 18, 416, 9, width / 2 - 18, s.muted)
    _text(c, f"HOT  {max(powers):.1f}W", margin + width - 18, 416, 9, width / 2 - 18, s.muted, align="right")
    _text(c, "0%  COLD", margin + width + gap + 18, 416, 9, width / 2 - 18, s.muted)
    _text(c, "HOT  100%", c.width - margin - 18, 416, 9, width / 2 - 18, s.muted, align="right")

def instrument_page(rows, cycle, count):
    """Select a bounded rotating subset of instrument rows.

    Args:
        rows: Available projected reading rows, preserving their source units.
        cycle: Shared page-cycle counter used for bounded row rotation.
        count: Maximum number of rows selected for this instrument page.
    """
    start = (cycle % math.ceil(len(rows) / count)) * count
    return rows[start:start + count], start

def fan_angle(label, rpm, now):
    """Advance a fan's animation from its measured RPM while bounding retained state.

    Args:
        label: Visible reading/device label retained from its source.
        rpm: Real measured fan speed in revolutions per minute.
        now: Monotonic timestamp in seconds used for fan motion.
    """
    previous = _FAN_ROTATIONS.get(label)
    angle = 0
    if previous and now >= previous[0]:
        # A readable emblem, not a simulation of the rotor's physical speed.
        speed = 30 + min(35, rpm / 100) if rpm > 0 else 0
        angle = (previous[1] + min(2, now - previous[0]) * speed) % 360
    _FAN_ROTATIONS[label] = (now, angle)
    _FAN_ROTATIONS.move_to_end(label)
    while len(_FAN_ROTATIONS) > 32:
        _FAN_ROTATIONS.popitem(last=False)
    return angle

def fan_icon(c, x, y, radius, tint, angle):
    """Draw a fan symbol at the supplied angle.

    Args:
        c: Shared Canvas facade; all coordinates use its logical theme space.
        x: Horizontal origin in logical canvas units.
        y: Vertical origin in logical canvas units.
        radius: Radius or corner radius in logical canvas units.
        tint: RGB hex color used by this drawing.
        angle: Fan rotation angle in radians.
    """
    c.rect(x - radius, y - radius, radius * 2, radius * 2, tint,
           radius=radius * .22, opacity=.35, stroke=1.2)
    c.circle(x, y, radius * .82, tint, opacity=.25, stroke=1)
    for dx, dy in ((-1, -1), (1, -1), (-1, 1), (1, 1)):
        c.circle(x + dx * radius * .77, y + dy * radius * .77, radius * .035, tint, .55)
    blade = skia.Path()
    blade.moveTo(.16, -.12)
    blade.cubicTo(.20, -.42, .40, -.73, .62, -.56)
    blade.cubicTo(.85, -.35, .56, -.05, .20, .12)
    blade.close()
    c.native.save()
    c.native.translate(x, y)
    c.native.rotate(angle)
    c.native.scale(radius, radius)
    for _ in range(4):
        c.native.drawPath(blade, c.paint(tint, .9, 1.6 / radius))
        c.native.rotate(90)
    c.native.restore()
    c.circle(x, y, radius * .16, tint, stroke=1.6)

def power_instruments(c, s, style, rows, box):
    """Draw the current page of measured power instruments.

    Args:
        c: Shared Canvas facade; all coordinates use its logical theme space.
        s: Current ThemeContext containing available data, palette and page timing.
        style: Stable visual style identifier from the selected theme.
        rows: Available projected reading rows, preserving their source units.
        box: Logical drawing rectangle as x, y, width and height.
    """
    x, y, w, h = box
    count = 2 if c.compact else 3
    if len(rows) > count:
        # Keep the first reading visible while the remaining readings rotate.
        rotating, _ = instrument_page(rows[1:], detail_step(s), count - 1)
        visible = [rows[0], *rotating]
    else:
        visible = rows
    panel(c, s, box, s.palette[0], style)
    if not c.compact:
        has_power = any(unit == "W" for _, _, unit in rows)
        has_electrical = any(unit != "W" for _, _, unit in rows)
        heading = "POWER & ELECTRICAL" if has_power and has_electrical else "POWER DRAW" if has_power else "ELECTRICAL"
        _text(c, heading, x + 20, y + 27, 12, w - 140, s.palette[0], True)
        if len(rows) > count:
            _text(c, f"{len(rows)} readings", x + w - 20, y + 27, 11, 100, s.muted, align="right")
    width = (w - 40) / len(visible)
    for index, (label, value, unit) in enumerate(visible):
        xx = x + 20 + index * width
        if index:
            c.line(xx - 8, y + (12 if c.compact else 45), xx - 8, y + h - 14,
                   s.fg, 1, .12)
        if c.compact:
            _text(c, label, xx, y + 21, 11, width - 16, s.muted)
            _text(c, value_text(value, unit), xx, y + 45, 21, width - 16, s.fg, True)
        else:
            baseline = y + (h * .63 if h > 180 else h - 20)
            _text(c, label, xx, baseline - 39, 13, width - 16, s.muted)
            _text(c, value_text(value, unit), xx, baseline, 32 if h > 180 else 28,
                  width - 16, s.fg, True)

def fan_instruments(c, s, style, rows, box):
    """Draw the current page of observed fan instruments.

    Args:
        c: Shared Canvas facade; all coordinates use its logical theme space.
        s: Current ThemeContext containing available data, palette and page timing.
        style: Stable visual style identifier from the selected theme.
        rows: Available projected reading rows, preserving their source units.
        box: Logical drawing rectangle as x, y, width and height.
    """
    x, y, w, h = box
    count = 2 if c.compact else 3
    if len(rows) > count and rows[0][1] > 0:
        # Keep one running fan visible while the remaining named channels rotate.
        # A late page containing only stopped headers otherwise looks like lost sensors.
        rotating, _ = instrument_page(rows[1:], s.page_cycle, count - 1)
        visible = [rows[0], *rotating]
    else:
        visible, _ = instrument_page(rows, s.page_cycle, count)
    _text(c, "FAN SPEED", x, y + 14, 12, w * .6, s.palette[1], True)
    _text(c, f"{len(rows)} channels", x + w, y + 14, 11, w * .35, s.muted, align="right")
    top = y + 28
    height = h - 28
    if c.compact:
        step = height / len(visible)
        for index, (label, value, unit) in enumerate(visible):
            yy = top + index * step
            fan_icon(c, x + 17, yy + step / 2, min(17, step / 2 - 4), s.palette[1], fan_angle(label, value, s.elapsed_seconds))
            _text(c, label, x + 46, yy + step / 2 + 5, 12, w - 186, s.muted)
            _text(c, f"{value:.0f} {unit}", x + w, yy + step / 2 + 6, 20, 134, s.fg, True, "right")
        return
    gap = 14
    width = (w - gap * (len(visible) - 1)) / len(visible)
    for index, (label, value, unit) in enumerate(visible):
        xx = x + index * (width + gap)
        center = xx + width / 2
        source, _, name = label.partition(" · ")
        panel(c, s, (xx, top, width, height), s.palette[1], style)
        _text(c, source, xx + 16, top + 25, 11, width - 32, s.muted, True)
        fan_icon(c, center, top + height * .36, min(52, height * .16), s.palette[1], fan_angle(label, value, s.elapsed_seconds))
        _text(c, f"{value:.0f}", center, top + height - 45, 30, width - 24, s.fg, True, "center")
        _text(c, unit, center, top + height - 27, 10, width - 24, s.palette[1], align="center")
        _text(c, name, center, top + height - 12, 12, width - 24, s.muted, align="center")

def cooling(c, s, style):
    """Compose cooling readings with real fan activity and available power data.

    Args:
        c: Shared Canvas facade; all coordinates use its logical theme space.
        s: Current ThemeContext containing available data, palette and page timing.
        style: Stable visual style identifier from the selected theme.
    """
    thermal, powers, fans = cooling_values(s.sensors)
    if thermal:
        thermal, _ = instrument_page(thermal, detail_step(s), 2)
    if c.compact:
        for index, (key, label, raw) in enumerate(thermal[:2]):
            x = 130 + index * 220 if len(thermal) > 1 else 240
            _text(c, label, x, 91, 13, 180, s.palette[index], True, "center")
            dial(c, s, x, 168, 53, raw["temperatureC"], "°C", 120, s.palette[index], style)
        y = 270 if thermal else 90
        if powers:
            power_instruments(c, s, style, powers, (28, y, 424, 55))
            y += 69
        if fans:
            fan_instruments(c, s, style, fans, (28, y, 424, 426 - y))
    else:
        for index, (key, label, raw) in enumerate(thermal[:2]):
            x = 40 + index * 300
            panel(c, s, (x, 77, 278, 345), s.palette[index], style)
            _text(c, label, x + 20, 105, 13, 238, s.palette[index], True)
            _text(c, short_name(key, raw), x + 20, 132, 15, 238, s.fg, True)
            dial(c, s, x + 139, 250, 72, raw["temperatureC"], "°C", 120, s.palette[index], style)
            if (clock := optional_number(raw.get("clockMHz"))) is not None:
                _text(c, f"{clock / 1000:.2f} GHz", x + 139, 396, 23, 230, s.palette[index], True, "center")
        x = 40 + min(2, len(thermal)) * 300
        w = 1240 - x
        if powers:
            power_instruments(c, s, style, powers, (x, 77, w, 128 if fans else 345))
        fan_y = 221 if powers else 77
        if fans:
            fan_instruments(c, s, style, fans, (x, fan_y, w, 422 - fan_y))

def trend_extrema(low, high, unit):
    """Format available minimum/maximum readings for a detail chart.

    Args:
        low: Lower bound of the displayed range.
        high: Upper bound of the displayed range.
        unit: Reading unit shown alongside the available value.
    """
    digits = 3 if unit == "V" else 1 if unit in ("A", "°C") else 0
    while low != high and round(low, digits) == round(high, digits) and digits < 6:
        digits += 1
    return f"{low:.{digits}f}", f"{high:.{digits}f}"

def detail_trend(c, s, samples, box, tint, unit):
    """Draw real per-reading history and its available extrema.

    Args:
        c: Shared Canvas facade; all coordinates use its logical theme space.
        s: Current ThemeContext containing available data, palette and page timing.
        samples: Real timestamp/value pairs retained by the bounded history cache.
        box: Logical drawing rectangle as x, y, width and height.
        tint: RGB hex color used by this drawing.
        unit: Reading unit shown alongside the available value.
    """
    if len(samples) < 2 or samples[-1][0] <= samples[0][0]:
        return
    x, y, w, h = box
    values = [value for _, value in samples]
    low, high = min(values), max(values)
    low_label, high_label = trend_extrema(low, high, unit)
    _text(c, high_label, x + w, y - 3, 9, w, s.muted, align="right")
    _text(c, low_label, x + w, y + h + 11, 9, w, s.muted, align="right")
    pixel = 1 / c.scale
    c.line(x, y, x, y + h, tint, pixel, .25)
    c.line(x, y + h, x + w, y + h, tint, pixel, .25)
    duration = samples[-1][0] - samples[0][0]
    vertices = [(x + w * (at - samples[0][0]) / duration,
                 y + h / 2 if high == low else y + h * (high - value) / (high - low))
                for at, value in samples]
    path = skia.Path()
    path.moveTo(*vertices[0])
    for previous, current in zip(vertices, vertices[1:]):
        middle = (previous[0] + current[0]) / 2
        path.cubicTo(middle, previous[1], middle, current[1], *current)
    c.native.drawPath(path, c.paint(tint, .95, pixel))

def detail_panel(c, s, style, group, box, tint, step):
    """Draw the selected detail rows and their bounded history charts.

    Args:
        c: Shared Canvas facade; all coordinates use its logical theme space.
        s: Current ThemeContext containing available data, palette and page timing.
        style: Stable visual style identifier from the selected theme.
        group: Projected hardware group with its stable key, device identity and rows.
        box: Logical drawing rectangle as x, y, width and height.
        tint: RGB hex color used by this drawing.
        step: Shared detail-rotation index; None uses the context's current page clock.
    """
    key, label, name, rows = group
    x, y, w, h = box
    panel(c, s, box, tint, style)
    _text(c, label, x + 20, y + 27, 13, w - 40, tint, True)
    _text(c, name, x + 20, y + 53, 14, w - 40, s.fg, True)
    visible, start = detail_rows(rows, step)
    keys = [identity for identity, _ in keyed_rows(group, s.sensors)]
    charted = not key.startswith("memory-spd-")
    text_width = (w - 40) * .47 if charted else w - 40
    row_height = (h - 93) / max(1, len(visible))
    for index, (name, value, unit) in enumerate(visible):
        yy = y + 80 + index * row_height
        _text(c, name, x + 20, yy, 12, text_width, s.muted)
        value_tint = progress_color(tint, value / 120, s.light) if unit == "°C" else tint
        _text(c, value_text(value, unit), x + 20, yy + 27, 25, text_width, value_tint, True)
        if charted:
            graph_x = x + 28 + text_width
            detail_trend(c, s, detail_points(keys[start + index]),
                         (graph_x, yy + 2, x + w - 20 - graph_x, 25), tint, unit)
        if unit == "°C":
            c.rect(x + 20, yy + 37, text_width, 2, tint, opacity=.13)
            c.rect(x + 20, yy + 37, text_width * clip(value / 120), 2, value_tint)
        elif index < len(visible) - 1:
            c.line(x + 20, yy + 47, x + w - 20, yy + 47, tint, opacity=.12)
    if any(unit == "°C" for _, _, unit in visible):
        _text(c, "SCALE 0–120°C", x + 20, y + h - 8, 10, (w - 40) / 2, s.muted)
    if len(rows) > len(visible):
        _text(c, f"{start + 1}–{start + len(visible)} / {len(rows)}", x + w - 20, y + h - 8,
              10, w - 40, s.muted, align="right")

def detail_groups(c, s, style, groups, columns=3, step=None):
    """Lay out rotating hardware detail groups across the supplied columns.

    Args:
        c: Shared Canvas facade; all coordinates use its logical theme space.
        s: Current ThemeContext containing available data, palette and page timing.
        style: Stable visual style identifier from the selected theme.
        groups: Available grouped hardware readings for the current page.
        columns: Number of detail columns available in this layout.
        step: Shared detail-rotation index; None uses the context's current page clock.
    """
    if not groups:
        return
    step = detail_step(s) if step is None else step
    group_count = 1 if c.compact else columns
    bank_count = math.ceil(len(groups) / group_count)
    visible, _ = instrument_page(groups, step, group_count)
    # Rotate readings independently of the group bank; every channel stays reachable.
    row_step = step // bank_count
    gap, margin = (16, 24) if c.compact else (22, 40)
    width = (c.width - margin * 2 - gap * (len(visible) - 1)) / len(visible)
    for index, group in enumerate(visible):
        key = group[0]
        tint = s.palette[0 if key == "cpu" else 1 if key.startswith("gpu-") else 2]
        height = 93 + 63 * len(detail_rows(group[3], row_step)[0])
        detail_panel(c, s, style, group, (margin + index * (width + gap), 77, width, height), tint, row_step)

def details(c, s, style, kinds):
    """Compose available hardware readings of the requested types.

    Args:
        c: Shared Canvas facade; all coordinates use its logical theme space.
        s: Current ThemeContext containing available data, palette and page timing.
        style: Stable visual style identifier from the selected theme.
        kinds: Allowed sensor types for this detail page.
    """
    detail_groups(c, s, style, sensor_groups(s.sensors, kinds))

def memory_page(c, s, style, step):
    """Compose physical RAM identities and separately labeled SPD limits.

    Args:
        c: Shared Canvas facade; all coordinates use its logical theme space.
        s: Current ThemeContext containing available data, palette and page timing.
        style: Stable visual style identifier from the selected theme.
        step: Shared detail-rotation index; None uses the context's current page clock.
    """
    raw = s.sensors.get("memory") or {}
    summary = metadata("memory", raw)
    if memory_type := clean(raw.get("type")):
        summary.insert(0, ("MEMORY TYPE", memory_type))
    if (clock := optional_number(raw.get("clockMHz"))) is not None:
        summary.append(("MEMORY CLOCK", f"{clock:.0f} MHz"))
    margin, gap = (24, 16) if c.compact else (40, 22)
    width = c.width - margin * 2
    if c.compact and summary:
        # Keep every measured summary reachable on square displays too.
        summary, _ = instrument_page(summary, step, 2)
    for index, (label, value) in enumerate(summary):
        cell = width / len(summary)
        x = margin + index * cell
        _text(c, label, x, 89, 10, cell - 12, s.muted)
        _text(c, value, x, 118, 16 if c.compact else 27, cell - 12, s.palette[2], True)
    modules = [module for module in raw.get("modules", []) or [] if isinstance(module, dict)]
    if not modules:
        occupancy(c, s, optional_number(raw.get("usedGiB")), optional_number(raw.get("totalGiB")),
                  (margin, 220, width, 120), s.palette[2], "PHYSICAL RAM")
        return
    visible, start = instrument_page(modules, step, 1 if c.compact else 2)
    cell = (width - gap * (len(visible) - 1)) / len(visible)
    for index, module in enumerate(visible):
        x = margin + index * (cell + gap)
        tint = s.palette[2]
        panel(c, s, (x, 149, cell, 273), tint, style)
        maker, part = clean(module.get("manufacturer")), clean(module.get("partNumber"))
        _text(c, maker or "RAM MODULE", x + 20, 178, 14, cell - 100, tint, True)
        _text(c, f"{start + index + 1} / {len(modules)}", x + cell - 20, 178, 11, 65, s.muted, align="right")
        _text(c, part or clean(module.get("name")), x + 20, 212, 25 if c.compact else 30, cell - 40, s.fg, True)
        if serial := clean(module.get("serialNumber")):
            _text(c, "S/N  " + serial, x + 20, 237, 12, cell - 40, s.muted)
        if (capacity := optional_number(module.get("capacityGiB"))) is not None:
            _text(c, "CAPACITY", x + 20, 263, 10, cell - 160, s.muted)
            _text(c, f"{capacity:g} GiB", x + 20, 293, 30, cell - 160, tint, True)
        memory_outline(c, x + cell - 123, 260, tint)
        identity = [("SLOT", clean(module.get("deviceLocator"))), ("BANK", clean(module.get("bankLabel")))]
        for row, (label, value) in enumerate((item for item in identity if item[1])):
            yy = 322 + row * 24
            _text(c, label, x + 20, yy, 10, 58, s.muted)
            _text(c, value, x + 84, yy, 13, cell - 104, s.fg)
        module_type = clean(module.get("type"))
        speed = optional_number(module.get("configuredSpeedMTs"))
        if module_type or speed is not None:
            c.line(x + 20, 367, x + cell - 20, 367, tint, opacity=.16)
            if module_type:
                _text(c, module_type, x + 20, 399, 22, (cell - 40) / 2, tint, True)
            if speed is not None:
                _text(c, "CONFIGURED", x + cell - 20, 384, 9, (cell - 40) / 2, s.muted, align="right")
                _text(c, f"{speed:.0f} MT/s", x + cell - 20, 406, 18, (cell - 40) / 2, s.fg, True, "right")

def memory_outline(c, x, y, tint):
    # A small DIMM symbol, not a diagram of the installed module's chip layout.
    """Draw the decorative RAM-module outline.

    Args:
        c: Shared Canvas facade; all coordinates use its logical theme space.
        x: Horizontal origin in logical canvas units.
        y: Vertical origin in logical canvas units.
        tint: RGB hex color used by this drawing.
    """
    c.line(x, y, x + 100, y, tint, 1.5, .7)
    c.line(x, y, x, y + 29, tint, 1.5, .7)
    c.line(x + 100, y, x + 100, y + 29, tint, 1.5, .7)
    c.line(x, y + 29, x + 43, y + 29, tint, 1.5, .7)
    c.line(x + 49, y + 29, x + 100, y + 29, tint, 1.5, .7)
    for offset in (8, 37, 66):
        c.rect(x + offset, y + 7, 23, 14, tint, radius=2, opacity=.17)
    for offset in range(7, 100, 7):
        if offset != 49:
            c.line(x + offset, y + 30, x + offset, y + 34, tint, 2, .6)

def render(c, s, style):
    """Compose the selected available System page in the requested visual style.

    Args:
        c: Shared Canvas facade; all coordinates use its logical theme space.
        s: Current ThemeContext containing available data, palette and page timing.
        style: Stable visual style identifier from the selected theme.
    """
    components = _components(s.sensors)
    _record(components, clean(s.sensors.get("updatedAtUtc")))
    record_details(s.sensors)
    items = devices(s)
    background(c, s, style)
    frame(c, s, style)
    if s.page_id == "overview":
        overview(c, s, style, items)
    elif s.page_id == "activity":
        activity(c, s, style, items, components)
    elif s.page_id == "cores":
        core_page(c, s, style)
    elif s.page_id == "cooling":
        cooling(c, s, style)
    elif s.page_id == "electrical":
        details(c, s, style, HARDWARE_KINDS)
    elif s.page_id == "temperatures":
        details(c, s, style, ("Temperature",))
    elif s.page_id == "memory":
        section, step = memory_view(s.sensors, detail_step(s))
        if section == "spd":
            detail_groups(c, s, style, spd_groups(s.sensors), columns=2, step=step)
        elif section == "modules":
            memory_page(c, s, style, step)
    else:
        raise ValueError("Unknown System page: " + s.page_id)
# VBWR E
