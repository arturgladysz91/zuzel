"""Scientific gates: partial native results must never become a three-lap PASS."""
import gzip,json,unittest
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2]
DOC=ROOT/'docs/experiments/autonomous-corner-planning'
class Autonomous146EvidenceTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):cls.data=json.loads((DOC/'analysis.json').read_text(encoding='utf-8'))
    def test_budget_includes_aborted_world_and_stays_bounded(self):
        b=self.data['native_budget'];self.assertLessEqual(b['worlds'],50);self.assertLessEqual(b['cpu_s']+(b['read_only_sequence_audit_cpu_s'] or 0),900)
        self.assertTrue(all(x['Complete'] for x in b['attempts']));self.assertTrue(any(x.get('Aborted') for x in b['attempts']))
    def test_no_full_autonomous_success_is_inferred_from_four_corners(self):
        r=next(r for r in self.data['runs'] if r['name']=='autonomous146-viable');self.assertEqual(r['laps'],2);self.assertIsNone(r['finish_s'])
        cs=[c for c in self.data['corners'] if c['run']==r['name']];self.assertEqual(sum(c['complete'] for c in cs),4)
        self.assertTrue(all(c['seconds'] is None for c in cs if not c['complete']))
    def test_recovered_forecasts_have_explicit_coverage_gap(self):
        a=self.data['plan_audits']['autonomous146-viable'];self.assertEqual(a['reviews'],72);self.assertEqual(a['native_decisions'],192)
        self.assertLess(a['coverage_end_s'],14.21);self.assertTrue(a['queues_match']);self.assertTrue(a['submitted_match']);self.assertTrue(a['activation_match'])
    def test_repeated_local_autonomy_preserves_all_native_samples(self):
        def read(n):return json.loads(gzip.decompress((DOC/'native'/f'{n}.json.gz').read_bytes()))['Riders'][0]['Samples']
        a=[s for s in read('local-K1-29') if s['Input']['Time']<=36+1e-8];b=read('repeat-local-K1-29');self.assertEqual(a,b)
    def test_retrospective_audit_does_not_claim_recovered_planner_intent(self):
        d=json.loads((DOC/'executed-sequence-audit.json').read_text(encoding='utf-8'));self.assertEqual(d['NativeWorlds'],0)
        self.assertIn('not original planner intent',d['Scope']);self.assertTrue(all(r['Passed'] for r in d['Rows']))
if __name__=='__main__':unittest.main()
