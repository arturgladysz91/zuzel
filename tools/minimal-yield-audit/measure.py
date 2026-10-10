"""Compare fresh base-main and PR-head executed paths in physical metres."""
import json
from pathlib import Path
import sys


def compare(before_path, after_path, output):
    before, after = [json.loads(Path(p).read_text()) for p in (before_path, after_path)]
    key = lambda row: (row["Name"], row["Configuration"])
    left, right = [{key(r): r for r in rows} for rows in (before, after)]
    assert left.keys() == right.keys() and len(right) == 44
    certified = 0
    for row in after:
        assert row["RiderOrderExact"]
        if row["ClearanceCertified"]:
            certified += 1
            assert row["MechanicalContacts"] == 0, key(row)
        if row["Name"].startswith("attack-"):
            assert row["ClearanceCertified"], key(row)
    measurements = []
    for configuration in "BC":
        a, b = [rows[("D-inside-overlap", configuration)] for rows in (left, right)]
        displacement = lambda row: next(r["DisplacementMeters"] for r in row["Riders"] if r["RiderId"] == 2)
        old, new = displacement(a), displacement(b)
        assert .01 < new < .5 and new < .3 * old and b["ClearanceCertified"]
        measurements.append({"Configuration": configuration, "BeforeOutwardMeters": old, "AfterOutwardMeters": new,
            "ReductionPercent": 100 * (1 - new / old),
            "MinimumSignedFootprintSeparationMeters": b["MinimumMechanicalSeparationMeters"]})
        for name in ("K-far-apart", "edge-trapped", "G-four-first-bend"):
            assert left[(name, configuration)]["Riders"] == right[(name, configuration)]["Riders"], (name, configuration)
        assert right[("imminent-overlap", configuration)]["MechanicalContacts"] > 0
    report = {"BaseMainSha": "f40b2fd129124d969fd22cfe7767e2fb00bd651f", "Cases": len(after),
              "CertifiedExecutedPaths": certified, "CertifiedMechanicalOverlaps": 0,
              "RiderOrderExact": True, "EstablishedAttack": measurements}
    Path(output).write_text(json.dumps(report, indent=2) + "\n")
    return report


if __name__ == "__main__":
    print(json.dumps(compare(*sys.argv[1:]), indent=2))
