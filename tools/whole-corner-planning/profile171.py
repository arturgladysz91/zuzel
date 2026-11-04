"""Create distinct smooth reference inputs for both bends from four archived #170 starts."""

from bisect import bisect_left
from io import BytesIO
import json
import math
from pathlib import Path
from statistics import median
import sys
from zipfile import ZipFile

from audit171 import ARCHIVE, load_entry


def sample(samples, references, position):
    j = bisect_left(references, position)
    a, b = samples[j - 1], samples[j]
    weight = (position - references[j - 1]) / (references[j] - references[j - 1])
    def data(q):
        state = q["Input"]
        bike = state["Rider"]
        return (bike["Road"]["OffsetMeters"], bike["Speed"], bike["SlideRadians"],
                bike["AngularVelocity"], state["Active"]["Drive"], q["CrankOmega"], state["Omega"])
    return [x + (y - x) * weight for x, y in zip(data(a), data(b))]


if __name__ == "__main__":
    with ZipFile(ARCHIVE) as archive:
        geom = json.loads(archive.read("geometry.json"))
        starts = [geom["Segments"][i]["StartReferenceMeters"] for i in (1, 5)]
        source = ["V1", "V3", "V5", "V7"]
        runs = [load_entry(archive, f"validate-reserve-speed-{key}.zip") for key in source]
    histories = [run["Riders"][0]["Samples"] for run in runs]
    refs = [[q["Input"]["Rider"]["TotalReferenceMeters"] for q in series] for series in histories]
    profiles = []
    for entry in starts:
        rows = []
        for distance in range(-24, 123, 2):
            values = [sample(series, positions, entry + distance) for series, positions in zip(histories, refs)]
            rows.append(dict(zip(("Offset", "Speed", "Beta", "Yaw", "Drive", "Crank", "Wheel"),
                                 (median(column) for column in zip(*values)))))
        profiles.append(rows)
    Path(sys.argv[1]).write_text(json.dumps(profiles, indent=2) + "\n", encoding="utf-8")
