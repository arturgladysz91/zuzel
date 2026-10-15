import importlib.util
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[2]
spec = importlib.util.spec_from_file_location("rea004a11_legacy", ROOT / "tools/rider-compatibility-audit/check-consumers.py")
audit = importlib.util.module_from_spec(spec)
spec.loader.exec_module(audit)


class FourRiderCausalExtractionTests(unittest.TestCase):
    def test_all_original_39_consumers_remain_exact(self):
        self.assertEqual(audit.verify(), 39)

    def test_causal_gate_clock_geometry_and_transaction_are_pinned(self):
        for name, before, after in [
            ("src/CoreSim/SimulationEngine.cs", "|| execution?.CausalHeat == true", "|| true"),
            ("src/CoreSim/Track/ExecutedPathTraversal.cs", "!pause.CausalHeat", "false"),
            ("src/CoreSim/Race/FrozenHeatContext.cs", "Interlocked.CompareExchange(ref _causalJournal, next, previous)", "previous"),
            ("src/CoreSim/Race/FourRiderCausalCoordinator.cs", "transaction(next);", "// omitted transaction"),
            ("src/CoreSim/Race/FourRiderCausalCoordinator.cs", "r.MinimumSeparationLowerBoundMeters < -GeometryNumerics.ContactDistanceMeters", "false"),
            ("src/CoreSim/Race/FourRiderCausalCoordinator.cs", "if (simultaneous > 1)", "if (false)"),
            ("src/CoreSim/SimulationSnapshot.cs", "internal double? ExecutionOriginTimeSeconds", "public double? ExecutionOriginTimeSeconds")]:
            source = (ROOT / name).read_text(encoding="utf-8")
            self.assertIn(before, source)
            with self.assertRaises(AssertionError):
                audit.restore_four_rider_causal(source.replace(before, after), name)
