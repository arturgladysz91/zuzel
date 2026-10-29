"""Full native completion, durable audit and measured-resource gates for Study147."""
import hashlib,json,unittest,importlib.util
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2];DOC=ROOT/'docs/experiments/efficient-autonomous-planner'
class Efficient147EvidenceTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.data=json.loads((DOC/'analysis.json').read_text(encoding='utf-8'))
        cls.result=json.loads((DOC/'native/autonomous147-shortlist-result.json').read_text(encoding='utf-8'))
    def test_complete_native_trial_has_all_commands_and_certificates(self):
        r=self.result;self.assertTrue(r['TechnicalPass']);self.assertEqual(r['CompletedLaps'],3);self.assertTrue(r['ZeroStartFromGateB']);self.assertTrue(r['JournalComplete']);self.assertTrue(r['ModelCertificatesAllLegal']);self.assertEqual(r['JournalDecisions'],r['NativeDecisions']);self.assertGreaterEqual(r['MinimumSweepCertificate'],0)
    def test_resource_budget_counts_profiles_failures_and_one_world(self):
        b=self.data['budget'];self.assertLessEqual(b['total_cpu_s'],900);self.assertEqual(b['native_worlds'],1);self.assertTrue(all(x['Complete'] for x in b['attempts']));self.assertTrue(any('exception' in x.get('Outcome','') for x in b['attempts']))
    def test_sport_and_compute_improvements_are_separate_measured_claims(self):
        r=self.result;self.assertLess(r['FinishSeconds'],54.794529838609535);self.assertLess(r['ControllerCpuSeconds'],410.140625);self.assertGreater(r['ControllerCpuSeconds'],27.03125)
        variants={r['Mode']:r for r in self.data['comparisons']};self.assertTrue(variants['exact-22-6']['EndStateBitExact']);self.assertFalse(variants['exact-22-6']['StrategyChanged']);self.assertTrue(variants['shortlist-22-6']['StrategyChanged']);self.assertFalse(variants['shortlist-22-6']['CommandMatchesBaseline'])
    def test_journal_hash_and_goal_predictions_have_real_native_support(self):
        p=DOC/'native/autonomous147-shortlist-decisions.jsonl';self.assertEqual(hashlib.sha256(p.read_bytes()).hexdigest().upper(),self.result['JournalSha256'])
        a=self.data['journal_audit'];self.assertEqual(a['decisions'],270);self.assertTrue(a['active_queue_matches']);self.assertGreater(len(a['unmodified_executed_goal_forecasts']),100);self.assertLess(a['maximum_unmodified_goal_error_s'],.01)
    def test_all_six_corners_are_completed_without_filling_missing_146(self):
        cs=self.data['corners'];new=[c for c in cs if c['run']=='autonomous147-shortlist'];old=[c for c in cs if c['run']=='autonomous146-viable'];self.assertEqual(len(new),6);self.assertTrue(all(c['complete'] for c in new));self.assertEqual(sum(c['complete'] for c in old),4)
    def test_generated_performance_adapter_preserves_frozen_equations(self):
        spec=importlib.util.spec_from_file_location('proof147',ROOT/'tools/efficient-autonomous-planner/check-source147.py');module=importlib.util.module_from_spec(spec);spec.loader.exec_module(module);self.assertTrue(module.check())
if __name__=='__main__':unittest.main()
