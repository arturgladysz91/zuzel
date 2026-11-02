from pathlib import Path
import gzip, hashlib, json, math, unittest
R=Path(__file__).resolve().parents[2];D=R/'docs/experiments/geometry-speed-audit'
def read(p):
 p=Path(p);return json.loads(gzip.decompress(p.read_bytes()) if p.suffix=='.gz' else p.read_bytes())

class GeometrySpeed159Tests(unittest.TestCase):
 @classmethod
 def setUpClass(cls):cls.a=read(D/'analysis.json')
 def test_forecasts_and_local_equilibria_never_count_as_native_finishes(self):
  self.assertEqual(self.a['verdict'],'PARTIAL');self.assertEqual(self.a['new_native_worlds'],0)
  self.assertEqual(len(self.a['forecast_trials']),14)
  self.assertEqual(len(self.a['equilibria']),84)
  self.assertFalse(next(q for q in self.a['forecast_trials'] if q['Label']=='rest-speed12')['Legal'])
  self.assertFalse(read(D/'speed12-certificate.json.gz')['Review']['ConditionalCertificate'])
 def test_native_inputs_are_exactly_ten_original_complete_s3_histories(self):
  self.assertEqual(len(self.a['native']),10)
  for row in self.a['native']:
   p=R/row['source'];self.assertEqual(hashlib.sha256(p.read_bytes()).hexdigest(),row['source_sha256'])
   run=read(p);self.assertEqual(run['Riders'][0]['CompletedLaps'],3);self.assertEqual(len(run['Crossings']),3)
   self.assertEqual(len(row['six_bends']),6);self.assertGreaterEqual(row['minimum_sweep_m'],.5)
   self.assertAlmostEqual(sum(row['lap_times_s']),row['finish_s'],places=8)
 def test_native_replay_has_no_interpolated_physical_state(self):
  data=read(D/'native-replay.json.gz')
  for row in data['runs']:
   ss=read(R/row['source'])['Riders'][0]['Samples']
   self.assertEqual(row['samples'][0]['index'],0);self.assertEqual(row['samples'][-1]['index'],len(ss)-1)
   for s in row['samples']:
    raw=ss[s['index']];self.assertEqual(raw['Input']['Time'],s['t']);self.assertEqual(raw['Input']['Rider']['Position']['X'],s['x']);self.assertEqual(raw['Input']['Rider']['Position']['Y'],s['y']);self.assertEqual(raw['Input']['Omega'],s['wheel']);self.assertEqual(raw['CrankOmega'],s['crank'])
 def test_width_c_is_not_a_new_reference_curve_or_native_validation(self):
  b,c=read(D/'geometry.json.gz')['Tracks'];self.assertEqual(b['LapMeters'],c['LapMeters'])
  for x,y in zip(b['Sections'],c['Sections']):self.assertEqual(x['ReferenceXY'],y['ReferenceXY']);self.assertEqual(x['Curvature'],y['Curvature'])
  rows=self.a['geometry_recheck'];self.assertEqual(sum(q['BadSweeps']>0 for q in rows),2)
  self.assertAlmostEqual(rows[5]['MinimumSweep'],.32048870627105913,places=8)
 def test_exact_boundary_polygons_are_closed_and_have_original_spacing(self):
  for g in read(D/'geometry.json.gz')['Tracks']:
   n=math.ceil(g['LapMeters']/.05)
   for edge in ['Inner','Outer']:
    pts=g['Boundaries'][edge];self.assertEqual(len(pts),n+1);self.assertEqual(pts[0],pts[-1])
 def test_energy_is_weighted_to_actual_finish_and_closes(self):
  for row in self.a['native']:
   self.assertAlmostEqual(row['all']['duration_s'],row['finish_s'],places=8)
   self.assertLess(abs(row['all']['energy_J']['total_energy_residual_J']),2)
 def test_causal_witnesses_require_negative_requested_and_active_but_positive_available_acceleration(self):
  self.assertEqual(self.a['paired_contact_queries'],50);self.assertEqual(len(self.a['causal_witnesses']),25)
  for q in self.a['causal_witnesses']:self.assertLess(q['desired_acceleration'],0);self.assertLess(q['active_acceleration'],0);self.assertGreater(q['full_gas_acceleration'],0)
 def test_equilibrium_needs_original_clutch_and_wheel_residuals(self):
  a=read(D/'operating-points.json');actual=[]
  for q in a['Rows']:
   b=q['Best'];c=q['ClutchCheck'];ok=bool(c and c['WithinThrottle'] and c['ClutchCapacitySatisfied'] and math.sqrt(sum(v*v for v in b['Residual']))<.01 and abs(c['YawTorque'])<.01 and abs(c['WheelTorqueResidual'])<.01 and abs(c['CrankTorqueResidual'])<.01);actual.append(ok)
  self.assertEqual(actual,[q['local_equilibrium'] for q in self.a['equilibria']]);self.assertEqual(sum(actual),62)
 def test_diagnostics_stay_inside_predeclared_budget_including_failed_attempts(self):
  plan=read(D/'budget-plan.json');self.assertFalse(plan['defaultEnabled'])
  rows=[read(p) for p in D.glob('cost-*.json')]
  self.assertTrue(any(not q['Complete'] for q in rows));self.assertEqual(sum(q['NativeWorlds'] for q in rows),0)
  charge=sum(q.get('ChargedCpuSeconds',q['CpuSeconds']) for q in rows)
  self.assertLess(charge,plan['diagnosticCpuCapSeconds']);self.assertLessEqual(len(self.a['forecast_trials']),plan['maximumLocalContinuations'])
 def test_manifest_covers_exact_evidence_and_tool_sources(self):
  seal=read(D/'evidence-manifest.json');self.assertEqual(seal['Base'],self.a['base'])
  for p,h in seal['Files'].items():self.assertEqual(hashlib.sha256((R/p).read_bytes()).hexdigest(),h,p)
 def test_previous_physics_and_historical_trees_are_unchanged(self):
  import subprocess
  seal=read(D/'evidence-manifest.json')
  for p,h in seal['FrozenTrees'].items():self.assertEqual(subprocess.check_output(['git','rev-parse','HEAD:'+p],cwd=R,text=True).strip(),h,p)
  self.assertEqual(subprocess.check_output(['git','rev-parse','HEAD:tools/robust-three-lap'],cwd=R,text=True).strip(),seal['Study158SourceTree'])

if __name__=='__main__':unittest.main()
