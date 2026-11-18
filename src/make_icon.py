"""Erzeugt icon.ico (Windows-EXE) und icon.png (Fenster-Icon, Linux). Aufruf: python make_icon.py"""
import math
import os
from PIL import Image, ImageDraw, ImageFilter

here = os.path.dirname(os.path.abspath(__file__))
S = 256
img = Image.new("RGB", (S, S))
px = img.load()
for y in range(S):
    for x in range(S):
        d = math.hypot(x - S / 2, y - S * 0.44) / S
        g = max(0.0, 1 - d * 2.3) ** 2            # fernes Leuchten im Nebel
        px[x, y] = (int(10 + 235 * g), int(9 + 215 * g), int(28 + 227 * g))
fig = Image.new("L", (S, S), 0)
d = ImageDraw.Draw(fig)
cx = S // 2
d.ellipse((cx - 13, 62, cx + 13, 94), fill=255)                      # Kopf
d.rounded_rectangle((cx - 16, 96, cx + 16, 172), 12, fill=255)       # Rumpf
d.rounded_rectangle((cx - 30, 100, cx - 21, 200), 4, fill=255)       # zu lange Arme
d.rounded_rectangle((cx + 21, 100, cx + 30, 200), 4, fill=255)
d.rounded_rectangle((cx - 14, 168, cx - 4, 256), 4, fill=255)        # Beine
d.rounded_rectangle((cx + 4, 168, cx + 14, 256), 4, fill=255)
fig = fig.filter(ImageFilter.GaussianBlur(1.2))
img.paste((2, 2, 6), mask=fig)
img.save(os.path.join(here, "icon.png"))
img.save(os.path.join(here, "icon.ico"), sizes=[(256, 256), (64, 64), (48, 48), (32, 32), (16, 16)])
print("icon ok")
