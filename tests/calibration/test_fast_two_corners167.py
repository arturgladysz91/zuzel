"""Audit the sealed #167 native histories and the pre-holdout protocol."""
import hashlib
from io import BytesIO
import json
import math
from pathlib import Path
import unittest
from zipfile import ZipFile

ROOT = Path(__file__).resolve().parents[2]
DOCS = ROOT / "docs/experiments/fast-two-corners"


class FastTwoCorners167Tests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.bundle = ZipFile(DOCS / "evidence.zip")
        cls.addClassCleanup(cls.bundle.close)
        cls.events = [json.loads(line) for line in cls.bundle.read("ledger.jsonl").splitlines()]
        cls.protocol = json.loads(cls.bundle.read("protocol.json"))
        cls.results = {r["id"]: r for r in cls.events if r["Event"] == "result"}
        cls.archives = {r["id"]: r for r in cls.events if r["Event"] == "archive"}

    def test_artifact_and_source_seals(self):
        for line in (DOCS / "SHA256SUMS").read_text(encoding="ascii").splitlines():
            expected, filename = line.split("  ")
            self.assertEqual(hashlib.sha256((DOCS / filename).read_bytes()).hexdigest(), expected)
        frozen = json.loads(self.bundle.read("freeze.json"))
        for name, expected in frozen["Sources"].items():
            self.assertEqual(
                hashlib.sha256((ROOT / "tools/fast-two-corners" / name).read_bytes()).hexdigest().upper(),
                expected,
            )

    def test_disjoint_starts_and_bounded_trials(self):
        groups = [{(x["Gate"], x["Start"], x["Offset"]) for x in self.protocol[key]}
                  for key in ("TrainingStarts", "SelectionStarts", "ValidationStarts")]
        self.assertEqual(len(set.union(*groups)), sum(map(len, groups)))
        self.assertEqual(len(groups[-1]), 8)
        self.assertEqual(len(self.protocol["Seeds"]), 2)
        stages = {stage: [r for r in self.results.values() if r["stage"] == stage]
                  for stage in ("diagnosis", "training", "selection", "validation", "comparison")}
        self.assertEqual([len(v) for v in stages.values()], [2, 192, 8, 8, 4])
        self.assertEqual(sum(r["result"]["Pass"] for r in stages["validation"]), 0)
        self.assertEqual(sum(r["result"]["Pass"] for r in stages["diagnosis"] + stages["comparison"]), 6)
        attempted = {r["id"] for r in self.events if r["Event"] == "reserved"}
        self.assertEqual(attempted, self.results.keys())
        self.assertLess(json.loads(self.bundle.read("study.json"))["TotalCpuSeconds"], 900)

    def test_native_continuity_pending_and_failure_gate(self):
        course = next(r for r in self.events if r["Event"] == "inputs")
        self.assertAlmostEqual(course["Second"]["Entry"] - course["First"]["Exit"], 62)
        self.assertAlmostEqual(course["Goal"] - course["Second"]["Exit"], 20)
        self.assertEqual(len(self.archives), 14)
        for name, manifest in self.archives.items():
            with self.subTest(name=name):
                data = self.bundle.read(manifest["File"])
                self.assertEqual(hashlib.sha256(data).hexdigest().upper(), manifest["Sha256"])
                with ZipFile(BytesIO(data)) as archive:
                    native = json.loads(archive.read("native.json"))
                self.assertEqual(len(native["Riders"]), 1)
                self.assertFalse(native["Contacts"])
                rider, = native["Riders"]
                samples = rider["Samples"]
                self.assertEqual(samples[0]["Input"]["Time"], 0)
                self.assertEqual(samples[0]["Input"]["Rider"]["Speed"], 0)
                self.assertTrue(all(a["Input"]["Time"] < b["Input"]["Time"]
                                    for a, b in zip(samples, samples[1:])))
                for command in rider["Activations"]:
                    self.assertAlmostEqual(command["ActivationAt"] - command["ObservedAt"], .24)
                for index, decision in enumerate(rider["Decisions"]):
                    self.assertAlmostEqual(decision["Choice"]["Time"], index * .2)
                result = self.results[name]["result"]
                self.assertEqual(result["Fingerprint"], native["Fingerprint"])
                if result["Pass"]:
                    self.assertTrue(all(min(q["Inner"], q["Outer"]) >= .5 for q in samples))
                    self.assertTrue(all(q["Certificate"] >= .5 for q in samples[1:]))
                    self.assertTrue(all(abs(q["Input"]["Rider"]["SlideRadians"]) <= math.pi / 4 for q in samples))
                    goal = next(q for q in samples if q["Input"]["Rider"]["TotalReferenceMeters"] >= course["Goal"])
                    self.assertGreaterEqual(samples[-1]["Input"]["Time"] - goal["Input"]["Time"], 1 - 1e-8)
                    self.assertTrue(any(d["Choice"]["Time"] > goal["Input"]["Time"] for d in rider["Decisions"]))
                else:
                    self.assertEqual(result["Reason"], "beta exceeds 45 degrees")
                    self.assertGreater(abs(samples[-1]["Input"]["Rider"]["SlideRadians"]), math.pi / 4)
                    self.assertLess(result["EndReference"], course["Second"]["Entry"])
