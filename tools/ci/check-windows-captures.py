"""Retain the Windows golden/shape/order checks from the retired OS comparison job."""
import json
from pathlib import Path
import runpy
import sys

root = Path(sys.argv[1])
repo = Path(__file__).resolve().parents[2]
goldens = repo / "docs/calibration"

# The physical calibration kernels remain frozen. Only target-driven production
# rows may differ from their historical goldens, and only with actual target work.
target = runpy.run_path(str(repo / "tools/minimal-yield-audit/compatibility.py"))
for directory, name, consequences in (("physical-contact", "physical-contact-analysis.json", False),
                                      ("physical-contact-consequences", "physical-contact-consequences.json", True)):
    summary = target["contact_presentation"](
        goldens / name, root / directory / name,
        root / directory / "minimal-yield-target-work.json",
        root / ("minimal-yield-" + directory + "-changes.json.gz"),
        consequences=consequences)
    print(name, summary)

physical = json.loads((root / "physical-space/physical-occupancy-evidence.json").read_text(encoding="utf-8"))
assert len(physical["Scenarios"]) == 12
assert physical["FourRiderStartAndFirstBend"]["Work"]["RiderPairs"] == 6
for name in ("FourRiderStartAndFirstBend", "FourRiderCompleteHeat"):
    assert physical[name]["Work"]["UnresolvedIntervals"] == 0
    assert physical[name]["IncompatibleFrameIntervals"] == 0
assert physical["MetricEmbedding"]["Closure"]["SupportsLapWrap"]
assert {p.name for p in (root / "rider-compatibility").iterdir()} == {
    "adaptive.json", "convergence.json", "rider-abilities-compatibility-evidence.json"}
assert {p.name for p in (root / "contested-space").iterdir()} == {
    "contested-space-racing-response.json", "production-motion-bits.json"}

# The base report and optimized report are compared row by row. Every
# unexpected untargeted change still fails the historical audit.
presentation = target["presentation"](
    root / "contested-space-before/contested-space-racing-response.json",
    root / "contested-space/contested-space-racing-response.json",
    root / "minimal-yield-presentation-changes.json.gz")
print("Minimal outward presentation:", presentation)
report = json.loads((root / "contested-space/contested-space-racing-response.json").read_text(encoding="utf-8"))
assert len(report["Scenarios"]) == 13
assert len(report["Sensitivity"]) == len(report["TechniqueCondition"]) == 9
assert len(report["Heats"]) == 44 and len(report["Archetypes"]) == 32
for heat in report["Heats"] + report["Archetypes"]:
    assert heat["MaximumFallbacksPerOrigin"] <= 1
    assert not heat["StormWarnings"]
for scenario in report["Scenarios"]:
    for episode in scenario["Interaction"]["Episodes"]:
        assert 2 <= len(episode["RiderIds"]) <= 4
        assert len(episode["Geometry"]) <= 6
        assert len(episode["Candidates"]) <= 81 * episode["PassCount"]
        assert episode["PassCount"] <= 2
ownership = {row["Name"]: row for row in report["IndependentOwnership"]}
assert len(ownership) == 7
for row in ownership.values():
    assert row["UnexpectedEpisodeMerges"] == 0
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
for name in ("real-bridge", "bridge-with-previously-attempted"):
    assert ownership[name]["EpisodeCount"] == 1
    assert ownership[name]["GenuinelyMergedEpisodes"] == 1
release = ownership["independent-episode-release"]
assert [e["RiderIds"] for e in release["ReleasedEpisodes"]] == [[1, 2]]
assert [e["RiderIds"] for e in release["ActiveEpisodes"]] == [[3, 4]]

evidence = json.loads((root / "physical-contact/physical-contact-analysis.json").read_text(encoding="utf-8"))
assert not evidence["Warnings"]
assert len(evidence["RawGrid"]) == 4374
assert set(row["Name"][0] for row in evidence["Controlled"]) >= set("ABCDEFGHIJKLMNOPQR")
decisions = runpy.run_path(str(repo / "tools/contact-decision-audit/compare.py"))
print(json.dumps(decisions["verify_order"](root / "contact-decision", "after"), indent=2))
decisions["write_changes"](root / "contact-decision-changes.json.gz",
    decisions["differences"](root / "contact-decision", "before", root / "contact-decision", "after"))
print("Windows goldens, geometry, storm bounds and complete enabled contact rider-order checks passed.")
