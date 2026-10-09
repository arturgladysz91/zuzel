import importlib.util
from pathlib import Path
import unittest
ROOT = Path(__file__).resolve().parents[2]
spec = importlib.util.spec_from_file_location('rea009_legacy', ROOT/'tools/rider-compatibility-audit/check-consumers.py')
audit = importlib.util.module_from_spec(spec)
spec.loader.exec_module(audit)
class Rea009ExtractionTests(unittest.TestCase):
    def test_original_consumers_remain_exact(self):
        self.assertEqual(audit.verify(), 39)
    def test_changed_completion_selection_is_rejected(self):
        name='src/CoreSim/SimulationEngine.cs'
        source=(ROOT/name).read_text().replace('resolution.Outcome == SegmentOutcome.Crash\n            ? rider.Position.Advance', 'true\n            ? rider.Position.Advance', 1)
        with self.assertRaises(AssertionError): audit.restore_segment_completion(source, name)
    def test_duplicate_handoff_is_rejected(self):
        name='src/CoreSim/SimulationEngine.cs'
        source=(ROOT/name).read_text()
        patch=__import__('json').loads(audit.REA009_EXTRACTION.read_text())['Patches'][name][0]['After']
        with self.assertRaises(AssertionError): audit.restore_segment_completion(source+'\n'+patch, name)
