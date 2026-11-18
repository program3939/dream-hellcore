"""Dreamy Hellcore - Konzeptbilder (Hellstage 1, 100, 1000, 10000).
Kleiner numpy-Raytracer fuer achsparallele Quader mit Z-Nebel. Aufruf: python make_concepts.py
"""
import math, colorsys, os
import numpy as np
from PIL import Image, ImageDraw

RW, RH = 960, 540
OUT = os.path.dirname(os.path.abspath(__file__))


def lerp(a, b, t): return a + (b - a) * t
def sstep(e0, e1, x):
    t = min(1.0, max(0.0, (x - e0) / (e1 - e0))); return t * t * (3 - 2 * t)
def hsv(h, s, v): return np.array(colorsys.hsv_to_rgb(h % 1.0, min(1, max(0, s)), min(1, max(0, v))))


def dims(n):
    s = math.sqrt(min(n, 10000)); ex = math.log10(max(n, 10000) / 10000)
    W = 12 + 4 * s + 100 * ex; H = 7 + 2.5 * s + 60 * ex; L = 60 + 6 * s + 150 * ex
    return W, H, L, min(math.log10(n) / 4, 1.25)


def palette(n, a):
    # Konzept Phase 2 (2026-11-16): dunkler Bogen Traum -> Hoelle -> Leere, Stuetzfarben bei a = 0 / 0.5 / 0.75 / 1.0
    def key(k0, k1, k2, k3):
        ks = [np.array(k0), np.array(k1), np.array(k2), np.array(k3)]; ps = [0.0, 0.5, 0.75, 1.0]
        for i in range(3):
            if a <= ps[i + 1]:
                return lerp(ks[i], ks[i + 1], sstep(ps[i], ps[i + 1], a))
        return ks[3]
    wall = key((0.40, 0.38, 0.62), (0.34, 0.10, 0.08), (0.13, 0.12, 0.13), (0.10, 0.06, 0.14))
    plat = key((0.92, 0.88, 0.82), (0.62, 0.50, 0.44), (0.46, 0.45, 0.45), (0.50, 0.55, 0.40))
    fog = key((0.035, 0.03, 0.09), (0.05, 0.008, 0.005), (0.008, 0.008, 0.01), (0.012, 0.02, 0.006))
    emis = key((1.0, 0.82, 0.62), (1.0, 0.25, 0.08), (0.9, 0.08, 0.05), (0.55, 1.0, 0.35))
    abyss = key((0.03, 0.03, 0.12), (0.30, 0.035, 0.0), (0.08, 0.0, 0.0), (0.02, 0.05, 0.0))
    return wall, plat, fog, emis, abyss, sstep(0.6, 1.0, a)


def scene(n):
    W, H, L, a = dims(n)
    wall, plat, fog, emis, abyss, weird = palette(n, a)
    r = np.random.default_rng(n)
    D = 4 + H * 0.5
    fog1 = 0.7 * L
    z0, z1 = -30.0, fog1 * 1.25
    B = []
    def add(x0, y0, zz0, x1, y1, zz1, col, e=0.0):
        B.append((min(x0, x1), min(y0, y1), min(zz0, zz1), max(x0, x1), max(y0, y1), max(zz0, zz1), *col, e))
    add(-W / 2 - W, -D, z0, -W / 2, H, z1, wall)
    add(W / 2, -D, z0, W / 2 + W, H, z1, wall)
    add(-W / 2, H, z0, W / 2, H + W, z1, wall * 0.7)
    add(-W / 2, -D - W, z0, W / 2, -D, z1, abyss, 0.6)
    # Wandrelief: regelmaessige Rippen (niedrig) -> Zaehne/Stacheln (hoch)
    cs = max(2.5, W / 7)
    for side in (-1, 1):
        for iz in range(int((z1 - z0) / cs)):
            for iy in range(int((H + D) / cs) + 1):
                if a < 0.2 and iz % 2 == 0:
                    dp = 0.45
                elif r.random() < 0.25 + 0.45 * a:
                    dp = cs * (0.1 + r.random() * (0.3 + 2.4 * a * a))
                else:
                    continue
                hh = cs * (1 - 0.8 * a * r.random()); ww = cs * (1 - 0.7 * a * r.random())
                zc = z0 + (iz + 0.5) * cs; yc = -D + (iy + 0.5) * cs
                sh = 0.75 + 0.5 * r.random()
                add(side * W / 2, yc - hh / 2, zc - ww / 2, side * (W / 2 - dp), yc + hh / 2, zc + ww / 2, wall * sh,
                    0.9 if r.random() < 0.05 * a else 0.0)
    # Pfad aus Plattformen
    x = y = 0.0; z = 6.0
    add(-4, -D if a < 0.25 else -0.8, -4, 4, 0, 6, plat * 0.9)
    while z < z1:
        gap = lerp(1.5, 4.5, r.random() * (0.3 + 0.7 * min(1, a)))
        ln = lerp(lerp(3, 7, r.random()), lerp(1.2, 9, r.random() ** 2), a)
        wd = lerp(lerp(3, 5, r.random()), lerp(0.9, 5, r.random() ** 2), a)
        x = max(-W / 2 + 3, min(W / 2 - 3, x + (r.random() - 0.5) * (3 + 6 * a)))
        y += (r.random() - 0.55) * (0.8 + 2.0 * a); y = max(-D * 0.4, min(H * 0.3, y))
        th = D + y if a < 0.25 else lerp(0.5, 0.3 + 6 * r.random() ** 3, a)
        add(x - wd / 2, y - th, z + gap, x + wd / 2, y, z + gap + ln, plat * (0.8 + 0.3 * r.random()))
        z += gap + ln
    # Deckenmonolithen, schwebende Koerper, Ringe, Beobachter
    for _ in range(int(4 + 40 * a)):
        s = lerp(1.0, W * 0.12, r.random() ** 2) * (0.5 + a)
        cx = (r.random() - 0.5) * (W - s); cz = r.random() * (z1 - 10) + 8
        ln = r.random() * H * (0.15 + 0.5 * a)
        add(cx - s / 2, H - ln, cz - s / 2, cx + s / 2, H, cz + s / 2, wall * 0.55)
    for _ in range(int(60 * max(0, a - 0.3))):
        s = lerp(0.6, W * 0.09, r.random() ** 3)
        cx = (r.random() - 0.5) * (W - s); cy = lerp(-D * 0.6, H * 0.9, r.random()); cz = r.random() * z1 + 10
        if abs(cx) < 6 + s and abs(cy) < 4 + s: continue
        glow = r.random() < 0.12
        add(cx - s / 2, cy - s / 2, cz - s / 2, cx + s / 2, cy + s / 2, cz + s / 2,
            emis if glow else wall * 0.8, 1.0 if glow else 0.0)
    if a > 0.6:
        nr = int(5 - 2 * weird)
        for k in range(1, nr + 1):
            zc = k * fog1 / (nr + 0.5); rad = min(W, H + D) * 0.36; s = rad * 0.16
            for i in range(18):
                an = i / 18 * 6.283 + k; cx = math.cos(an) * rad; cy = (H - D) / 2 + math.sin(an) * rad
                add(cx - s / 2, cy - s / 2, zc - s / 3, cx + s / 2, cy + s / 2, zc + s / 3, emis * 0.9, 0.8 - 0.55 * weird)
    for _ in range(int(3 + 14 * a)):  # Beobachter: duenne Gestalten mit zwei leuchtenden Augen
        sc = 1 + 18 * a * r.random(); side = 1 if r.random() < 0.5 else -1
        cx = side * (W / 2 - 1.2 * sc); cz = 10 + r.random() * fog1 * 0.8; by = lerp(-1, H * 0.4, r.random()) * (a > 0.2)
        add(cx - 0.3 * sc, by - 3 * sc, cz - 0.3 * sc, cx + 0.3 * sc, by + 2.4 * sc, cz + 0.3 * sc, wall * 0.15)
        add(cx - 0.45 * sc, by + 2.4 * sc, cz - 0.4 * sc, cx + 0.45 * sc, by + 3.2 * sc, cz + 0.4 * sc, wall * 0.15)
        for e in (-0.2, 0.2):
            add(cx - side * 0.46 * sc, by + 2.75 * sc, cz + (e - 0.07) * sc, cx - side * 0.40 * sc, by + 2.9 * sc,
                cz + (e + 0.07) * sc, np.array([1.0, 0.95, 0.8]), 1.0)
    # Parcours-Komplexitaet: Schienen (Achterbahn) als leuchtende Kette, mit dem Level mehr und wilder
    for k in range(int(3 * a + 0.5)):
        zs = 12 + k * fog1 * 0.22; ph = k * 2.1
        for i in range(int(26 + 30 * a)):
            t = i / (26 + 30 * a); zz = zs + t * fog1 * 0.5
            xx = math.sin(t * 6.283 * (1 + a) + ph) * W * 0.16; yy = 2 + math.cos(t * 6.283 * (1.5 + a) + ph) * (2 + 10 * a) - t * 8 * a
            s = 0.18 + 0.5 * a
            add(xx - s, yy - s * 0.4, zz - s, xx + s, yy + s * 0.4, zz + s, emis * 0.8, 0.9)
    return np.array(B), dict(W=W, H=H, L=L, a=a, D=D, fog=fog, fog1=fog1, abyss=abyss, weird=weird)


def render(n):
    B, P = scene(n)
    a = P['a']
    cam = np.array([0.0, 1.7, -2.0])
    fov = math.radians(78 + 14 * min(1, a))
    px = (np.arange(RW) + 0.5) / RW * 2 - 1; py = 1 - (np.arange(RH) + 0.5) / RH * 2
    X, Y = np.meshgrid(px * math.tan(fov / 2) * RW / RH, py * math.tan(fov / 2))
    roll = 0.10 * P['weird']; cr, sr = math.cos(roll), math.sin(roll)
    X, Y = X * cr - Y * sr, X * sr + Y * cr
    pitch = -0.04 + 0.10 * min(1, a)
    d = np.stack([X, Y * math.cos(pitch) + math.sin(pitch), np.cos(pitch) - Y * math.sin(pitch)], -1)
    d /= np.linalg.norm(d, axis=-1, keepdims=True)
    inv = 1.0 / np.where(np.abs(d) < 1e-9, 1e-9, d)
    best = np.full((RH, RW), 1e18); idx = np.full((RH, RW), -1); nax = np.zeros((RH, RW), int)
    for i, b in enumerate(B):
        t0 = (b[0:3] - cam) * inv; t1 = (b[3:6] - cam) * inv
        tn = np.minimum(t0, t1); tf = np.maximum(t0, t1)
        ax = np.argmax(tn, -1); tnear = np.max(tn, -1); tfar = np.min(tf, -1)
        hit = (tnear < tfar) & (tnear > 0.01) & (tnear < best)
        best[hit] = tnear[hit]; idx[hit] = i; nax[hit] = ax[hit]
    hitm = idx >= 0
    pos = cam + d * np.where(hitm, best, 0)[..., None]
    base = B[np.maximum(idx, 0), 6:9]; em = B[np.maximum(idx, 0), 9]
    nrm = np.zeros((RH, RW, 3))
    for k in range(3):
        m = nax == k; nrm[m, k] = -np.sign(d[m, k])
    # Abstraktion: Regenbogen-Verfremdung abhaengig vom Ort
    w = P['weird']
    rb = 0.5 + 0.5 * np.cos(6.283 * (pos[..., 1:2] * 0.013 + pos[..., 2:3] * 0.006 + pos[..., 0:1] * 0.004 + np.array([0, 0.33, 0.67])))
    base = base * (1 - 0.6 * w) + rb * 0.6 * w * (0.10 + 0.9 * base)
    ldir = np.array([0.25, 0.85, -0.45]); ldir /= np.linalg.norm(ldir)
    dif = np.clip(nrm @ ldir, 0, 1)
    lamp = 1.0 / (1 + (best / lerp(14, 170, min(1, a))) ** 2)
    light = (0.22 + 0.75 * dif)[..., None] * lerp(lamp, 1.0, 0.25 + 0.15 * min(1, a))[..., None]
    under = np.clip(-nrm[..., 1], 0, 1)[..., None] * P['abyss'] * 0.9
    col = base * light + base * under + base * em[..., None] * 1.4
    dz = np.abs(pos[..., 2] - cam[2])
    f = np.clip((dz - 0.12 * P['fog1']) / (0.88 * P['fog1']), 0, 1); f = f * f * (3 - 2 * f)
    g = np.clip((-pos[..., 1] - 1.5) / (P['D'] * 0.9), 0, 1) * 0.85
    col = col * (1 - g[..., None]) + P['abyss'] * 0.35 * g[..., None]
    col = col * (1 - f[..., None]) + P['fog'] * f[..., None]
    col[~hitm] = P['fog']
    r2 = (px[None, :] ** 2 + py[:, None] ** 2)
    col *= (1 - 0.38 * r2)[..., None]
    col += (np.random.default_rng(1).random((RH, RW, 1)) - 0.5) * 0.035
    img = Image.fromarray((np.clip(col, 0, 1) ** (1 / 1.9) * 255).astype(np.uint8))
    # Beschriftung nur im Konzeptbild (im Spiel gibt es kein HUD)
    ImageDraw.Draw(img).text((14, RH - 24), f"Dreamy Hellcore - Konzept: Hellstage {n}   "
                             f"(Raum {P['W']:.0f} m breit, {P['H']:.0f} m hoch, {P['L']:.0f} m lang)", fill=(235, 235, 235))
    path = os.path.join(OUT, f"konzept_hellstage_{n}.png"); img.save(path); print(path, len(B), "Quader")


if __name__ == "__main__":
    for n in (1, 100, 1000, 10000):
        render(n)
