from pathlib import Path
import gzip,importlib.util,json,math,subprocess,unittest
R=Path(__file__).resolve().parents[2];D=R/'docs/experiments/line-throttle-physics'
spec=importlib.util.spec_from_file_location('study156',R/'tools/line-throttle-physics/analyze156.py');m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m)
class LineThrottle156Tests(unittest.TestCase):
 def test_real_curvature_of_circle(self):
  def point(t):return {'Input':{'Time':t,'Rider':{'Velocity':{'X':-20*math.sin(.8*t),'Y':20*math.cos(.8*t)}}}}
  self.assertAlmostEqual(m.curvature(point(1),point(1.001)),.04,places=7)
 def test_changing_offset_on_straight_has_nonzero_actual_curvature(self):
  def point(t):return {'Input':{'Time':t,'Rider':{'Velocity':{'X':1,'Y':math.cos(t)}}}}
  self.assertAlmostEqual(m.curvature(point(math.pi/2-.001),point(math.pi/2+.001)),-1,places=6)
 def test_native_passes_are_complete_and_family_members(self):
  a=m.read(D/'analysis.json');passed=[r for r in a['results'] if r['status']=='PASS'];self.assertTrue(passed)
  for r in passed:
   self.assertTrue(r['complete']);self.assertTrue(r['safe']);self.assertTrue(r['family_achieved']);self.assertTrue(r['exact_initial']);self.assertGreaterEqual(r['sweep_m'],.5);self.assertLessEqual(r['max_beta_deg'],45)
 def test_every_native_initial_state_is_identical_and_failures_not_promoted(self):
  for p in (D/'native').glob('*-result.json'):
   x=m.read(p);self.assertTrue(x['ExactInitialKey'])
   if not x['Complete'] or not x['Safety']['Legal']:self.assertFalse(x['Pass'])
 def test_distance_compensation_is_measured_over_the_common_window(self):
  a=m.read(D/'analysis.json')
  for c in a['comparisons']:
   rs=[r for r in a['matrix_results'] if r['fixture']==c['fixture'] and r['strategy']==c['strategy']]
   x=next(r for r in rs if r['family']==c['from_family']);y=next(r for r in rs if r['family']==c['to_family'])
   self.assertAlmostEqual(c['required_speed_gain_pct'],(y['distance_m']/x['distance_m']-1)*100)
   self.assertEqual(c['time_delta_s']<0,c['actual_speed_gain_pct']>c['required_speed_gain_pct'])
 def test_mechanical_native_derivation_changes_guards_only(self):
  p=subprocess.run(['node','tools/line-throttle-physics/generate156.mjs','--check'],cwd=R,capture_output=True,text=True);self.assertEqual(p.returncode,0,p.stdout+p.stderr)
 def test_all_failed_reservations_are_charged_and_budget_is_bounded(self):
  rows=m.read(D/'budget.json');self.assertEqual(len(rows),len({r['Label'] for r in rows}));self.assertTrue(all(r['Closed'] for r in rows));self.assertLessEqual(sum(r['CpuSeconds'] for r in rows),1200)
  failure=next(r for r in rows if r['Label']=='matrix-historical-v3');self.assertFalse(failure['Measured']);self.assertEqual(failure['CpuSeconds'],130)
 def test_production_physics_and_all_prior_experiments_remain_exact_git_trees(self):
  proof=m.read(D/'source-proof.json')
  self.assertEqual(proof['Base'],'d1eda795c66a527d25f9ecebd22a6f82d30e5a9b')
  for path,tree in proof['FrozenTrees'].items():self.assertEqual(subprocess.check_output(['git','rev-parse','HEAD:'+path],cwd=R,text=True).strip(),tree,path)
if __name__=='__main__':unittest.main()
