"""Render Study 208 figures directly from the recorded trajectory substeps."""

import gzip
import json
import math
import sys
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont


DARK = "#192837"
MUTED = "#506177"
GRID = "#dbe3eb"
TEAL = "#067e73"
ORANGE = "#b95026"
BLUE = "#385d83"
BG = "#f7fafc"


def font(size, weight="regular"):
    style = {"regular": "DejaVuSans.ttf", "bold": "DejaVuSans-Bold.ttf"}
    try:
        return ImageFont.truetype(style[weight], size)
    except OSError:
        return ImageFont.load_default(size)


def load(path):
    with gzip.open(path, "rt", encoding="utf-8") as handle:
        return json.load(handle)


def rider(row):
    return row["Observed"]["Start"]["State"]["Rider"]


def sample_s(row):
    return rider(row)["TotalReferenceMeters"]


def nearest(rows, station):
    return min(rows, key=lambda point: abs(sample_s(point) - station))


def line(draw, samples, output, color, width):
    draw.line([output(row) for row in samples], fill=color, width=width, joint="curve")


def picture(title, subtitle):
    im = Image.new("RGB", (1500, 1000), BG)
    draw = ImageDraw.Draw(im)
    draw.text((64, 40), title, fill=DARK, font=font(42, "bold"))
    draw.text((65, 102), subtitle, fill=MUTED, font=font(24))
    return im, draw


def legend(draw, y):
    for x, color, label in [
        (72, ORANGE, "#207 frozen 12152"),
        (402, TEAL, "#208 locked nominal"),
        (780, BLUE, "Geometric reference"),
    ]:
        draw.line(((x, y + 12), (x + 52, y + 12)), fill=color, width=5)
        draw.text((x + 67, y), label, fill=DARK, font=font(20))


def xy(row, reference=False):
    body = rider(row)
    p = body["Position"]
    if not reference:
        return p["X"], p["Y"]
    normal = body["Road"]["Outward"]
    shift = body["Road"]["OffsetMeters"]
    return p["X"] - shift * normal["X"], p["Y"] - shift * normal["Y"]


def geometry(old, new, bend, output):
    s0, s1 = bend["Entry"] - 42, bend["Exit"] + 17
    old = [row for row in old if s0 <= sample_s(row) <= s1]
    new = [row for row in new if s0 <= sample_s(row) <= s1]
    im, draw = picture("Fourth bend trajectory", "Equal axes in metres. Arrows show measured velocity around the exit.")
    legend(draw, 150)
    pts = [xy(row) for row in old + new] + [xy(row, True) for row in new]
    xmin, xmax = min(p[0] for p in pts) - 7, max(p[0] for p in pts) + 11
    ymin, ymax = min(p[1] for p in pts) - 7, max(p[1] for p in pts) + 9
    plot = (120, 218, 1385, 881)
    draw.rounded_rectangle(plot, radius=15, fill="white", outline=GRID, width=2)
    scale = min((plot[2] - plot[0]) / (xmax - xmin), (plot[3] - plot[1]) / (ymax - ymin))
    ox = (plot[0] + plot[2] - scale * (xmin + xmax)) * .5
    oy = (plot[1] + plot[3] + scale * (ymin + ymax)) * .5

    def project(point):
        return ox + point[0] * scale, oy - point[1] * scale

    for value in range(math.ceil(xmin / 10) * 10, math.floor(xmax / 10) * 10 + 1, 10):
        xp, _ = project((value, 0))
        draw.line((xp, plot[1] + 12, xp, plot[3] - 12), fill=GRID, width=1)
        draw.text((xp, plot[3] + 10), str(value), anchor="mt", font=font(17), fill=MUTED)
    for value in range(math.ceil(ymin / 10) * 10, math.floor(ymax / 10) * 10 + 1, 10):
        _, yp = project((0, value))
        draw.line((plot[0] + 12, yp, plot[2] - 12, yp), fill=GRID, width=1)
        draw.text((plot[0] - 10, yp), str(value), anchor="rm", font=font(17), fill=MUTED)
    line(draw, new, lambda row: project(xy(row, True)), BLUE, 3)
    line(draw, old, lambda row: project(xy(row)), ORANGE, 5)
    line(draw, new, lambda row: project(xy(row)), TEAL, 6)
    for tag, s, offset in [
        ("Entry", bend["Entry"], (-45, -32)),
        ("Apex", (bend["Entry"] + bend["Exit"]) / 2, (-69, 20)),
        ("Exit", bend["Exit"], (-8, 26)),
        ("+12 m", bend["Exit"] + 12, (14, -30)),
    ]:
        p = project(xy(nearest(new, s)))
        draw.ellipse((p[0] - 6, p[1] - 6, p[0] + 6, p[1] + 6), fill="white", outline=TEAL, width=3)
        draw.text((p[0] + offset[0], p[1] + offset[1]), tag, fill=TEAL, font=font(19, "bold"))
    for rows, color in [(old, ORANGE), (new, TEAL)]:
        p = nearest(rows, bend["Exit"] + 4)
        pos = xy(p)
        v = rider(p)["Velocity"]
        a = project(pos)
        tip = project((pos[0] + .37 * v["X"], pos[1] + .37 * v["Y"]))
        phi = math.atan2(tip[1] - a[1], tip[0] - a[0])
        wing = [tuple(tip[i] - 13 * component for i, component in enumerate((math.cos(theta), math.sin(theta))))
                for theta in (phi - .45, phi + .45)]
        draw.line((a, tip), fill=color, width=4)
        draw.polygon([tip, *wing], fill=color)
    draw.text((72, 942), "Reference is the geometry's zero-offset line. Tracks end at the last plotted substep.",
              fill=MUTED, font=font(19))
    im.save(output)


def momentum(old, new, bend, output):
    s0, s1 = bend["Exit"] - 49, bend["Exit"] + 22
    old = [row for row in old if s0 <= sample_s(row) <= s1]
    new = [row for row in new if s0 <= sample_s(row) <= s1]
    im, draw = picture("Outward momentum and exit viability", "The velocity basis is fixed to the next straight across the bend and exit.")
    legend(draw, 150)
    left, right = 128, 1418
    def sx(s):
        return left + (s - s0) * (right - left) / (s1 - s0)
    panels = [
        ("Outward momentum [kg·m/s]", lambda r: r["Velocity"]["Momentum"], [-400, 0, 800, 1600, 2400, 3200], 0),
        ("Yaw rate [rad/s]", lambda r: r["Velocity"]["Yaw"], [-.25, 0, .25, .5, .75, 1], 0),
        ("Physical swept clearance [m]", lambda r: r["Observed"]["Swept"], [.5, 2, 4, 6, 8], .5),
    ]
    for i, (label, fn, ticks, limit) in enumerate(panels):
        top, bottom = 253 + 222 * i, 419 + 222 * i
        draw.text((left, top - 32), label, fill=DARK, font=font(21, "bold"))
        samples = [fn(row) for row in old + new if fn(row) is not None]
        lo, hi = min(ticks[0], min(samples)), max(ticks[-1], max(samples))
        def sy(value):
            return bottom - (value - lo) * (bottom - top) / (hi - lo)
        draw.rectangle((left, top, right, bottom), fill="white", outline=GRID, width=2)
        for tick in ticks:
            h = sy(tick)
            draw.line((left, h, right, h), fill=GRID if tick != limit else BLUE, width=1 if tick != limit else 2)
            draw.text((left - 12, h), f"{tick:g}", anchor="rm", fill=MUTED, font=font(17))
        for s, marker in [(bend["Exit"], "Exit"), (bend["Exit"] + 12, "+12 m")]:
            x = sx(s)
            draw.line((x, top, x, bottom), fill=BLUE, width=2)
            if i == 0:
                draw.text((x + 8, top + 4), marker, fill=BLUE, font=font(18))
        for rows, color in [(old, ORANGE), (new, TEAL)]:
            clean = [row for row in rows if fn(row) is not None]
            line(draw, clean, lambda row: (sx(sample_s(row)), sy(fn(row))), color, 5)
    for s in range(math.ceil(s0 / 10) * 10, math.floor(s1 / 10) * 10 + 1, 10):
        draw.text((sx(s), 889), str(s), anchor="mt", fill=MUTED, font=font(18))
    draw.text((750, 940), "Reference distance S [m]", anchor="mt", fill=DARK, font=font(22, "bold"))
    im.save(output)


def main():
    root = Path(sys.argv[1]) if len(sys.argv) > 1 else Path("data/experiments/speedway208")
    baseline = load(root / "fourth-exit-audit.json.gz")
    old = next(row["Trace"] for row in baseline["Audit"] if row["Fixture"] == "C")
    new = load(root / "official" / "C.json.gz")["Trace"]
    out = root / "figures"
    out.mkdir(exist_ok=True)
    geometry(old, new, baseline["Bend"], out / "fourth-bend-geometry.png")
    momentum(old, new, baseline["Bend"], out / "exit-momentum-and-swept.png")


if __name__ == "__main__":
    main()
