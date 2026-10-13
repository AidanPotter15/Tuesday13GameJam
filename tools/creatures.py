"""Generates the black cat and the other guest as jointed rigs of smooth meshes,
and bakes them into Assets/XIII/Scripts/CreatureData.cs.

    python3 tools/creatures.py [preview_dir]

Coordinates match Unity: x right, y up, z forward (the creature faces +z).
Every part is built in rest-pose world space, then stored relative to its own pivot,
so the game can rotate legs, heads and tails about their joints.
"""
import math
import os
import sys

import numpy as np

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT_CS = os.path.join(ROOT, "Assets", "XIII", "Scripts", "CreatureData.cs")


# ---------------------------------------------------------------- mesh helpers

def norm(v):
    v = np.asarray(v, float)
    n = np.linalg.norm(v, axis=-1, keepdims=True)
    return v / np.where(n == 0, 1, n)


class Mesh:
    def __init__(self, v, t):
        self.v = np.asarray(v, float).reshape(-1, 3)
        self.t = np.asarray(t, int).reshape(-1, 3)

    def oriented(self):
        """Wind triangles so cross(b-a, c-a) points outward (what Unity treats as the front face)."""
        a, b, c = (self.v[self.t[:, k]] for k in range(3))
        if np.einsum("ij,ij->i", a, np.cross(b, c)).sum() < 0:
            self.t = self.t[:, [0, 2, 1]]
        return self


def merge(*meshes):
    v, t, off = [], [], 0
    for m in meshes:
        v.append(m.v)
        t.append(m.t + off)
        off += len(m.v)
    return Mesh(np.vstack(v), np.vstack(t))


def rings_to_mesh(rings, start_pole, end_pole):
    """rings: list of (M,3) loops. Poles close the ends."""
    L, M = len(rings), len(rings[0])
    v = np.vstack(rings + [np.asarray([start_pole]), np.asarray([end_pole])])
    ps, pe = L * M, L * M + 1
    t = []
    for i in range(L - 1):
        for j in range(M):
            a, b = i * M + j, i * M + (j + 1) % M
            c, d = (i + 1) * M + j, (i + 1) * M + (j + 1) % M
            t += [(a, c, b), (b, c, d)]
    for j in range(M):
        t.append((ps, j, (j + 1) % M))
        t.append(((L - 1) * M + j, pe, (L - 1) * M + (j + 1) % M))
    return Mesh(v, t).oriented()


def loft(points, rx, ry=None, M=14, ref=(0, 0, 1), round_ends=True, sections=None, wobble=None):
    """Tube through `points` (smoothly resampled). rx/ry: radius per point (interpolated).
    ref: a direction that is never parallel to the path, used to orient the cross-sections.
    wobble(theta, t) -> radius multiplier, for folds."""
    pts = np.asarray(points, float)
    rx = np.asarray(rx, float)
    ry = rx if ry is None else np.asarray(ry, float)
    # resample along a Catmull-Rom curve
    n = sections or max(8, len(pts) * 6)
    seg = len(pts) - 1
    P = np.vstack([pts[0] * 2 - pts[1], pts, pts[-1] * 2 - pts[-2]])
    C, RX, RY, T = [], [], [], []
    for k in range(n):
        u = k / (n - 1) * seg
        i = min(int(u), seg - 1)
        s = u - i
        p0, p1, p2, p3 = P[i], P[i + 1], P[i + 2], P[i + 3]
        C.append(0.5 * ((2 * p1) + (-p0 + p2) * s + (2 * p0 - 5 * p1 + 4 * p2 - p3) * s * s + (-p0 + 3 * p1 - 3 * p2 + p3) * s ** 3))
        RX.append(rx[i] + (rx[i + 1] - rx[i]) * s)
        RY.append(ry[i] + (ry[i + 1] - ry[i]) * s)
        T.append(k / (n - 1))
    C = np.asarray(C)
    tang = norm(np.gradient(C, axis=0))
    rings = []

    def ring(c, tg, a, b, tt):
        side = norm(np.cross(ref, tg))
        up = np.cross(tg, side)
        th = np.linspace(0, 2 * math.pi, M, endpoint=False)
        w = np.ones(M) if wobble is None else np.array([wobble(x, tt) for x in th])
        return c + np.outer(np.cos(th) * a * w, side) + np.outer(np.sin(th) * b * w, up)

    caps = 3 if round_ends else 0
    for k in range(caps, 0, -1):  # rounded start
        f = math.cos(k * math.pi / (2 * (caps + 1)))
        g = math.sin(k * math.pi / (2 * (caps + 1)))
        rings.append(ring(C[0] - tang[0] * RX[0] * 0.8 * g, tang[0], RX[0] * f, RY[0] * f, 0))
    for c, tg, a, b, tt in zip(C, tang, RX, RY, T):
        rings.append(ring(c, tg, a, b, tt))
    for k in range(1, caps + 1):  # rounded end
        f = math.cos(k * math.pi / (2 * (caps + 1)))
        g = math.sin(k * math.pi / (2 * (caps + 1)))
        rings.append(ring(C[-1] + tang[-1] * RX[-1] * 0.8 * g, tang[-1], RX[-1] * f, RY[-1] * f, 1))
    sp = C[0] - tang[0] * RX[0] * (0.8 if round_ends else 0)
    ep = C[-1] + tang[-1] * RX[-1] * (0.8 if round_ends else 0)
    return rings_to_mesh(rings, sp, ep)


def blob(center, radii, deform=None, L=18, M=24):
    """Ellipsoid whose surface is pushed in/out along its normal by deform(dir) (metres)."""
    center = np.asarray(center, float)
    radii = np.asarray(radii, float)
    rings = []
    for i in range(1, L):
        phi = math.pi * i / L  # 0 at +y
        th = np.linspace(0, 2 * math.pi, M, endpoint=False)
        d = np.stack([np.sin(phi) * np.cos(th), np.full(M, math.cos(phi)), np.sin(phi) * np.sin(th)], 1)
        p = d * radii
        if deform is not None:
            n = norm(d / radii)
            p = p + n * np.array([deform(x) for x in d])[:, None]
        rings.append(center + p)
    top = center + np.array([0, radii[1], 0]) + (np.array([0, deform(np.array([0, 1.0, 0])), 0]) if deform else 0)
    bot = center - np.array([0, radii[1], 0]) - (np.array([0, deform(np.array([0, -1.0, 0])), 0]) if deform else 0)
    return rings_to_mesh(rings, top, bot)


def bump(d, toward, amp, sigma):
    toward = norm(toward)
    ang = math.acos(max(-1.0, min(1.0, float(np.dot(d, toward)))))
    return amp * math.exp(-(ang / sigma) ** 2)


def rot(m, deg_x=0.0, deg_y=0.0, deg_z=0.0, about=(0, 0, 0)):
    """Rotate a mesh about a point (applied Z, then X, then Y, like Unity's Euler)."""
    ax, ay, az = (math.radians(a) for a in (deg_x, deg_y, deg_z))
    Rx = np.array([[1, 0, 0], [0, math.cos(ax), -math.sin(ax)], [0, math.sin(ax), math.cos(ax)]])
    Ry = np.array([[math.cos(ay), 0, math.sin(ay)], [0, 1, 0], [-math.sin(ay), 0, math.cos(ay)]])
    Rz = np.array([[math.cos(az), -math.sin(az), 0], [math.sin(az), math.cos(az), 0], [0, 0, 1]])
    R = Ry @ Rx @ Rz
    about = np.asarray(about, float)
    return Mesh((m.v - about) @ R.T + about, m.t)


def ear(base, w, h, d, tilt_z, yaw):
    """A cat ear: a thin, slightly cupped triangle."""
    v = np.array([
        [-w / 2, 0, 0], [w / 2, 0, 0], [0, h, -d * 0.2],      # front face
        [-w / 2, 0, -d], [w / 2, 0, -d], [0, h * 0.95, -d * 0.6],  # back face
        [0, h * 0.35, 0.004],                                 # cup bulge
    ])
    t = [(0, 6, 1), (1, 6, 2), (2, 6, 0), (3, 4, 5), (0, 3, 5), (0, 5, 2), (1, 2, 5), (1, 5, 4), (0, 1, 4), (0, 4, 3)]
    m = Mesh(v, t).oriented()
    m = rot(m, 0, yaw, tilt_z)
    return Mesh(m.v + np.asarray(base, float), m.t)


# ---------------------------------------------------------------- rig

class Rig:
    def __init__(self, name):
        self.name = name
        self.parts = []  # (name, parent, pivot, mesh_key, mesh, mat)

    def add(self, name, parent, pivot, mesh=None, mat=0):
        key = (self.name + "_" + name).lower() if mesh is not None else None
        self.parts.append((name, parent, np.asarray(pivot, float), key, mesh, mat))

    def index(self, name):
        return [p[0] for p in self.parts].index(name)


# ---------------------------------------------------------------- the black cat

def build_cat():
    r = Rig("Cat")
    FUR, EYE, NOSE, PUPIL = 0, 1, 2, 3

    # Torso: hips, a low belly, deep chest.
    z = [-0.235, -0.205, -0.15, -0.08, 0.0, 0.07, 0.13, 0.175, 0.205]
    y = [0.25, 0.25, 0.246, 0.238, 0.232, 0.235, 0.24, 0.246, 0.25]
    rx = [0.035, 0.064, 0.075, 0.078, 0.076, 0.072, 0.07, 0.061, 0.042]
    ry = [0.04, 0.072, 0.083, 0.086, 0.09, 0.093, 0.096, 0.083, 0.052]
    body = loft([(0, yy, zz) for yy, zz in zip(y, z)], rx, ry, M=18, ref=(0, 1, 0), sections=30)
    r.add("Body", -1, (0, 0.24, 0), body, FUR)

    # Neck + head, pivoting at the base of the neck.
    neck = loft([(0, 0.255, 0.14), (0, 0.29, 0.19), (0, 0.322, 0.222)], [0.05, 0.047, 0.043], M=14, ref=(1, 0, 0))
    hc = np.array([0, 0.352, 0.25])
    muzzle = (0, -0.45, 0.89)

    def head_shape(d):
        out = bump(d, muzzle, 0.019, 0.36)
        out += bump(d, (0.72, -0.38, 0.58), 0.011, 0.38) + bump(d, (-0.72, -0.38, 0.58), 0.011, 0.38)
        out -= bump(d, (0.42, 0.14, 0.9), 0.006, 0.18) + bump(d, (-0.42, 0.14, 0.9), 0.006, 0.18)
        out -= 0.008 * max(0.0, d[1]) ** 3  # flatter crown
        return out

    head = blob(hc, (0.07, 0.06, 0.063), head_shape, L=20, M=28)
    ears = merge(ear(hc + (-0.038, 0.046, -0.004), 0.052, 0.056, 0.015, 16, 12),
                 ear(hc + (0.038, 0.046, -0.004), 0.052, 0.056, 0.015, -16, -12))
    r.add("Head", "Body", (0, 0.27, 0.16), merge(neck, head, ears), FUR)
    nose_at = hc + norm(np.array(muzzle)) * np.array([0.07, 0.06, 0.063]) + np.array([0, 0.004, 0.013])
    r.add("Nose", "Head", nose_at, blob(nose_at, (0.009, 0.006, 0.006), L=8, M=10), NOSE)
    for side, nm in ((-1, "L"), (1, "R")):
        e = hc + np.array([side * 0.028, 0.012, 0.054])
        eye = rot(blob(e, (0.0135, 0.0095, 0.006), L=10, M=14), 0, side * 18, side * -8, about=e)
        r.add("Eye" + nm, "Head", e, eye, EYE)
        p = e + np.array([side * 0.0015, 0, 0.0055])
        r.add("Pupil" + nm, "Eye" + nm, p, rot(blob(p, (0.0022, 0.0085, 0.0018), L=8, M=10), 0, side * 18, 0, about=p), PUPIL)

    # Legs: two segments each, pivoting at shoulder/hip and elbow/knee.
    for side, nm in ((-1, "L"), (1, "R")):
        x = side * 0.047
        sh, el, paw = (x, 0.21, 0.125), (x, 0.112, 0.13), (x, 0.022, 0.128)
        r.add("Front" + nm, "Body", sh, loft([sh, (x, 0.16, 0.13), el], [0.031, 0.026, 0.021], M=12), FUR)
        lower = merge(loft([el, (x, 0.07, 0.13), paw], [0.019, 0.016, 0.015], M=12),
                      blob((x, 0.013, 0.14), (0.019, 0.012, 0.026), L=8, M=12))
        r.add("Front" + nm + "2", "Front" + nm, el, lower, FUR)

        x = side * 0.052
        hip, knee, hock, paw = (x, 0.228, -0.15), (x, 0.13, -0.108), (x, 0.06, -0.176), (x, 0.022, -0.165)
        r.add("Hind" + nm, "Body", hip, loft([hip, (x, 0.18, -0.12), knee], [0.042, 0.034, 0.022], M=12), FUR)
        lower = merge(loft([knee, (x, 0.095, -0.15), hock, paw], [0.019, 0.016, 0.014, 0.014], M=12),
                      blob((x, 0.013, -0.152), (0.019, 0.012, 0.027), L=8, M=12))
        r.add("Hind" + nm + "2", "Hind" + nm, knee, lower, FUR)

    # Tail: a question mark in two joints so it can swish.
    t0, t1 = (0, 0.256, -0.218), (0, 0.393, -0.36)
    r.add("Tail", "Body", t0, loft([t0, (0, 0.278, -0.29), (0, 0.333, -0.34), t1], [0.021, 0.02, 0.018, 0.017], M=10), FUR)
    r.add("Tail2", "Tail", t1, loft([t1, (0, 0.468, -0.36), (0, 0.523, -0.33), (0, 0.543, -0.295)], [0.017, 0.015, 0.013, 0.011], M=10), FUR)
    return r


# ---------------------------------------------------------------- the other guest

def build_guest():
    r = Rig("Guest")
    COAT, SKIN, GLOVE, BRASS, HOLLOW, GLINT = 0, 1, 2, 3, 4, 5

    # A floor-length bellhop's coat: flared hem, pinched waist, square shoulders, a slight hunch.
    prof = [  # y, rx, rz, z-offset
        (0.0, 0.25, 0.2, 0.0), (0.12, 0.24, 0.19, 0.0), (0.55, 0.2, 0.16, 0.0), (0.95, 0.16, 0.125, 0.0),
        (1.2, 0.165, 0.125, 0.005), (1.45, 0.2, 0.13, 0.015), (1.62, 0.245, 0.125, 0.03), (1.71, 0.23, 0.11, 0.04),
        (1.77, 0.12, 0.085, 0.04), (1.8, 0.05, 0.05, 0.035),
    ]
    pts = [(0, p[0], p[3]) for p in prof]

    def folds(th, t):
        return 1 + 0.05 * math.sin(7 * th) * max(0.0, 1 - t * 1.8)

    coat = loft(pts, [p[1] for p in prof], [p[2] for p in prof], M=32, ref=(0, 0, 1), round_ends=False, sections=40, wobble=folds)
    buttons = merge(*[blob((sx, yy, 0.128 + (0.008 if yy > 1.3 else 0.0) + 0.01), (0.012, 0.012, 0.008), L=6, M=8)
                      for sx in (-0.055, 0.055) for yy in (1.12, 1.22, 1.32, 1.42, 1.52)])
    r.add("Body", -1, (0, 0, 0), coat, COAT)
    r.add("Buttons", "Body", (0, 1.3, 0.14), buttons, BRASS)

    # Neck and a long, gaunt head. Pivots at the base of the neck so it can tilt.
    neck = loft([(0, 1.76, 0.035), (0, 1.88, 0.045), (0, 1.99, 0.055)], [0.034, 0.032, 0.034], M=12, ref=(1, 0, 0))
    hc = np.array([0, 2.085, 0.06])

    def face(d):
        out = 0.0
        if d[1] < 0:  # long, narrow jaw
            out += 0.03 * (-d[1]) ** 1.5 * max(0.0, 1 - abs(d[0]) * 1.6)
            out -= 0.012 * abs(d[0]) * (-d[1])
        for s in (-1, 1):
            out -= bump(d, (s * 0.36, 0.16, 0.92), 0.028, 0.21)   # eye sockets
            out -= bump(d, (s * 0.62, -0.3, 0.72), 0.012, 0.28)   # hollow cheeks
            out += bump(d, (s * 0.33, 0.36, 0.87), 0.006, 0.18)   # brow
        out += 0.016 * math.exp(-((d[0] / 0.09) ** 2 + ((d[1] + 0.02) / 0.22) ** 2)) * max(0.0, d[2])  # nose ridge
        out -= 0.01 * math.exp(-((d[0] / 0.2) ** 2 + ((d[1] + 0.47) / 0.04) ** 2)) * max(0.0, d[2])   # thin mouth
        return out

    head = blob(hc, (0.083, 0.125, 0.094), face, L=28, M=36)
    ears = merge(*[blob(hc + (s * 0.081, 0.0, -0.008), (0.012, 0.03, 0.02), L=8, M=10) for s in (-1, 1)])
    r.add("Head", "Body", (0, 1.77, 0.035), merge(neck, head, ears), SKIN)
    collar = loft([(0, 1.745, 0.035), (0, 1.805, 0.042)], [0.056, 0.047], [0.052, 0.045], M=20, ref=(0, 0, 1), round_ends=False)
    r.add("Collar", "Body", (0, 1.77, 0.035), collar, GLOVE)
    sockets = []
    glints = []
    for s in (-1, 1):
        sc = hc + np.array([s * 0.031, 0.02, 0.0615])
        sockets.append(blob(sc, (0.019, 0.016, 0.008), L=8, M=12))
        glints.append(blob(sc + (s * -0.002, 0.001, 0.0065), (0.0022, 0.0022, 0.0015), L=6, M=8))
    r.add("Sockets", "Head", hc, merge(*sockets), HOLLOW)
    r.add("Glints", "Head", hc, merge(*glints), GLINT)
    hat_c = hc + np.array([0, 0.1, -0.016])
    hat = rot(loft([hat_c + (0, -0.03, 0), hat_c + (0, 0.04, 0)], [0.078, 0.074], [0.07, 0.066], M=24, ref=(0, 0, 1), round_ends=False), -8, 0, 0, about=hat_c)
    band = rot(loft([hat_c + (0, -0.032, 0), hat_c + (0, -0.012, 0)], [0.081, 0.081], [0.073, 0.073], M=24, ref=(0, 0, 1), round_ends=False), -8, 0, 0, about=hat_c)
    r.add("Hat", "Head", hat_c, hat, COAT)
    r.add("HatBand", "Head", hat_c, band, BRASS)

    # Arms far too long, hanging past the knees, in white gloves with long fingers.
    for s, nm in ((-1, "L"), (1, "R")):
        sh, el, wr = (s * 0.235, 1.64, 0.03), (s * 0.275, 1.2, 0.005), (s * 0.29, 0.79, 0.04)
        r.add("Arm" + nm, "Body", sh, loft([sh, (s * 0.26, 1.42, 0.01), el], [0.058, 0.052, 0.046], M=14), COAT)
        r.add("Arm" + nm + "2", "Arm" + nm, el, loft([el, (s * 0.285, 1.0, 0.02), wr], [0.046, 0.042, 0.036], M=14), COAT)
        palm = blob((s * 0.292, 0.73, 0.045), (0.02, 0.055, 0.042), L=10, M=12)
        fingers = []
        for k, dz in enumerate((-0.022, 0.0, 0.022, 0.042)):
            ln = 0.15 if k in (1, 2) else 0.125
            base = np.array([s * 0.292, 0.69, 0.045 + dz])
            fingers.append(loft([base, base + (s * -0.006, -ln * 0.55, 0.006), base + (s * -0.018, -ln, 0.016)], [0.0085, 0.0075, 0.006], M=8))
        r.add("Hand" + nm, "Arm" + nm + "2", wr, merge(palm, *fingers), GLOVE)
    return r


# ---------------------------------------------------------------- export

def export(rigs, path):
    def enc(a):
        return ",".join(str(int(round(x))) for x in a)

    out = [
        "// <auto-generated>",
        "// Baked by tools/creatures.py. Edit the generator and re-run it rather than editing this file.",
        "// Vertex positions are in units of 0.1 mm, relative to each part's pivot.",
        "// </auto-generated>",
        "using System.Collections.Generic;",
        "using UnityEngine;",
        "",
        "namespace XIII",
        "{",
        "    public static class CreatureData",
        "    {",
        "        public struct Part",
        "        {",
        "            public string Name, Mesh;",
        "            public int Parent, Mat;",
        "            public Vector3 Pivot; // relative to the parent's pivot",
        "            public Part(string name, int parent, float x, float y, float z, string mesh, int mat)",
        "            {",
        "                Name = name; Parent = parent; Pivot = new Vector3(x, y, z); Mesh = mesh; Mat = mat;",
        "            }",
        "        }",
        "",
    ]
    meshes = []
    for rig in rigs:
        out.append("        public static readonly Part[] %s =" % rig.name)
        out.append("        {")
        for name, parent, pivot, key, mesh, mat in rig.parts:
            pi = -1 if parent == -1 else rig.index(parent)
            ppos = np.zeros(3) if pi == -1 else rig.parts[pi][2]
            lp = pivot - ppos
            out.append('            new Part("%s", %d, %.4ff, %.4ff, %.4ff, %s, %d),' % (
                name, pi, lp[0], lp[1], lp[2], ('"%s"' % key) if key else "null", mat))
            if mesh is not None:
                meshes.append((key, (mesh.v - pivot) * 10000.0, mesh.t))
        out.append("        };")
        out.append("")
    out.append("        public static readonly Dictionary<string, int[][]> Meshes = new Dictionary<string, int[][]>")
    out.append("        {")
    for key, v, t in meshes:
        out.append('            { "%s", new[] {' % key)
        out.append("                new[] { %s }," % enc(v.reshape(-1)))
        out.append("                new[] { %s } } }," % enc(t.reshape(-1)))
    out.append("        };")
    out.append("    }")
    out.append("}")
    with open(path, "w") as f:
        f.write("\n".join(out) + "\n")
    nv = sum(len(m[1]) for m in meshes)
    nt = sum(len(m[2]) for m in meshes)
    print("wrote %s: %d meshes, %d verts, %d tris, %d KB" % (path, len(meshes), nv, nt, os.path.getsize(path) // 1024))


# ---------------------------------------------------------------- preview

MAT_COLORS = {
    "Cat": [(0.09, 0.09, 0.1), (0.85, 1.0, 0.3), (0.35, 0.18, 0.18), (0.0, 0.0, 0.0)],
    "Guest": [(0.22, 0.05, 0.06), (0.86, 0.83, 0.77), (0.9, 0.88, 0.82), (0.8, 0.62, 0.28), (0.0, 0.0, 0.0), (1, 1, 1)],
}
UNLIT = {"Cat": {1, 3}, "Guest": {4, 5}}


def world_tris(rig, pose=None):
    """pose: {part: (rx, ry, rz)} degrees, applied at the part's pivot to it and its children."""
    pose = pose or {}
    tris, mats = [], []
    for i, (name, parent, pivot, key, mesh, mat) in enumerate(rig.parts):
        if mesh is None:
            continue
        m = mesh
        chain, j = [], i
        while j != -1:
            chain.append(j)
            p = rig.parts[j][1]
            j = -1 if p == -1 else rig.index(p)
        for j in chain:  # innermost joint first
            nmj, _, pv, _, _, _ = rig.parts[j]
            if nmj in pose:
                R = pose[nmj]
                if isinstance(R, np.ndarray):  # a rotation matrix, for previewing solved poses
                    m = Mesh((m.v - pv) @ R.T + pv, m.t)
                else:
                    m = rot(m, *R, about=pv)
        tris.append(m.v[m.t])
        mats += [mat] * len(m.t)
    return np.vstack(tris), np.asarray(mats)


def render(rig, cam, target, path, pose=None, size=(520, 620), fov=30):
    from PIL import Image
    T, mats = world_tris(rig, pose)
    W, H = size
    cam, target = np.asarray(cam, float), np.asarray(target, float)
    f = norm(target - cam)
    rr = norm(np.cross((0, 1, 0), f))
    u = np.cross(f, rr)
    focal = (H / 2) / math.tan(math.radians(fov / 2))
    rel = T - cam
    pc = np.stack([rel @ rr, rel @ u, rel @ f], -1)
    sx = W / 2 + pc[..., 0] / pc[..., 2] * focal
    sy = H / 2 - pc[..., 1] / pc[..., 2] * focal
    n = np.cross(T[:, 1] - T[:, 0], T[:, 2] - T[:, 0])
    n = norm(n)
    facing = np.einsum("ij,ij->i", n, cam - T[:, 0]) > 0
    light = norm(np.array([-0.4, 0.9, 0.6]))
    shade = 0.3 + 0.7 * np.clip(n @ light, 0, 1)
    img = np.zeros((H, W, 3)) + np.array([0.75, 0.72, 0.66])
    zb = np.full((H, W), np.inf)
    cols = MAT_COLORS[rig.name]
    for k in range(len(T)):
        if pc[k, :, 2].min() < 0.05:  # behind the near plane
            continue
        x0, x1 = int(max(0, np.floor(sx[k].min()))), int(min(W - 1, np.ceil(sx[k].max())))
        y0, y1 = int(max(0, np.floor(sy[k].min()))), int(min(H - 1, np.ceil(sy[k].max())))
        if x1 < x0 or y1 < y0:
            continue
        X, Y = np.meshgrid(np.arange(x0, x1 + 1) + 0.5, np.arange(y0, y1 + 1) + 0.5)
        (ax, bx, cx), (ay, by, cy) = sx[k], sy[k]
        den = (by - cy) * (ax - cx) + (cx - bx) * (ay - cy)
        if abs(den) < 1e-9:
            continue
        w0 = ((by - cy) * (X - cx) + (cx - bx) * (Y - cy)) / den
        w1 = ((cy - ay) * (X - cx) + (ax - cx) * (Y - cy)) / den
        w2 = 1 - w0 - w1
        inside = (w0 >= 0) & (w1 >= 0) & (w2 >= 0)
        if not inside.any():
            continue
        z = w0 * pc[k, 0, 2] + w1 * pc[k, 1, 2] + w2 * pc[k, 2, 2]
        sub = zb[y0:y1 + 1, x0:x1 + 1]
        m = inside & (z < sub)
        sub[m] = z[m]
        if not facing[k]:
            col = np.array([1.0, 0.0, 1.0])  # inside-out triangle: a winding bug
        elif mats[k] in UNLIT[rig.name]:
            col = np.array(cols[mats[k]])
        else:
            col = np.array(cols[mats[k]]) * shade[k]
        img[y0:y1 + 1, x0:x1 + 1][m] = col
    Image.fromarray((np.clip(img, 0, 1) * 255).astype(np.uint8)).save(path)


def montage(paths, out):
    from PIL import Image
    ims = [Image.open(p) for p in paths]
    w = sum(i.width for i in ims)
    h = max(i.height for i in ims)
    m = Image.new("RGB", (w, h))
    x = 0
    for i in ims:
        m.paste(i, (x, 0))
        x += i.width
    m.save(out)


if __name__ == "__main__":
    cat, guest = build_cat(), build_guest()
    export([cat, guest], OUT_CS)
    if len(sys.argv) > 1:
        d = sys.argv[1]
        os.makedirs(d, exist_ok=True)
        walk = {"FrontL": (-25, 0, 0), "FrontL2": (40, 0, 0), "FrontR": (20, 0, 0), "HindL": (22, 0, 0), "HindR": (-20, 0, 0), "HindR2": (35, 0, 0),
                "Tail": (0, 20, 0), "Tail2": (0, 30, 0), "Head": (0, -35, 0)}
        render(cat, (1.1, 0.42, 0.2), (0, 0.25, 0.02), d + "/cat_side.png", size=(520, 360))
        render(cat, (0.55, 0.5, 0.95), (0, 0.27, 0.05), d + "/cat_front.png", size=(400, 360))
        render(cat, (1.1, 0.42, 0.2), (0, 0.25, 0.02), d + "/cat_walk.png", pose=walk, size=(520, 360))
        montage([d + "/cat_side.png", d + "/cat_front.png", d + "/cat_walk.png"], d + "/cat.png")
        render(guest, (0.0, 1.3, 5.2), (0, 1.15, 0), d + "/guest_front.png", size=(380, 620))
        render(guest, (3.0, 1.4, 3.8), (0, 1.15, 0), d + "/guest_34.png", size=(380, 620), pose={"Head": (0, 0, 18)})
        render(guest, (0.25, 2.1, 0.75), (0, 2.08, 0.06), d + "/guest_face.png", size=(380, 620), fov=40)
        montage([d + "/guest_front.png", d + "/guest_34.png", d + "/guest_face.png"], d + "/guest.png")
        print("previews in", d)
