import importlib.util
from pathlib import Path
import unittest
from unittest.mock import patch

ROOT = Path(__file__).resolve().parents[2]
spec = importlib.util.spec_from_file_location('scheduled_control_audit', ROOT / 'tools/rider-compatibility-audit/check-consumers.py')
audit = importlib.util.module_from_spec(spec)
spec.loader.exec_module(audit)


class ScheduledControlExtractionTests(unittest.TestCase):
    def test_original_39_consumers_and_hashes_stay_frozen(self):
        self.assertEqual(audit.verify(), 39)

    def test_early_activation_cannot_pass_the_reviewed_contract(self):
        name = 'src/CoreSim/Track/ExecutedPathTraversal.cs'
        source = (ROOT / name).read_text()
        changed = source.replace('physicalTime < pending.ActivationElapsedTimeSeconds',
                                 'physicalTime > pending.ActivationElapsedTimeSeconds')
        self.assertNotEqual(source, changed)
        with self.assertRaises(AssertionError):
            audit.restore_scheduled_control(changed, name)

    def test_physics_outside_control_plumbing_is_still_rejected(self):
        original_read = Path.read_text
        def tampered(path, *args, **kwargs):
            source = original_read(path, *args, **kwargs)
            if path == ROOT / 'src/CoreSim/Track/ExecutedPathTraversal.cs':
                changed = source.replace('var ds = GeometricDistance(', 'var ds = 2 * GeometricDistance(')
                self.assertNotEqual(source, changed)
                return changed
            return source
        with patch.object(Path, 'read_text', tampered):
            with self.assertRaises(AssertionError):
                audit.verify()
