"""Exact #56B presentation bytes, raw production IEEE values, and storm audit."""
import json
from pathlib import Path
import sys

root = Path(sys.argv[1])
windows = root / "determinism-windows-latest/contested-space"
ubuntu = root / "determinism-ubuntu-latest/contested-space"
names = {"contested-space-racing-response.json", "production-motion-bits.json"}
assert {p.name for p in windows.iterdir()} == {p.name for p in ubuntu.iterdir()} == names
for name in sorted(names):
    assert (windows / name).read_bytes() == (ubuntu / name).read_bytes(), name
report = json.loads((windows / "contested-space-racing-response.json").read_text())
golden = Path(__file__).resolve().parents[2] / "docs/calibration/contested-space-racing-response.json"
assert (windows / "contested-space-racing-response.json").read_bytes() == golden.read_bytes().replace(b"\r\n", b"\n"), "Recorded #56B report is stale"
assert len(report["Scenarios"]) == 13
assert len(report["Sensitivity"]) == len(report["TechniqueCondition"]) == 9
assert len(report["Heats"]) == 44 and len(report["Archetypes"]) == 32
for heat in report["Heats"] + report["Archetypes"]:
    assert heat["MaximumFallbacksPerEpisode"] <= 1, heat
    assert not heat["StormWarnings"], heat
for scenario in report["Scenarios"]:
    for episode in scenario["Interaction"]["Episodes"]:
        assert 2 <= len(episode["RiderIds"]) <= 4
        assert len(episode["Geometry"]) <= 6
        assert len(episode["Candidates"]) <= 81
        assert episode["PassCount"] <= 2
print("#56B report bytes and complete production IEEE captures match Windows/Ubuntu; storm audit clear.")
