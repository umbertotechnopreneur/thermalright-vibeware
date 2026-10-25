# VBWR B — Thermalright VibeWare. Copyright (c) 2026 Umberto Giacobbi. MIT.
"""Build Windows icon sizes from two first-party SVG sources using the scanned Skia runtime."""
from pathlib import Path
import struct
import skia

root = Path(__file__).resolve().parents[2]
assets = root / 'src/ThermalrightVibeWare.App/Assets'
images = []
for size in (16, 24, 32, 48, 64, 128, 256):
    source = assets / ('thermalright-vibeware-mark.svg' if size <= 32 else 'thermalright-vibeware-app.svg')
    dom = skia.SVGDOM.MakeFromStream(skia.MemoryStream(source.read_bytes()))
    surface = skia.Surface(size, size)
    surface.getCanvas().clear(skia.ColorTRANSPARENT)
    surface.getCanvas().scale(size / (16 if size <= 32 else 256), size / (16 if size <= 32 else 256))
    dom.render(surface.getCanvas())
    png = bytes(surface.makeImageSnapshot().encodeToData())
    images.append((size, png))
    if size == 256:
        (assets / 'thermalright-vibeware-icon.png').write_bytes(png)
header = struct.pack('<HHH', 0, 1, len(images))
offset = 6 + 16 * len(images)
entries, data = [], []
for size, png in images:
    entries.append(struct.pack('<BBBBHHII', size % 256, size % 256, 0, 0, 1, 32, len(png), offset))
    data.append(png)
    offset += len(png)
(assets / 'thermalright-vibeware.ico').write_bytes(header + b''.join(entries) + b''.join(data))
print('Built 16, 24, 32, 48, 64, 128 and 256 px Thermalright VibeWare display icons.')
# VBWR E
