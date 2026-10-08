"""Exact #56B presentation bytes, raw production IEEE values, and storm audit."""
import json
from pathlib import Path
import sys
import runpy

root = Path(sys.argv[1])
windows = root / "determinism-windows-latest/contested-space"
ubuntu = root / "determinism-ubuntu-latest/contested-space"
names = {"contested-space-racing-response.json", "production-motion-bits.json"}
assert {p.name for p in windows.iterdir()} == {p.name for p in ubuntu.iterdir()} == names
for name in sorted(names):
    assert (windows / name).read_bytes() == (ubuntu / name).read_bytes(), name
report = json.loads((windows / "contested-space-racing-response.json").read_text())
golden = Path(__file__).resolve().parents[2] / "docs/calibration/contested-space-racing-response.json"
frozen = runpy.run_path(str(Path(__file__).resolve().parents[1] / "contested-performance/compare-frozen.py"))
changes = frozen["compare"](frozen["read"](golden), frozen["read"](windows / "contested-space-racing-response.json"))
(root / "contested-work-savings.json").write_text(json.dumps(changes,indent=2)+"\n")
assert len(report["Scenarios"]) == 13
assert len(report["Sensitivity"]) == len(report["TechniqueCondition"]) == 9
assert len(report["Heats"]) == 44 and len(report["Archetypes"]) == 32
for heat in report["Heats"] + report["Archetypes"]:
    assert heat["MaximumFallbacksPerOrigin"] <= 1, heat
    assert not heat["StormWarnings"], heat
for scenario in report["Scenarios"]:
    for episode in scenario["Interaction"]["Episodes"]:
        assert 2 <= len(episode["RiderIds"]) <= 4
        assert len(episode["Geometry"]) <= 6
        assert len(episode["Candidates"]) <= 81 * episode["PassCount"]
        assert episode["PassCount"] <= 2
ownership = {row["Name"]: row for row in report["IndependentOwnership"]}
assert len(ownership) == 7
for row in ownership.values():
    assert row["UnexpectedEpisodeMerges"] == 0, row["Name"]
    assert row["GlobalSafetySearches"] <= 1
    assert row.get("SafetyJointCombinations", 0) <= 81
disjoint = ownership["two-disjoint-unresolved"]
assert disjoint["EpisodeCount"] == 2
assert [e["RiderIds"] for e in disjoint["EpisodeMemberships"]] == [[1, 2], [3, 4]]
assert disjoint["ActualConnectedComponents"] == disjoint["FinalConnectedComponents"] == 2
assert disjoint["GlobalSafetySearches"] == 1
assert disjoint["Interaction"]["Work"]["LegacyFallbackAttempts"] == 2
cleared = ownership["one-disjoint-clears"]
assert cleared["ActualConnectedComponents"] == 2 and cleared["FinalConnectedComponents"] == 1
assert cleared["Interaction"]["Work"]["LegacyFallbackAttempts"] == 1
assert ownership["one-previously-attempted"]["Interaction"]["Work"]["LegacyFallbackAttempts"] == 1
for name in ["real-bridge", "bridge-with-previously-attempted"]:
    assert ownership[name]["EpisodeCount"] == 1
    assert ownership[name]["GenuinelyMergedEpisodes"] == 1
release = ownership["independent-episode-release"]
assert [e["RiderIds"] for e in release["ReleasedEpisodes"]] == [[1, 2]]
assert [e["RiderIds"] for e in release["ActiveEpisodes"]] == [[3, 4]]
print("#56B report bytes and complete production IEEE captures match Windows/Ubuntu; storm audit clear.")
