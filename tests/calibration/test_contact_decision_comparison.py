import gzip
import hashlib
import importlib.util
import json
from pathlib import Path
import tempfile
import unittest

spec = importlib.util.spec_from_file_location("contact_decision_comparison",
    Path(__file__).resolve().parents[2] / "tools/contact-decision-audit/compare.py")
comparison = importlib.util.module_from_spec(spec)
spec.loader.exec_module(comparison)


class ContactDecisionComparisonTests(unittest.TestCase):
    def capture(self, directory, phase, leaves):
        raw = json.dumps(leaves).encode()
        row = {"Leaves": len(leaves), "IEEELeaves": sum(isinstance(v, str) and v.startswith(("float:", "double:")) for v in leaves.values()),
               "SHA256": hashlib.sha256(raw).hexdigest().upper()}
        (directory / f"{phase}.json").write_text(json.dumps({"decision": row}))
        (directory / f"{phase}.json.decision.json.gz").write_bytes(gzip.compress(raw))

    def test_exact_platform_comparison_rejects_any_enabled_drift(self):
        for changed in ("double:3FF0000000000001", "float:3F800000", "string:double:3FF0000000000000", True, None):
            with self.subTest(changed=changed), tempfile.TemporaryDirectory() as tmp:
                root = Path(tmp)
                windows = root / "determinism-windows-latest/contact-decision"
                ubuntu = root / "determinism-ubuntu-latest/contact-decision"
                windows.mkdir(parents=True)
                ubuntu.mkdir(parents=True)
                for directory in (windows, ubuntu):
                    self.capture(directory, "before", {"role": "double:3FF0000000000000"})
                    self.capture(directory, "after", {"role": "double:3FF0000000000000"})
                self.capture(ubuntu, "after", {"role": changed})
                with self.assertRaisesRegex(AssertionError, "cross-platform drift"):
                    comparison.compare_platforms(root)

    def test_complete_correction_map_retains_types_bits_missing_fields_and_decisions(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            self.capture(root, "before", {"clearance": "double:8000000000000000", "valid": False, "removed": 1})
            self.capture(root, "after", {"clearance": "double:0000000000000000", "valid": True, "new": "string:1"})
            delta = comparison.differences(root, "before", root, "after")["decision"]
            self.assertEqual(set(delta), {"clearance", "valid", "removed", "new"})
            self.assertEqual(delta["clearance"]["Before"], {"Type": "double", "Bits": "8000000000000000"})
            self.assertEqual(delta["removed"]["After"], {"Type": "missing"})
            self.assertEqual(delta["new"]["After"], {"Type": "string", "Value": "1"})

    def test_compressed_raw_sidecar_must_match_its_manifest(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            self.capture(root, "after", {"value": "double:3FF0000000000000"})
            row = comparison.exact.read_manifest(root, "after")["decision"]
            (root / "after.json.decision.json.gz").write_bytes(gzip.compress(b"{}"))
            with self.assertRaisesRegex(AssertionError, "hash mismatch"):
                comparison.read_leaves(root, "after", "decision", row)
