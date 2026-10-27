# VBWR B
#
# Project: Thermalright VibeWare
# Repository: https://github.com/umbertotechnopreneur/thermalright-vibeware
# Creator: Umberto Giacobbi | https://umbertogiacobbi.biz
#
# File: artist_page.py
# Purpose: Draws sourced artist facts and attributed portrait images when available.
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

"""A short attributed artist page shared by the ten music themes."""
from __future__ import annotations

import base64
import hashlib
import skia
from canvas import cached, clean, SAMPLING

def paragraph(c, text, x, y, width, lines, size=20):
    """Draw wrapped biography text within the supplied line and width limits.

    Args:
        c: Shared Canvas facade; all coordinates use its logical theme space.
        text: Source text to wrap or display.
        x: Horizontal origin in logical canvas units.
        y: Vertical origin in logical canvas units.
        width: Available width in logical canvas units; None uses the helper's default.
        lines: Maximum number of biography lines to draw.
        size: Text size or square image size in logical canvas units.
    """
    font_size = min(size * c.ctx.font_scale, size * 1.12)
    font = skia.Font(skia.Typeface.MakeFromName(c.ctx.font_family), font_size)
    words = str(text or "")[:1000].split()
    rows, line = [], ""
    for word in words:
        candidate = (line + " " + word).strip()
        if font.measureText(candidate) > width and line:
            rows.append(line)
            line = word
        else:
            line = candidate
    if line:
        rows.append(line)
    visible = rows[:lines]
    if len(rows) > lines and visible:
        visible[-1] = visible[-1].rstrip(". ") + "…"
    for index, row in enumerate(visible):
        c.text(row, x, y + index * (font_size + 8), font_size / c.ctx.font_scale, c.ctx.fg, width)

def portrait(c, profile, x, y, width, height):
    """Draw an available attributed artist portrait without stretching its aspect ratio.

    Args:
        c: Shared Canvas facade; all coordinates use its logical theme space.
        profile: Sourced artist profile, including its available portrait metadata.
        x: Horizontal origin in logical canvas units.
        y: Vertical origin in logical canvas units.
        width: Available width in logical canvas units; None uses the helper's default.
        height: Available height in logical canvas units.
    """
    encoded = profile.get("photo")
    if not isinstance(encoded, str) or not encoded or len(encoded) > 600_000:
        return False
    key = hashlib.sha256(encoded.encode("ascii")).hexdigest()
    def decode():
        """Decode and bound the artist portrait for the shared image cache."""
        image = skia.Image.MakeFromEncoded(skia.Data.MakeWithCopy(base64.b64decode(encoded, validate=True)))
        return image if image and image.width() * image.height() <= 16_777_216 else None
    image = cached(("artist-photo", key), decode)
    if image is None:
        return False
    scale = min(width / image.width(), height / image.height())
    w, h = image.width() * scale, image.height() * scale
    c.native.drawImageRect(image, skia.Rect.MakeXYWH(x + (width - w) / 2, y + (height - h) / 2, w, h), SAMPLING)
    return True

def render(c, s):
    """Draw available artist facts with their attribution; hide insufficiently sourced data.

    Args:
        c: Shared Canvas facade; all coordinates use its logical theme space.
        s: Current ThemeContext containing available data, palette and page timing.
    """
    p = s.artist_profile
    c.backdrop(s.style, artwork=True)
    c.header("ARTIST / " + clean(p.get("name")))
    if c.compact:
        has_photo = portrait(c, p, 24, 83, 155, 170)
        x = 200 if has_photo else 24
        width = 456 - x
        c.text(p.get("name"), x, 110, min(25, 28 / s.font_scale), width=width, bold=True)
        for index, fact in enumerate(p.get("facts", [])[:3]):
            c.text(fact.get("label"), x, 145 + index * 42, 11 / s.font_scale, s.muted, width)
            c.text(fact.get("value"), x, 166 + index * 42, 17 / s.font_scale, s.accent, width)
        paragraph(c, p.get("biography"), 24, 280, 432, 5, 16)
        c.text(p.get("biographyCredit"), 24, 421, 10 / s.font_scale, s.muted, 432)
        c.text(p.get("photoCredit"), 24, 438, 10 / s.font_scale, s.muted, 432)
        c.footer("MUSICBRAINZ · WIKIPEDIA · COMMONS", width=432)
    else:
        has_photo = portrait(c, p, 40, 82, 300, 304)
        x = 380 if has_photo else 48
        width = 1232 - x
        c.text(p.get("name"), x, 116, min(38, 44 / s.font_scale), width=width, bold=True)
        facts = [clean(fact.get("value")) for fact in p.get("facts", [])[:3]]
        c.caption(" · ".join(facts), x, 150, tint=s.accent, width=width)
        paragraph(c, p.get("biography"), x, 192, width, 6, 20)
        sales = p.get("albumSales")
        if isinstance(sales, dict):
            c.caption(f"{sales.get('label')} · {sales.get('units'):,.0f} · {sales.get('asOf')}", x, 395, tint=s.accent, width=width)
        c.text(p.get("photoCredit"), 40, 414, 12 / s.font_scale, s.muted, 1200)
        c.text(p.get("biographyCredit"), 40, 439, 12 / s.font_scale, s.muted, 1200)
        c.footer(clean(p.get("sourceUrl")), width=1180)
# VBWR E
