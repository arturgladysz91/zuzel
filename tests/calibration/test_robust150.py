"""Causal evidence, native scope, immutable physics and uncached policy contracts."""
import gzip,hashlib,json,unittest
from pathlib import Path
R=Path(__file__).resolve().parents[2];D=R/'docs/experiments/robust-autonomous-racing'
def read(p):return json.loads(gzip.decompress(p.read_bytes()) if p.name.endswith('.gz') else p.read_bytes())
class Study150Tests(unittest.TestCase):
 @classmethod
 def setUpClass(cls):cls.a=read(D/'analysis.json');cls.runs={x['name']:x for x in cls.a['full_runs']}
 def test_causal_fixed_tape_excludes_new_K1_as_necessary_cause(self):
  for model in ['A','B']:
   p=read(D/'diagnosis-safe'/f'{model}-B-predicted-shift-fixed.json.gz');q=read(D/'diagnosis-safe'/f'{model}-C-observed-shift-fixed.json.gz');self.assertTrue(p['Safety']['Legal']);self.assertFalse(q['Safety']['Legal']);self.assertEqual(p['Actions'][:len(q['Actions'])],q['Actions']);self.assertEqual(p['States'][0]['Input']['Pending'],q['States'][0]['Input']['Pending'])
 def test_original_forecast_reproduction_and_absolute_time_alignment(self):
  self.assertTrue(all(x['OriginalForecastExactlyReproduced'] for x in read(D/'diagnosis-summary.json')))
  for x in self.a['diagnosis']:
   if x['name']=='A-original-fixed':self.assertIsNone(x['first_different_action_time']);self.assertIsNone(x['first_growing_error'])
   if x['name']=='D-fresh-K1':self.assertAlmostEqual(x['first_different_action_time'],5.8)
 def test_native_same_tape_crosses_and_feedback_recovers_exact_prefix(self):
  for model in ['A','B']:
   bad=self.runs['fixed-certified-tape-native-'+model];good=self.runs['robust150-exact-recovery-'+model];self.assertEqual(bad['outcome'],'confirmed footprint crossing');self.assertEqual(good['outcome'],'completed');self.assertLess(good['maximum_beta_deg'],45);self.assertGreater(good['minimum_sweep_m'],.1);self.assertEqual(good['kind'],'rolling diagnostic');self.assertEqual(good['laps'],1);self.assertEqual(good['prefix_until'],1)
 def test_no_new_full_race_success_and_failure_is_explicit(self):
  self.assertEqual(self.a['verdict'],'PARTIAL');full=[x for x in self.a['full_runs'] if x['kind']=='full autonomous attempt'];self.assertEqual(len(full),1);self.assertEqual(full[0]['end_s'],.4);self.assertIsNone(full[0]['finish_s']);self.assertEqual(full[0]['initial_kph'],0);self.assertEqual(read(D/'index.json')['DefaultRun'],full[0]['name'])
 def test_all333_complete_sequences_reproduce_without_prediction_or_feedback_cache(self):
  audit=read(D/'contract-audit.json');self.assertEqual(sum(x['VerifiedDecisions'] for x in audit),333)
  for x in audit:self.assertTrue(x['AllCommandSequencesExact']);self.assertTrue(x['AllCompleteForecastsLegal']);self.assertTrue(x['PhysicsAndFeedbackCacheBypassed']);self.assertEqual(x['Counts']['PredictionHits'],0);self.assertEqual(x['Counts']['FeedbackHits'],0)
 def test_native_queues_and_all_issued_first_actions_match_saved_contracts(self):
  for name,audit in self.a['plan_audits'].items():
   self.assertTrue(audit['queues_match']);self.assertEqual(audit['reviews'],audit['native_decisions'])
   for row in audit['rows']:
    self.assertEqual(row['actual_action'],row['selected'][0]);activation=row['actual_activation']
    if activation:self.assertAlmostEqual(activation['ActivationAt']-row['time_s'],.24)
 def test_local_path_improvement_does_not_substitute_for_full_race(self):
  c=next(x for x in self.a['corners'] if x['run']=='robust150-prefix-B-45' and x['lap']==3 and x['bend']==2);base=next(x for x in self.a['corners'] if x['run']=='separated149-autonomous' and x['lap']==3 and x['bend']==2);self.assertLess(c['seconds'],base['seconds']);self.assertLess(c['path_m'],base['path_m']);self.assertLess(c['exit_kph'],base['exit_kph']);self.assertEqual(c['kind'],'prefix diagnostic');self.assertTrue(all(not x['complete'] for x in self.a['corners'] if x['run']=='robust150-tube-full-A'))
 def test_benchmark_near_angle_exists_but_full_benchmark_not_found(self):
  x=self.a['manual_action_space'];self.assertFalse(x['OriginalGridIncludes']);self.assertTrue(x['SimilarActionInNewRule']);self.assertLess(x['NearestExpandedAngleDifferenceRadians'],1e-7);self.assertIsNotNone(self.a['manual_probe']['Failure'])
 def test_budget_preserves_failed_attempts_and_stops_before_exhaustion(self):
  b=read(D/'budget.json');self.assertTrue(all(x['Complete'] for x in b));self.assertLess(sum(x['CpuSeconds'] for x in b),1180);self.assertEqual(sum(x['NativeWorlds'] for x in b),8);self.assertTrue(any('exception' in x['Outcome'] for x in b));self.assertTrue(any('ABSTAIN' in x['Outcome'] for x in b))
 def test_physics_and_old_evidence_hashes_are_unchanged(self):
  for path,expected in read(D/'source-proof.json')['FrozenHashes'].items():self.assertEqual(hashlib.sha256((R/path).read_bytes().replace(b'\r\n',b'\n') if path.endswith('.cs') else (R/path).read_bytes()).hexdigest(),expected)
 def test_finite_recourse_failure_is_not_hidden(self):
  q=read(D/'next-observation-recourse-audit.json');self.assertTrue(q);self.assertTrue(all(not x['AllNextScenariosCovered'] for x in q));ab=self.a['plan_audits']['robust150-tube-full-A']['abstentions'][0]['Review'];self.assertGreater(ab['Legal'],0);self.assertEqual(ab['Robust'],0)
if __name__=='__main__':unittest.main()
