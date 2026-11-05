"""Read the sealed #170 native evidence and audit six geometric sections per run."""

import argparse
from io import BytesIO
import json
import math
from pathlib import Path
from statistics import median
import sys
from zipfile import ZipFile

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "fast-two-corners"))
from analyze167 import telemetry

ROOT = Path("docs/experiments/two-corner-full-drive")
ARCHIVE = ROOT / "170-dowody.zip"


def load_entry(archive, name):
    with ZipFile(BytesIO(archive.read(name))) as run:
        return json.loads(run.read("native.json"))


def trace_of(run):
    samples = run["Riders"][0]["Samples"]
    trace = telemetry(samples)
    for q, sample in zip(trace, samples):
        body = sample["Input"]["Rider"]
        force = sample["Forces"]
        q.update(
            distance=body["DistanceMeters"],
            chi=math.atan2(body["Velocity"]["Y"], body["Velocity"]["X"]),
            body_heading=body["Heading"],
            rear_fx=force["RearLongitudinal"] if force else 0,
            rear_fy=force["RearLateral"] if force else 0,
            front_fx=0,
            front_fy=force["FrontLateral"] if force else 0,
        )
    for i, q in enumerate(trace):
        lo, hi = trace[max(0, i - 4)], trace[min(len(trace) - 1, i + 4)]
        distance = hi["distance"] - lo["distance"]
        turn = (hi["chi"] - lo["chi"] + math.pi) % (2 * math.pi) - math.pi
        q["curvature"] = turn / distance if distance > .05 else 0
    return trace


def at(a, b, s, field):
    fraction = (s - a["s"]) / (b["s"] - a["s"])
    return a[field] + fraction * (b[field] - a[field])


def section(trace, segment):
    lo = segment["StartReferenceMeters"]
    hi = lo + segment["LengthReferenceMeters"]
    intervals = [(a, b, max(lo, a["s"]), min(hi, b["s"])) for a, b in zip(trace, trace[1:])
                 if a["s"] < hi and b["s"] > lo and b["s"] > a["s"]]
    if not intervals or trace[-1]["s"] < hi:
        return None
    a0, b0, _, _ = intervals[0]
    a1, b1, _, _ = intervals[-1]
    t0, t1 = at(a0, b0, lo, "t"), at(a1, b1, hi, "t")
    dt = t1 - t0
    points = [q for q in trace if lo <= q["s"] <= hi]
    fields = ("beta", "beta_dot", "yaw", "side", "requested", "active", "slip", "front", "rear",
              "front_fx", "front_fy", "rear_fx", "rear_fy", "curvature", "offset")
    means, energy = {}, {}
    step_fields = {"requested", "active", "front", "rear", "front_fx", "front_fy", "rear_fx", "rear_fy"}
    for name in fields:
        means[name] = sum((b[name] if name in step_fields else at(a, b, (x + y) / 2, name)) *
                          (at(a, b, y, "t") - at(a, b, x, "t"))
                          for a, b, x, y in intervals) / dt
    for a, b, x, y in intervals:
        for name, joules in b["losses"].items():
            energy[name] = energy.get(name, 0) + joules * (y - x) / (b["s"] - a["s"])
    peaks = {k: max(abs(q[k]) for q in points) for k in fields}
    entry = at(a0, b0, lo, "kph")
    exit = at(a1, b1, hi, "kph")
    distance = at(a1, b1, hi, "distance") - at(a0, b0, lo, "distance")
    return dict(segment=segment["Index"], lo=lo, hi=hi, seconds=dt, distance=distance, entry=entry,
                exit=exit, min_speed=min(entry, exit, *(q["kph"] for q in points)),
                mean_speed=distance / dt * 3.6, means=means, peaks=peaks,
                min_radius=1 / peaks["curvature"] if peaks["curvature"] else None,
                energy=energy, inner=min(q["inner"] for q in points),
                outer=min(q["outer"] for q in points), swept=min(q["swept"] for q in points))


def section_table(rows, segments):
    lines = [
        "| Kontroler | Sekcja | Droga [m] | Czas [s] | V wej. | V min | V wyj. | V śr. | Śr. β [°] | Max yaw [rad/s] | Gaz aktywny | Strata netto [kJ] |",
        "|---|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|",
    ]
    for arm in ("baseline", "reserve-speed"):
        label = "#166" if arm == "baseline" else "#170"
        for i, segment in enumerate(segments):
            values = [r["sections"][i] for r in rows if r["controller"] == arm]
            med = lambda accessor: median(accessor(v) for v in values)
            common = [med(lambda q: q[k]) for k in ("distance", "seconds", "entry", "min_speed", "exit", "mean_speed")]
            tail = [med(lambda q: q["means"]["beta"]), med(lambda q: q["peaks"]["yaw"]),
                    med(lambda q: q["means"]["active"]) * 100, med(lambda q: q["energy"]["net"]) / 1000]
            bend, phase = divmod(i, 3)
            lines.append(f"| {label} | Łuk {bend + 1}, sekcja {phase + 1} | " +
                         " | ".join(f"{x:.2f}" for x in common + tail[:2]) +
                         f" | {tail[2]:.1f}% | {tail[3]:.2f} |")
    return "\n".join(lines) + "\n"


def audit(out):
    out.mkdir(parents=True, exist_ok=True)
    rows, traces = [], {}
    with ZipFile(ARCHIVE) as archive:
        geometry = json.loads(archive.read("geometry.json"))
        turns = [{k: v for k, v in p.items() if k != "Sections"}
                 for p in geometry["Segments"] if p["Type"] != "Straight"]
        if len(turns) != 6:
            raise ValueError("Six geometric turn segments required")
        for arm in ("baseline", "reserve-speed"):
            for i in range(1, 9):
                start = f"V{i}"
                run = load_entry(archive, f"validate-{arm}-{start}.zip")
                trace = trace_of(run)
                traces[(arm, start)] = trace
                rows.append(dict(controller=arm, start=start, fingerprint=run["Fingerprint"],
                                 sections=[section(trace, b) for b in turns]))
    (out / "audit.json").write_text(json.dumps(dict(segments=turns, rows=rows), separators=(",", ":")) + "\n", encoding="utf-8")
    (out / "SEKCJE.md").write_text(section_table(rows, turns), encoding="utf-8")
    return rows, geometry, turns, traces


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("output", type=Path)
    audit(parser.parse_args().output)
