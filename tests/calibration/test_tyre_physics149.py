"""Evidence gates: nominal grip, mathematical separation, full distance and honest diagnostic scope."""
import gzip,hashlib,importlib.util,json,unittest
from pathlib import Path
R=Path(__file__).resolve().parents[2];D=R/'docs/experiments/tyre-physics-corner-limits'
def read(p):return json.loads(gzip.decompress(p.read_bytes()) if p.name.endswith('.gz') else p.read_bytes())
class Study149Tests(unittest.TestCase):
 @classmethod
 def setUpClass(cls):cls.a=read(D/'analysis.json')
 def test_final148_reproduced_and_cpu_is_new_measurement(self):
  e=self.a['baseline_equivalence'];self.assertTrue(e['samples_equal'] and e['commands_equal']);self.assertEqual(e['samples'],5412);self.assertEqual(e['commands'],270)
  r=next(x for x in self.a['full_runs'] if x['name']=='baseline149-final148');self.assertEqual(r['cost_scope'],'new149 measurement');self.assertEqual(r['controller_cpu_s'],83.15625)
 def test_B_is_repeated_but_is_not_a_sporting_improvement_over_A(self):
  a=next(x for x in self.a['full_runs'] if x['name']=='baseline149-final148');b=next(x for x in self.a['full_runs'] if x['name']=='separated149-autonomous');self.assertGreater(b['finish_s'],a['finish_s']);self.assertEqual(b['legal_completions'],1);self.assertTrue(self.a['B_repeatability']['samples_equal']);self.assertTrue(self.a['B_repeatability']['commands_equal']);self.assertEqual(len([c for c in self.a['corners'] if c['run']==b['name'] and c['complete']]),6)
 def test_same_capacity_engine_and_historical_files(self):
  for path,expected in read(D/'source-proof.json')['FrozenHashes'].items():self.assertEqual(hashlib.sha256((R/path).read_bytes().replace(b'\r\n',b'\n') if path.endswith('.cs') else (R/path).read_bytes()).hexdigest(),expected)
  a=read(D/'native/baseline149-final148.json.gz')['Riders'][0]['Samples'][100]['Forces']['TyreLoads'];b=read(D/'native/separated149-autonomous.json.gz')['Riders'][0]['Samples'][100]['Forces']['TyreLoads'];self.assertEqual(a,b)
 def test_resource_ledger_includes_failures_and_stays_bounded(self):
  b=read(D/'budget.json');self.assertTrue(all(x['Complete'] for x in b));self.assertLess(sum(x['CpuSeconds'] for x in b),1200);self.assertLessEqual(sum(x['NativeWorlds'] for x in b),50);self.assertTrue(any('Preflight rejected' in x['Outcome'] for x in b));self.assertTrue(any(x['Outcome']=='confirmed footprint crossing' for x in b))
 def test_force_budget_and_reference_curve_are_not_boosted(self):
  s=read(D/'contact-summary.json');self.assertLess(s['MaximumRearUtilization'],1);self.assertLess(s['MaximumEnergyIdentityResidualJoules'],1e-6);self.assertLess(s['NominalMaximumFxDifference'],.001);self.assertLess(s['NominalMaximumFyDifference'],.001)
 def test_boundary_beta_and_incomplete_prefix_are_separate(self):
  q=[x for x in self.a['fixtures'] if x['name'].startswith('entry-')];self.assertEqual(len(q),24);self.assertTrue(any(x['physical_crossing'] for x in q));self.assertTrue(all(x['ExtendedBetaObservation'] for x in q));self.assertTrue(any(not x['intervention_reached'] for x in self.a['causal']));self.assertTrue(all(x['PrefixExact'] for x in self.a['causal'] if x.get('PrefixSource')=='B'))
 def test_planner_prediction_queues_are_consistent_and_native_corner_errors_small(self):
  self.assertTrue(all(x['queues_match'] for x in self.a['plan_audits'].values()));q=next(x for x in self.a['fixtures'] if x['name']=='corner-parity-B');self.assertEqual(q['Comparisons'],44);self.assertLess(q['MaximumPredictionPositionError'],.0001)
 def test_generated_B_changes_no_frozen_adapter(self):
  s=importlib.util.spec_from_file_location('g149',R/'tools/tyre-physics-corner-limits/generate149.py');m=importlib.util.module_from_spec(s);s.loader.exec_module(m)
  for p,text in m.outputs():self.assertEqual(p.read_text(encoding='utf-8'),text)
 def test_high_speed_sequence_probe_abstains_without_a_physical_DNF(self):
  probes=[x for x in self.a['fixtures'] if x['name'].startswith('planned-entry-')];self.assertEqual(len(probes),2)
  for p in probes:self.assertTrue(p['controller_abstention']);self.assertFalse(p['physical_crossing']);self.assertFalse(p['numeric_instability']);self.assertEqual(p['EndSeconds'],1);self.assertGreater(p['MinimumSweep'],0)
  for p in self.a['local_replanning']:self.assertTrue(p['prefix_choices_exact']);self.assertEqual(p['candidate_count'],20);self.assertEqual(p['reason_counts'],{'beta45 envelope':12,'footprint reserve':8});self.assertTrue(p['prediction_error']['QueueMatches'])
if __name__=='__main__':unittest.main()
