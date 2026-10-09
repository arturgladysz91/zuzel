"""Reject drift outside the exact, presence-aware REA-009 topology evidence."""
import copy
import hashlib
import json
from pathlib import Path
import runpy
import tempfile
import unittest
ROOT = Path(__file__).resolve().parents[2]
helper = runpy.run_path(str(ROOT / 'tools/contested-performance/compare.py'))
class Rea009HistoricalBoundaryTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(); self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        expected = json.loads((ROOT / 'tests/fixtures/rea009-historical-boundaries.json').read_text())['Changes']
        self.before = {}; self.after = {}
        for case, delta in expected.items():
            self.before[case] = {p: v['Before'] for p,v in delta.items() if v['BeforePresent']}
            self.after[case] = {p: v['After'] for p,v in delta.items() if v['AfterPresent']}
            self.before[case]['root.PhysicalSpeed'] = 'float:41B00000'
            self.after[case]['root.PhysicalSpeed'] = 'float:41B00000'
        self.capture('before', self.before); self.capture('after', self.after)
    def capture(self, phase, cases):
        manifest={}
        for case, leaves in cases.items():
            raw=json.dumps(leaves,sort_keys=True,separators=(',',':')).encode()
            (self.root/f"{phase}.json.{case.replace('/', '_')}.json").write_bytes(raw)
            manifest[case]={'Leaves':len(leaves),'IEEELeaves':sum(isinstance(v,str) and v.startswith(('float:','double:')) for v in leaves.values()),'SHA256':hashlib.sha256(raw).hexdigest().upper()}
        (self.root/f'{phase}.json').write_text(json.dumps(manifest))
    def test_exact_topological_delta_is_required(self):
        self.assertEqual(300,helper['compare'](self.root)['Rea009ChangedLeaves'])
        self.capture('after',self.before)
        with self.assertRaises(AssertionError): helper['compare'](self.root)
    def test_extra_physical_change_is_rejected_even_with_matching_reversal(self):
        drift=copy.deepcopy(self.after)
        for leaves in drift.values(): leaves['root.PhysicalSpeed']='float:41B00001'
        self.capture('after',drift)
        with self.assertRaisesRegex(AssertionError,'Exact original-main behavior changed'): helper['compare'](self.root)
    def test_null_cannot_replace_missing_boundary_metadata(self):
        drift=copy.deepcopy(self.after)
        for leaves in drift.values(): leaves['root[0].Motions[0].ExitBoundary']=None
        self.capture('after',drift)
        with self.assertRaises(AssertionError): helper['compare'](self.root)
