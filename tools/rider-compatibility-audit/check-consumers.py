"""Verify the complete legacy skill/style source dependency audit against main."""
import hashlib
import json
from pathlib import Path
import re
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[2]
BASE = "ffa7196027f031f947c844da04f5706cb2655f41"
MANIFEST = ROOT / "docs/gameplay/rider-legacy-consumers.json"
REFERENCES = re.compile(r"\bRiderSkills\b|\bRiderStyle\b|\.Skills\b|\.Style\b")
# Domain declarations and the one-way adapter are deliberately outside consumers.
DOMAIN = {"src/CoreSim/Rider.cs", "src/CoreSim/RiderProfile.Gameplay.cs", "src/CoreSim/RiderAbilityCompatibility.cs"}
CANONICAL_DOMAIN = DOMAIN | {
    "src/CoreSim/RiderAbilities.cs", "src/CoreSim/RiderGameplayProfile.cs",
    "src/CoreSim/RiderState.cs", "src/CoreSim/SimulationSnapshot.cs",
}
CANONICAL = re.compile(r"\bRiderAbilities\b|\bRiderGameplayProfile\b|\bRiderPhysicalProfile\b|"
                       r"\bRiderInteractionStyle\b|\.Gameplay\b|\.Condition\b")
EXTRACTION = ROOT / "tests/fixtures/contested-source-extraction.json"
CONSEQUENCE_EXTRACTION = ROOT / "tests/fixtures/physical-consequence-source-extraction.json"
CONSEQUENCE_EXTRACTION_SHA256 = "10a5a5892ef2fbfb34944d03dc42468ca5b60645888d87b9ece18cffcc624ace"
EXTRACTION_SHA256 = "46a8f58c1cbd84dcb2697247e294b60474f7ee67712a49aa15f4f1337acd5d89"
CANONICAL_TRAFFIC = {"src/CoreSim/Interactions/InteractionModel.cs",
                     "src/CoreSim/Interactions/ContestedSpaceInteractionCoordinator.cs"}
CANONICAL_CONTACT_SHADOW = {
    "src/CoreSim/Interactions/PhysicalContactModel.cs",
    "src/CoreSim/Interactions/PhysicalContactAnalyzer.cs",
    "src/CoreSim/Interactions/PhysicalContactGeometry.cs",
    "src/CoreSim/Interactions/PhysicalContactSnapshotAdapter.cs",
    "src/CoreSim/Analysis/PhysicalContactEvidence.cs",
}


def reviewed_extraction():
    raw = EXTRACTION.read_bytes().replace(b"\r\n", b"\n")
    assert hashlib.sha256(raw).hexdigest() == EXTRACTION_SHA256
    obj = json.loads(raw)
    assert obj["BaseMainSha"] == "a1609e485131f553619c386a051a158796c9e2dd"
    return obj


def consumers():
    result = {}
    extraction = reviewed_extraction()
    consequence_raw = CONSEQUENCE_EXTRACTION.read_bytes().replace(b"\r\n", b"\n")
    assert hashlib.sha256(consequence_raw).hexdigest() == CONSEQUENCE_EXTRACTION_SHA256
    consequences = json.loads(consequence_raw)
    assert consequences["BaseMainSha"] == "5989301192565f6a265d53a2db12c7d00c94fedc"
    for path in sorted((ROOT / "src").rglob("*.cs")):
        if {"obj", "bin"} & set(path.parts):
            continue
        name = path.relative_to(ROOT).as_posix()
        source = path.read_text(encoding="utf-8").replace("\r\n", "\n")
        if name in consequences["NewDiagnosticSources"]:
            assert not REFERENCES.search(source), f"Legacy execution data in #56C2 module: {name}"
            assert hashlib.sha256(source.encode()).hexdigest() == consequences["NewDiagnosticSources"][name]
            continue
        for patch in consequences["Patches"].get(name, []):
            assert source.count(patch["After"]) == 1, name
            source = source.replace(patch["After"], patch["Before"])
        if name in CANONICAL_CONTACT_SHADOW:
            assert not REFERENCES.search(source), f"Legacy execution data in #56C1 shadow module: {name}"
            continue
        if name in extraction["NewDiagnosticSources"]:
            assert hashlib.sha256(source.encode()).hexdigest() == extraction["NewDiagnosticSources"][name]
            continue
        # Restore only exact reviewed #56B gate/control plumbing. The original
        # 39-file manifest, old arithmetic and every legacy read stay frozen.
        for patch in extraction["Patches"].get(name, []):
            assert source.count(patch["After"]) == 1, name
            source = source.replace(patch["After"], patch["Before"])
        # Only permitted production edit: carry inert Condition into detached snapshots.
        if name == "src/CoreSim/SimulationEngine.cs":
            source = source.replace(", Condition = rider.Condition", "")
        if name not in CANONICAL_DOMAIN | CANONICAL_TRAFFIC and not REFERENCES.search(source):
            # Existing WeatherState.Condition is unrelated to RiderState.Condition.
            canonical_source = source.replace("weather.Condition", "weather.WeatherCondition")
            assert not CANONICAL.search(canonical_source), f"Unexpected canonical production consumer: {name}"
        if name in DOMAIN or not REFERENCES.search(source):
            continue
        result[name] = hashlib.sha256(source.encode("utf-8")).hexdigest()
    return result


def verify():
    expected = json.loads(MANIFEST.read_text(encoding="utf-8"))
    assert expected["BaseMainSha"] == BASE
    actual = consumers()
    assert actual == expected["ConsumerSourceSha256"], {
        "added": sorted(actual.keys() - expected["ConsumerSourceSha256"].keys()),
        "removed": sorted(expected["ConsumerSourceSha256"].keys() - actual.keys()),
        "changed": [k for k in actual.keys() & expected["ConsumerSourceSha256"].keys()
                    if actual[k] != expected["ConsumerSourceSha256"][k]],
    }
    return len(actual)


if __name__ == "__main__":
    if sys.argv[1:] == ["--record-baseline"]:
        assert subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=ROOT, text=True).strip() == BASE
        # Also require every source file to match the actual Git tree at this base.
        assert not subprocess.check_output(["git", "diff", "HEAD", "--", "src"], cwd=ROOT)
        MANIFEST.write_text(json.dumps({"BaseMainSha": BASE, "ConsumerSourceSha256": consumers()},
                                     indent=2) + "\n", encoding="utf-8", newline="\n")
    else:
        print(f"Legacy consumer audit: {verify()} frozen readers; exact #56B and #56C2 extraction verified; canonical readers explicitly scoped.")
