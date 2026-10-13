"""Paints the proprietor's portrait for the corridor wall:
Assets/XIII/Resources/XIII_Portrait.png (384 x 512).

    python3 tools/portrait.py [preview.png]

The eyes are left as dark almond hollows: the game sets real eyes into the canvas there
(EYES below, in 0..1 canvas coordinates from the bottom-left, must match Corridor.PortraitEyes).
"""
import math
import os
import random
import sys

from PIL import Image, ImageChops, ImageDraw, ImageFilter

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, "Assets", "XIII", "Resources", "XIII_Portrait.png")
W, H = 384, 512
S = 2  # paint at 2x, then downsample
EYES = [(0.425, 0.645), (0.575, 0.645)]  # u, v (v from the bottom)
EYE_W, EYE_H = 0.07, 0.03

rng = random.Random(1913)


def P(u, v):
    """Canvas coords (u right, v down, 0..1) to pixels at paint scale."""
    return (u * W * S, v * H * S)


def soft(img, shape_fn, color, blur, alpha=255):
    """Paint a blurred shape onto img."""
    mask = Image.new("L", img.size, 0)
    shape_fn(ImageDraw.Draw(mask), alpha)
    if blur:
        mask = mask.filter(ImageFilter.GaussianBlur(blur * S))
    img.paste(Image.new("RGB", img.size, color), (0, 0), mask)


def ell(cx, cy, rx, ry):
    def f(d, a):
        x, y = P(cx, cy)
        d.ellipse([x - rx * W * S, y - ry * H * S, x + rx * W * S, y + ry * H * S], fill=a)
    return f


def poly(pts):
    def f(d, a):
        d.polygon([P(u, v) for u, v in pts], fill=a)
    return f


def paint():
    img = Image.new("RGB", (W * S, H * S))
    px = img.load()
    # Background: dark olive, warmer behind the head.
    for y in range(H * S):
        for x in range(W * S):
            u, v = x / (W * S), y / (H * S)
            d = math.hypot((u - 0.46) * 1.2, v - 0.34)
            k = max(0.0, 1 - d / 0.62) ** 1.6
            base = (34 - 12 * v, 33 - 12 * v, 22 - 8 * v)
            px[x, y] = tuple(int(b + (c - b) * k) for b, c in zip(base, (92, 80, 50)))

    # Suit and shoulders, lit from the upper left.
    suit = [(0.02, 1.0), (0.04, 0.76), (0.14, 0.66), (0.38, 0.565), (0.62, 0.565), (0.86, 0.66), (0.97, 0.76), (0.99, 1.0)]
    soft(img, poly(suit), (20, 17, 16), 2)
    soft(img, poly([(0.06, 0.78), (0.16, 0.67), (0.36, 0.59), (0.3, 0.68), (0.12, 0.82)]), (44, 38, 34), 10, 160)
    # Waistcoat, shirt front, wing collar, cravat.
    soft(img, poly([(0.36, 0.59), (0.64, 0.59), (0.6, 1.0), (0.4, 1.0)]), (58, 20, 19), 2)
    soft(img, poly([(0.42, 0.58), (0.58, 0.58), (0.53, 0.82), (0.47, 0.82)]), (196, 186, 160), 2)
    soft(img, poly([(0.43, 0.56), (0.5, 0.6), (0.39, 0.64)]), (214, 205, 178), 1)
    soft(img, poly([(0.57, 0.56), (0.5, 0.6), (0.61, 0.64)]), (170, 160, 136), 1)
    soft(img, poly([(0.45, 0.595), (0.55, 0.595), (0.53, 0.66), (0.5, 0.63), (0.47, 0.66)]), (14, 12, 12), 1)
    soft(img, ell(0.5, 0.635, 0.008, 0.006), (190, 150, 70), 0.5)
    # Lapels.
    soft(img, poly([(0.36, 0.58), (0.43, 0.62), (0.4, 0.78), (0.33, 0.66)]), (30, 26, 24), 2)
    soft(img, poly([(0.64, 0.58), (0.57, 0.62), (0.6, 0.78), (0.67, 0.66)]), (12, 10, 10), 2)

    # Neck.
    soft(img, poly([(0.44, 0.5), (0.56, 0.5), (0.57, 0.58), (0.43, 0.58)]), (150, 128, 100), 2)
    soft(img, poly([(0.51, 0.5), (0.57, 0.5), (0.57, 0.58), (0.52, 0.58)]), (98, 80, 62), 4, 200)

    # A long, gaunt face.
    face = [(0.36, 0.3), (0.39, 0.2), (0.5, 0.16), (0.61, 0.2), (0.64, 0.3), (0.63, 0.42), (0.58, 0.5),
            (0.53, 0.535), (0.47, 0.535), (0.42, 0.5), (0.37, 0.42)]
    soft(img, poly(face), (196, 174, 140), 2)
    soft(img, poly([(0.52, 0.16), (0.64, 0.25), (0.63, 0.43), (0.55, 0.53), (0.52, 0.54)]), (108, 88, 66), 7, 235)  # shadow side
    soft(img, ell(0.43, 0.29, 0.06, 0.06), (220, 200, 165), 10, 150)   # lit forehead
    soft(img, ell(0.41, 0.46, 0.035, 0.05), (128, 104, 80), 7, 170)    # hollow cheeks
    soft(img, ell(0.6, 0.46, 0.035, 0.05), (92, 74, 58), 7, 200)
    soft(img, ell(0.5, 0.53, 0.04, 0.02), (205, 184, 150), 5, 120)     # chin light
    # Eye sockets and the almond hollows where the real eyes sit.
    for (eu, ev), dark in zip(EYES, ((96, 76, 58), (70, 54, 42))):
        cv = 1 - ev
        soft(img, ell(eu, cv, 0.065, 0.04), dark, 6, 230)
        soft(img, ell(eu, cv, EYE_W / 2 + 0.006, EYE_H / 2 + 0.004), (36, 26, 20), 1.5)
        soft(img, poly([(eu - 0.045, cv - 0.012), (eu, cv - 0.03), (eu + 0.045, cv - 0.012), (eu, cv - 0.022)]), (40, 30, 24), 1.2)  # lid crease
        soft(img, poly([(eu - 0.05, cv - 0.05), (eu, cv - 0.065), (eu + 0.05, cv - 0.048), (eu, cv - 0.056)]), (34, 26, 20), 1.5)  # brow
    # Nose: lit ridge, shadowed side, nostrils.
    soft(img, poly([(0.49, 0.34), (0.51, 0.34), (0.53, 0.45), (0.47, 0.45)]), (214, 192, 156), 3, 170)
    soft(img, poly([(0.51, 0.34), (0.545, 0.44), (0.52, 0.455)]), (110, 88, 66), 4, 200)
    soft(img, ell(0.475, 0.455, 0.012, 0.006), (60, 44, 34), 1.5)
    soft(img, ell(0.525, 0.455, 0.012, 0.006), (50, 36, 28), 1.5)
    # Thin mustache and a mouth that is almost a smile.
    soft(img, poly([(0.42, 0.485), (0.46, 0.47), (0.5, 0.475), (0.54, 0.47), (0.58, 0.485), (0.54, 0.48), (0.5, 0.485), (0.46, 0.48)]), (30, 22, 18), 1.2)
    soft(img, poly([(0.44, 0.502), (0.5, 0.506), (0.56, 0.5), (0.5, 0.511)]), (70, 36, 32), 1)
    # Hair: slicked, centre parting, sideburns.
    hair = [(0.35, 0.3), (0.36, 0.2), (0.42, 0.14), (0.5, 0.125), (0.58, 0.14), (0.64, 0.2), (0.65, 0.3), (0.62, 0.25),
            (0.56, 0.2), (0.505, 0.19), (0.495, 0.19), (0.44, 0.2), (0.38, 0.25)]
    soft(img, poly(hair), (28, 22, 17), 2)
    soft(img, poly([(0.35, 0.29), (0.38, 0.27), (0.385, 0.38), (0.365, 0.37)]), (30, 24, 18), 2)
    soft(img, poly([(0.65, 0.29), (0.62, 0.27), (0.615, 0.38), (0.635, 0.37)]), (22, 18, 14), 2)
    soft(img, poly([(0.42, 0.155), (0.49, 0.135), (0.47, 0.16)]), (70, 58, 44), 3, 150)  # sheen
    # Ears.
    soft(img, ell(0.355, 0.36, 0.014, 0.04), (150, 122, 92), 2)
    soft(img, ell(0.645, 0.36, 0.014, 0.04), (96, 76, 58), 2)
    return img


def brushwork(img):
    """Short directional strokes of light and dark, then a little softening."""
    strokes = Image.new("RGB", img.size, (128, 128, 128))
    d = ImageDraw.Draw(strokes)
    for _ in range(9000):
        x, y = rng.uniform(0, img.width), rng.uniform(0, img.height)
        a = rng.uniform(-0.6, 0.6) + (math.pi / 2 if rng.random() < 0.35 else 0)
        ln = rng.uniform(6, 22) * S
        g = int(128 + rng.uniform(-36, 36))
        d.line([(x, y), (x + math.cos(a) * ln, y + math.sin(a) * ln)], fill=(g, g, g), width=rng.choice((2, 3, 4)))
    strokes = strokes.filter(ImageFilter.GaussianBlur(1.0 * S))
    img = img.filter(ImageFilter.GaussianBlur(0.5 * S))
    return ImageChops.overlay(img, strokes)


def varnish(img):
    """Yellowed varnish, a dark vignette, and fine craquelure."""
    w, h = img.size
    tint = Image.new("RGB", img.size, (255, 236, 196))
    img = ImageChops.multiply(img, tint)
    vig = Image.new("L", img.size, 0)
    vp = vig.load()
    for y in range(h):
        for x in range(w):
            d = math.hypot((x / w - 0.5) * 1.1, (y / h - 0.45))
            vp[x, y] = int(255 * min(1.0, max(0.0, (d - 0.32) / 0.4)) * 0.75)
    img.paste(Image.new("RGB", img.size, (6, 5, 3)), (0, 0), vig)
    cracks = Image.new("L", img.size, 0)
    d = ImageDraw.Draw(cracks)
    for _ in range(200):
        x, y = rng.uniform(0, w), rng.uniform(0, h)
        a = rng.uniform(0, 2 * math.pi)
        for _ in range(rng.randint(3, 7)):
            a += rng.uniform(-0.9, 0.9)
            nx, ny = x + math.cos(a) * rng.uniform(8, 26) * S, y + math.sin(a) * rng.uniform(8, 26) * S
            d.line([(x, y), (nx, ny)], fill=20, width=1)
            x, y = nx, ny
    img.paste(Image.new("RGB", img.size, (10, 8, 6)), (0, 0), cracks)
    return img


if __name__ == "__main__":
    img = varnish(brushwork(paint())).resize((W, H), Image.LANCZOS)
    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    img.save(OUT, optimize=True)
    print("wrote", OUT, os.path.getsize(OUT) // 1024, "KB")
    print("eyes (u, v from bottom):", EYES, "size", (EYE_W, EYE_H))
    if len(sys.argv) > 1:
        # Preview with the in-game eyes laid over the hollows.
        prev = img.resize((W * 2, H * 2), Image.LANCZOS)
        d = ImageDraw.Draw(prev)
        for k, (u, v) in enumerate(EYES):
            cx, cy = u * W * 2, (1 - v) * H * 2
            ew, eh = EYE_W * W * 2 / 2, EYE_H * H * 2 / 2
            d.ellipse([cx - ew, cy - eh, cx + ew, cy + eh], fill=(160, 150, 128))
            off = 7 if k == 0 else 5
            d.ellipse([cx - 9 + off, cy - 9, cx + 9 + off, cy + 9], fill=(16, 12, 10))
        prev.save(sys.argv[1])
