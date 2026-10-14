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
REA009_EXTRACTION = ROOT / "tests/fixtures/rea009-source-extraction.json"
REA009_EXTRACTION_SHA256 = "58876cb85d065bf716073061c2f2baadcab783cc98d453148f63cd8d30177f6f"
REA001_EXTRACTION = ROOT / "tests/fixtures/rea001-source-extraction.json"
REA001_EXTRACTION_SHA256 = "2c369eace9249fc226a2eb522799a6b473b281b1cc18ce3cadb05469f1c6048c"
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
        source = restore_practical_contact_time(source, name)
        source = restore_first_contact(source, name)
        source = restore_frozen_heat(source, name)
        source = restore_real_pair_admission(source, name)
        source = restore_causal_pair(source, name)
        source = restore_partial_step(source, name)
        source = restore_turnexit_cursor(source, name)
        source = restore_integrator_cursor(source, name)
        source = restore_minimal_yield(source, name)
        source = restore_segment_completion(source, name)
        source = restore_optional_safety_contract(source, name)
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


def restore_practical_contact_time(source, name):
    raw = (ROOT / "tests/fixtures/rea004a10-source-extraction.json").read_bytes().replace(b"\r\n", b"\n")
    assert hashlib.sha256(raw).hexdigest() == "69526eec8de2b20ecd13b5f6168b3295fc5ff3ddc7ffde2d966a38c32072a172"
    extraction = json.loads(raw)
    assert extraction["BaseSha"] == "b4b510f42c6d805ca286313fa441c6b3cba71829"
    assert set(extraction["Patches"]) == {"src/CoreSim/SimulationEngine.cs", "src/CoreSim/PhysicalSpace/ContestedSpace.cs",
        "src/CoreSim/PhysicalSpace/SpaceSearchDiagnostics.cs", "src/CoreSim/Track/RiderIntegrationCursor.cs",
        "src/CoreSim/Race/TwoRiderContactExecution.cs"}
    assert extraction["NewSourceSha256"] == {}
    for patch in extraction["Patches"].get(name, []):
        assert source.count(patch["After"]) == 1, name
        source = source.replace(patch["After"], patch["Before"])
    if name in extraction["BaseSourceSha256"]:
        assert hashlib.sha256(source.encode()).hexdigest() == extraction["BaseSourceSha256"][name], name
    return source


def restore_first_contact(source, name):
    raw = (ROOT / "tests/fixtures/rea004a9-source-extraction.json").read_bytes().replace(b"\r\n", b"\n")
    assert hashlib.sha256(raw).hexdigest() == "a045ba7f9257bb167467b81ef481f26493a6321009534997645e04cc2f50f25e"
    extraction = json.loads(raw)
    assert extraction["BaseSha"] == "ccb2bac78fc25972c8ed213cc0d28713a00fcf29"
    assert set(extraction["Patches"]) == {"src/CoreSim/PhysicalSpace/ContestedSpace.cs",
        "src/CoreSim/Track/ExecutedPathTraversal.cs", "src/CoreSim/Track/RiderIntegrationCursor.cs",
        "src/CoreSim/Race/TwoRiderContactExecution.cs"}
    for patch in extraction["Patches"].get(name, []):
        assert source.count(patch["After"]) == 1, name
        source = source.replace(patch["After"], patch["Before"])
    if name in extraction["BaseSourceSha256"]:
        assert hashlib.sha256(source.encode()).hexdigest() == extraction["BaseSourceSha256"][name], name
    if name in extraction["NewSourceSha256"]:
        assert not REFERENCES.search(source), name
        assert hashlib.sha256(source.encode()).hexdigest() == extraction["NewSourceSha256"][name], name
        return ""
    return source


def restore_frozen_heat(source, name):
    raw = (ROOT / "tests/fixtures/rea004a8-source-extraction.json").read_bytes().replace(b"\r\n", b"\n")
    assert hashlib.sha256(raw).hexdigest() == "2a72bf5b4767955c8db253fff569bee68554dcc3a462edc13aa8255cff27dc9d"
    extraction = json.loads(raw)
    assert extraction["BaseSha"] == "3fe36abb01bb0367a2d2f716b838863c09d5f81d"
    assert set(extraction["Patches"]) == {"src/CoreSim/SimulationEngine.cs", "src/CoreSim/Track/ExecutedPathTraversal.cs",
        "src/CoreSim/Track/RiderIntegrationCursor.cs", "src/CoreSim/Race/TwoRiderContactExecution.cs", "src/CoreSim/Race/CommonTimeMotionWindow.cs",
        "src/CoreSim/SimulationSnapshot.cs", "src/CoreSim/Decisions/TrajectoryEvaluator.cs"}
    for patch in extraction["Patches"].get(name, []):
        assert source.count(patch["After"]) == 1, name
        source = source.replace(patch["After"], patch["Before"])
    if name in extraction["BaseSourceSha256"]:
        assert hashlib.sha256(source.encode()).hexdigest() == extraction["BaseSourceSha256"][name], name
    if name in extraction["NewSourceSha256"]:
        assert not REFERENCES.search(source), name
        assert hashlib.sha256(source.encode()).hexdigest() == extraction["NewSourceSha256"][name], name
        return ""
    return source


def restore_real_pair_admission(source, name):
    # Restore only A7 admission/provenance plumbing before every unchanged older extraction.
    raw = (ROOT / "tests/fixtures/rea004a7-source-extraction.json").read_bytes().replace(b"\r\n", b"\n")
    assert hashlib.sha256(raw).hexdigest() == "b6a12d835382169c1cf55d561e11b5186d66c0329965f408b62cb734c701b624"
    extraction = json.loads(raw)
    assert extraction["BaseSha"] == "229685c2aeaf2c0d79ec826efa1640e8b5a82ac0"
    assert set(extraction["Patches"]) == {"src/CoreSim/SimulationEngine.cs", "src/CoreSim/Race/TwoRiderContactExecution.cs"}
    for patch in extraction["Patches"].get(name, []):
        assert source.count(patch["After"]) == 1, name
        source = source.replace(patch["After"], patch["Before"])
    if name in extraction["BaseSourceSha256"]:
        assert hashlib.sha256(source.encode()).hexdigest() == extraction["BaseSourceSha256"][name], name
    return source


def restore_causal_pair(source, name):
    raw = (ROOT / "tests/fixtures/rea004a6-source-extraction.json").read_bytes().replace(b"\r\n", b"\n")
    assert hashlib.sha256(raw).hexdigest() == "c23a67789fba7ab5de5c8a160ac801cfede6e7504766a95d5572fa86ef747841"
    extraction = json.loads(raw)
    assert extraction["BaseSha"] == "5aafc1dad2936242b64035ec4ddfe51cd9ea338e"
    assert set(extraction["Patches"]) == {"src/CoreSim/Track/ExecutedPathTraversal.cs",
        "src/CoreSim/Track/RiderIntegrationCursor.cs", "src/CoreSim/PhysicalSpace/BikePoseIntervals.cs",
        "src/CoreSim/SimulationEngine.cs", "src/CoreSim/Race/RiderPosition.cs"}
    for patch in extraction["Patches"].get(name, []):
        assert source.count(patch["After"]) == 1, name
        source = source.replace(patch["After"], patch["Before"])
    if name in extraction["BaseSourceSha256"]:
        assert hashlib.sha256(source.encode()).hexdigest() == extraction["BaseSourceSha256"][name], name
    if name in extraction["NewSourceSha256"]:
        assert not REFERENCES.search(source), name
        assert hashlib.sha256(source.encode()).hexdigest() == extraction["NewSourceSha256"][name], name
        # The pinned two-rider adapter explicitly supplies canonical #56 physiology.
        # It adds no legacy skill/style consumer; all older extraction rules remain exact.
        return ""
    return source


def restore_partial_step(source, name):
    # Undo only the pinned A5 execution/pose adapter plumbing. Every older
    # extraction, consumer manifest and accepted historical value stays frozen.
    raw = (ROOT / "tests/fixtures/rea004a5-source-extraction.json").read_bytes().replace(b"\r\n", b"\n")
    assert hashlib.sha256(raw).hexdigest() == "e2d59068fded9fdf6066e0b6a2486e925b4237e034ab74fa1520c5d672be2ed7"
    extraction = json.loads(raw)
    assert extraction["BaseSha"] == "ffe5be37c1402706252835d5a6145004290c2699"
    assert set(extraction["Patches"]) == {
        "src/CoreSim/Track/ExecutedPathTraversal.cs", "src/CoreSim/Track/RiderIntegrationCursor.cs",
        "src/CoreSim/PhysicalSpace/BikePoseIntervals.cs", "src/CoreSim/Race/CommonTimeMotionWindow.cs"}
    for patch in extraction["Patches"].get(name, []):
        assert source.count(patch["After"]) == 1, name
        source = source.replace(patch["After"], patch["Before"])
    if name in extraction["BaseSourceSha256"]:
        assert hashlib.sha256(source.encode()).hexdigest() == extraction["BaseSourceSha256"][name], name
    if name in extraction["NewSourceSha256"]:
        assert hashlib.sha256(source.encode()).hexdigest() == extraction["NewSourceSha256"][name], name
    return source


def restore_turnexit_cursor(source, name):
    # Preserve every historical extraction/hash. This exact A4 inverse includes
    # only cursor admission and retained completed-corner execution state.
    raw = (ROOT / "tests/fixtures/rea004a4-source-extraction.json").read_bytes().replace(b"\r\n", b"\n")
    assert hashlib.sha256(raw).hexdigest() == "965141de995e6066c32f836c0ef0798d772f309945b4656800f1c155b04608a7"
    extraction = json.loads(raw)
    assert extraction["BaseSha"] == "0d2653fc13c55ee83d5d2d92cf6537a60d634eb6"
    assert set(extraction["Patches"]) == {"src/CoreSim/SimulationEngine.cs", "src/CoreSim/Track/ExecutedPathTraversal.cs"}
    for patch in extraction["Patches"].get(name, []):
        assert source.count(patch["After"]) == 1, name
        source = source.replace(patch["After"], patch["Before"])
    if name in extraction["BaseSourceSha256"]:
        assert hashlib.sha256(source.encode()).hexdigest() == extraction["BaseSourceSha256"][name], name
    return source


def restore_integrator_cursor(source, name):
    # Reverse only this PR's exact execution plumbing before the unchanged older
    # extractions. Neither the historical manifests nor their hashes are updated.
    raw = (ROOT / "tests/fixtures/rea004a3-source-extraction.json").read_bytes().replace(b"\r\n", b"\n")
    assert hashlib.sha256(raw).hexdigest() == "c5305645fa883478812af48b088360d40073032d76e804d0a4a99e2507aaf971"
    extraction = json.loads(raw)
    assert extraction["BaseSha"] == "ba945c613502a03c28b80819af9e3b8afdc1ad83"
    assert set(extraction["Patches"]) == {"src/CoreSim/SimulationEngine.cs", "src/CoreSim/Track/ExecutedPathTraversal.cs"}
    for patch in extraction["Patches"].get(name, []):
        assert source.count(patch["After"]) == 1, name
        source = source.replace(patch["After"], patch["Before"])
    if name in extraction["BaseSourceSha256"]:
        assert hashlib.sha256(source.encode()).hexdigest() == extraction["BaseSourceSha256"][name], name
    return source


def restore_minimal_yield(source, name):
    raw = (ROOT / "tests/fixtures/minimal-yield-source-extraction.json").read_bytes().replace(b"\r\n", b"\n")
    assert hashlib.sha256(raw).hexdigest() == "4816c93c0a48c711fa507f903a4f60c2c154111a0f0bb5264b0acec823864ed1"
    extraction = json.loads(raw)
    assert extraction["BaseMainSha"] == "f40b2fd129124d969fd22cfe7767e2fb00bd651f"
    for patch in extraction["Patches"].get(name, []):
        assert source.count(patch["After"]) == 1, name
        source = source.replace(patch["After"], patch["Before"])
    return source


def restore_segment_completion(source, name):
    # Restore exactly the two semantic handoffs before older frozen extractions.
    # Physics expressions and legacy skill/style consumer hashes stay pinned.
    raw = REA009_EXTRACTION.read_bytes().replace(b"\r\n", b"\n")
    assert hashlib.sha256(raw).hexdigest() == REA009_EXTRACTION_SHA256
    extraction = json.loads(raw)
    assert extraction["BaseMainSha"] == "cd19c399dfc97d7abbd2aca03d50aa2c94a76262"
    assert set(extraction["Patches"]) == {"src/CoreSim/SimulationEngine.cs"}
    assert len(extraction["Patches"]["src/CoreSim/SimulationEngine.cs"]) == 2
    for patch in extraction["Patches"].get(name, []):
        assert source.count(patch["After"]) == 1, name
        source = source.replace(patch["After"], patch["Before"])
    return source


def restore_optional_safety_contract(source, name):
    # Restore only the exact REA-001 failure contract before applying older frozen
    # extractions. No arithmetic, legacy-reader manifest or allowlist is relaxed.
    raw = REA001_EXTRACTION.read_bytes().replace(b"\r\n", b"\n")
    assert hashlib.sha256(raw).hexdigest() == REA001_EXTRACTION_SHA256
    extraction = json.loads(raw)
    assert extraction["BaseMainSha"] == "350f59cc53184399066bdf93b18341e4628ec8ef"
    assert set(extraction["Patches"]) == {"src/CoreSim/Track/ExecutedPathTraversal.cs"}
    for patch in extraction["Patches"].get(name, []):
        assert source.count(patch["After"]) == 1, name
        source = source.replace(patch["After"], patch["Before"])
    return source


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
