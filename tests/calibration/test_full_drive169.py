"""Verify the sealed native comparisons and the study-only force interventions."""
from collections import Counter
from io import BytesIO
import hashlib
import json
import math
from pathlib import Path
import unittest
from zipfile import ZipFile

ROOT = Path(__file__).resolve().parents[2]
DOCS = ROOT / "docs/experiments/full-drive-stability"


class FullDrive169Tests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.package = ZipFile(DOCS / "169-dowody.zip")
        cls.addClassCleanup(cls.package.close)
        cls.native = ZipFile(BytesIO(cls.package.read("native.zip")))
        cls.addClassCleanup(cls.native.close)
        cls.measurements = json.loads(cls.package.read("measurements.json"))
        cls.rows = {r["Id"]: r for r in cls.measurements["trials"]}

    def test_seals_budget_and_complete_ledger(self):
        for line in (DOCS / "SHA256SUMS").read_text(encoding="ascii").splitlines():
            digest, name = line.split("  ")
            self.assertEqual(hashlib.sha256((DOCS / name).read_bytes()).hexdigest(), digest)
        for name, digest in json.loads(self.package.read("source-sha256.json")).items():
            self.assertEqual(hashlib.sha256((ROOT / name).read_bytes()).hexdigest(), digest)
        plan = json.loads(self.package.read("budget.json"))
        cpu = json.loads(self.package.read("analysis-receipt.json"))
        self.assertEqual(len(self.rows), cpu["worlds"])
        self.assertLess(cpu["total_cpu"], plan["BudgetCpuSeconds"])
        self.assertLessEqual(cpu["worlds"], plan["UserMaxWorlds"])
        counts = Counter(row["Stage"] for row in self.rows.values())
        self.assertEqual(counts, {"A": 15, "B": 11, "C": 10, "D": 16})
        events = [json.loads(line) for line in self.package.read("ledger.jsonl").splitlines()]
        self.assertEqual({e["id"] for e in events if e["Event"] == "reserved"}, set(self.rows))
        self.assertFalse(any(e["Event"] == "phase-aborted" for e in events))

    def test_same_prefix_full_throttle_and_archived_reproductions(self):
        for cohort in ("60", "70", "80", "166", "167"):
            self.assertEqual(len({r["PreActivationSha256"] for r in self.rows.values()
                                  if r["Cohort"] == cohort and r["StepSeconds"] == .01}), 1)
        old = json.loads(self.package.read("sources.json"))
        path = ROOT / old["History"]
        self.assertEqual(hashlib.sha256(path.read_bytes()).hexdigest().upper(), old["HistorySha256"])
        mapping = {"A-original-70-1.00-replay168-10": "AB-70-counter-100",
                   "D-166-original-fixed-100": "C-166-fixed-100",
                   "D-167-original-fixed-100": "C-167-fixed-100"}
        with ZipFile(path) as historical, ZipFile(BytesIO(historical.read("native.zip"))) as archive:
            for name, old_name in mapping.items():
                self.assertTrue(self.rows[name]["ReproducesArchive"])
                self.assertEqual(json.loads(self.native.read(name + ".json"))["Fingerprint"],
                                 json.loads(archive.read(old_name + ".json"))["Fingerprint"])
        for name, r in self.rows.items():
            with self.subTest(name=name):
                run = json.loads(self.native.read(name + ".json"))
                rider, = run["Riders"]
                self.assertFalse(run["Contacts"])
                for i, d in enumerate(rider["Decisions"]):
                    self.assertAlmostEqual(d["Choice"]["Time"], .2 * i)
                for cmd in rider["Activations"]:
                    self.assertAlmostEqual(cmd["ActivationAt"] - cmd["ObservedAt"], .24)
                for q in rider["Samples"][1:]:
                    f = q["Forces"]
                    w = q["Powertrain"]["Wheel"]
                    load = f["TyreLoads"]
                    self.assertAlmostEqual(load["FrontNormalNewtons"] + load["RearNormalNewtons"], 147 * 9.81)
                    self.assertGreaterEqual(min(load["FrontNormalNewtons"], load["RearNormalNewtons"]), 0)
                    self.assertAlmostEqual(load["EffectiveFrictionCoefficient"], 1.2845000267028808)
                    self.assertLessEqual(w["RearUtilization"], 1.000001)
                    self.assertGreaterEqual(w["LongitudinalLossJoules"], -1e-7)
                    self.assertLessEqual(abs(f["SteeringAfter"] - f["SteeringBefore"]),
                                         math.pi / 2 * q["Powertrain"]["Seconds"] + 2e-7)
                    if q["Input"]["Time"] > r["ActivationSeconds"] + 1e-8:
                        self.assertEqual(q["Input"]["Active"]["Drive"], r["Drive"])
                self.assertLess(abs(r["after"]["energy"]["closure"]), 1)
                if r["Legal"]:
                    self.assertGreaterEqual(r["MinimumSwept"], .5)
                    self.assertLessEqual(r["PeakBetaDegrees"], 45)
                else:
                    self.assertEqual(r["status"], "β45")

    def test_mechanisms_are_distinguishable(self):
        for kph in (60, 70, 80):
            for p in ("0.25", "0.50"):
                self.assertFalse(self.rows[f"A-original-{kph}-{p}-pulse-10"]["Legal"])
                self.assertTrue(self.rows[f"A-original-{kph}-{p}-feedback-10"]["Legal"])
            self.assertTrue(self.rows[f"B-h055-{kph}-0.50-pulse-10"]["Legal"])
            self.assertFalse(self.rows[f"C-separated-{kph}-0.50-pulse-10"]["Legal"])
            self.assertTrue(self.rows[f"C-lateral-{kph}-0.50-pulse-10"]["Legal"])
        for cohort in (166, 167):
            self.assertTrue(self.rows[f"D-{cohort}-original-feedback-100"]["Legal"])
            a, b = [self.rows[f"D-{cohort}-h055-feedback-{gas}"]["after"]["delta_kph"] for gas in (50, 100)]
            self.assertGreater(b, a)
        grid = json.loads(self.package.read("contact-grid.json"))
        self.assertEqual(len(grid), 3 * 7 * 6 * 3)
        for r in grid:
            self.assertGreaterEqual(r["DissipationWatts"], -1e-6)
            self.assertLessEqual(math.hypot(r["Fx"], r["Fy"]), r["Br"] + .001)
            if r["Variant"] != "original":
                self.assertEqual(r["Fx"], r["FxWithDoubleInertia"])
                self.assertEqual(r["Fy"], r["FyWithDoubleInertia"])
            if r["Kappa"] == 0:
                self.assertAlmostEqual(r["Fx"], 0)
            if r["AlphaDegrees"] == 0:
                self.assertAlmostEqual(r["Fy"], 0)
        for q in self.measurements["native_dt"].values():
            self.assertEqual(len(q), 3)
            self.assertLess(abs(q["0.02"]["beta"] - q["0.005"]["beta"]), .1)
        for q in self.measurements["local_dt"].values():
            self.assertEqual(len(q), 4)
            self.assertLess(abs(q[-1]["EndKph"] - q[-2]["EndKph"]), .02)
