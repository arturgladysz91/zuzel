import importlib.util
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[2]
spec = importlib.util.spec_from_file_location('reactive_control_audit', ROOT / 'tools/rider-compatibility-audit/check-consumers.py')
audit = importlib.util.module_from_spec(spec)
spec.loader.exec_module(audit)


class ReactiveControlExtractionTests(unittest.TestCase):
    def test_all_original_consumers_remain_exact(self):
        self.assertEqual(audit.verify(), 39)

    def test_an_older_point_cannot_be_presented_as_the_current_observation(self):
        name = 'src/CoreSim/Track/ExecutedPathTraversal.cs'
        source = (ROOT / name).read_text()
        changed = source.replace('controlSource(nodes![^1])', 'controlSource(nodes![0])')
        self.assertNotEqual(source, changed)
        with self.assertRaises(AssertionError):
            audit.restore_reactive_control(changed, name)
