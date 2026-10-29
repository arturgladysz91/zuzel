"""Study148 complete-distance equivalence and causal-scope evidence gates."""
import gzip,importlib.util,json,unittest
from pathlib import Path
R=Path(__file__).resolve().parents[2];D=R/'docs/experiments/planner-speed-root-cause'
def read(p):return json.loads(gzip.decompress(p.read_bytes()) if p.name.endswith('.gz') else p.read_bytes())
class Study148Tests(unittest.TestCase):
 @classmethod
 def setUpClass(cls):cls.a=read(D/'analysis.json')
 def test_complete_physical_samples_and_commands_match147(self):
  self.assertEqual(len(self.a['full_equivalence']),3)
  for q in self.a['full_equivalence']:self.assertTrue(q['samples_bit_equal']);self.assertTrue(q['commands_equal']);self.assertTrue(q['fingerprint_equal']);self.assertEqual(q['commands'],270)
 def test_resource_ledger_preserves_failed_strategies_and_cpu_bound(self):
  b=read(D/'budget.json');self.assertTrue(all(x['Complete'] for x in b));self.assertLess(sum(x['CpuSeconds'] for x in b),1500);self.assertEqual(sum(x['NativeWorlds'] for x in b),24);self.assertTrue(any(x['Outcome']=='abstain' for x in b));self.assertTrue(any(x.get('CpuMeasurement')=='upper bound' for x in b))
 def test_exact_optimization_reduces_work_without_claiming_target_cpu(self):
  r=next(x for x in self.a['full_runs'] if x['name']=='autonomous148-prefix');self.assertEqual(r['controller_cpu_s'],82);self.assertGreater(r['controller_cpu_s'],60);self.assertEqual(r['forecast_contacts'],3552122);self.assertEqual(r['full_K1_calls'],1643);self.assertFalse(r['boundary_or_contact_violation'])
 def test_diagnostics_keep_causal_prefix_and_beta_stops_distinct(self):
  self.assertTrue(all(x['prefix_exact'] for x in self.a['causal']));fail=[x for x in self.a['causal'] if x['outcome']=='experimental beta45 envelope'];self.assertEqual(len(fail),3);self.assertTrue(all(not x['actual_boundary_crossing'] and not x['physical_contact'] for x in fail));self.assertTrue(any(x['finish_s'] and x['delta_finish_vs_matched_control_s']>0 for x in self.a['causal'] if x['mode']!='control'))
 def test_window_end_is_not_a_claimed_full_lap_or_maximum_track_speed(self):
  c=self.a['capabilities'];self.assertEqual(len(c),8);self.assertTrue(all(x['outcome']=='experiment timeout' and x['controlled_window_legal'] for x in c));self.assertGreater(min(x['maximum_kph'] for x in c if x['name'].startswith('long')),140)
 def test_geometry_controls_preserve_g0_and_identify_changed_curve_family(self):
  g=self.a['geometry'];self.assertEqual({x['name'] for x in g},{'G0','G0c','G1','G2','G3','G4'});audit=read(D/'geometry-audit.json');self.assertFalse(audit['BankingModeled']);self.assertLess(audit['MinimumReferenceRadius'],25);self.assertTrue(all(abs(j['HeadingJump'])<1e-4 for j in audit['Joins']))
 def test_static_tyre_material_limitation_is_not_a_native_parameter_change(self):
  m=self.a['tyre_static_example'];self.assertEqual(len(m),3);self.assertEqual(len({x['TyreLoads']['TotalCapacityNewtons'] for x in m}),1);self.assertGreater(max(x['RearLongitudinal'] for x in m)-min(x['RearLongitudinal'] for x in m),1)
 def test_mechanical_adapters_have_no_untracked_equation_drift(self):
  spec=importlib.util.spec_from_file_location('gen148',R/'tools/planner-speed-root-cause/generate148.py');module=importlib.util.module_from_spec(spec);spec.loader.exec_module(module)
  for name,source in module.generated():
   path=R/('tools/shared-world-speedway/PlannerPerformance148' if name in ['FastCrank148.cs','ForecastContact148.cs'] else 'tools/planner-speed-root-cause')/name;self.assertEqual(path.read_text(encoding='utf-8'),source,name)
if __name__=='__main__':unittest.main()
