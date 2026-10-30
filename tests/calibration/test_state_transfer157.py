from pathlib import Path
import hashlib,json,math,subprocess,unittest
R=Path(__file__).resolve().parents[2];D=R/'docs/experiments/state-maneuver-transfer'
def read(p):return json.loads(p.read_bytes())
class StateTransfer157Tests(unittest.TestCase):
 def test_frozen_model_and_algorithm_were_not_tuned_after_first_held_results(self):
  f=read(D/'algorithm-freeze.json');self.assertTrue(f['BeforeFirstB'])
  for p,h in f['Hashes'].items():self.assertEqual(hashlib.sha256((R/'tools/state-maneuver-transfer'/p).read_bytes()).hexdigest(),h,p)
 def test_model_has_only_unordered_current_state_examples(self):
  rows=read(R/'tools/state-maneuver-transfer/model157.json');self.assertEqual(len(rows),450)
  for r in rows:self.assertEqual(set(r),{'Family','Features','Steering','Drive'});self.assertEqual(len(r['Features']),18);self.assertTrue(all(math.isfinite(x) for x in r['Features']));self.assertLessEqual(abs(r['Steering']),math.radians(35));self.assertTrue(0<=r['Drive']<=1)
  f=read(D/'training-freeze.json');self.assertTrue(f['FrozenBeforeFirstB']);self.assertFalse(any('held-' in p['Path'] for p in f['Sources']))
 def test_protocol_derivation_and_physical_sources_stay_frozen(self):
  p=subprocess.run(['node','tools/state-maneuver-transfer/generate157.mjs','--check'],cwd=R,capture_output=True,text=True);self.assertEqual(p.returncode,0,p.stdout+p.stderr)
  for path,tree in read(D/'source-proof.json')['FrozenTrees'].items():self.assertEqual(subprocess.check_output(['git','rev-parse','HEAD:'+path],cwd=R,text=True).strip(),tree,path)
 def test_gate_failure_never_becomes_full_autonomous_success(self):
  a=read(D/'analysis.json');self.assertFalse(a['success']);self.assertTrue(a['gates']['C'].startswith('NOT_RUN'));self.assertTrue(a['gates']['D'].startswith('NOT_RUN'))
  for r in a['native']:
   if not r['complete']:self.assertFalse(r['pass_'])
 def test_exact_kinematic_decomposition_does_not_claim_separate_yaw_causality(self):
  a=read(D/'analysis.json')['decomposition'];self.assertAlmostEqual(a['log_time'],a['log_distance']+a['negative_log_speed'],places=10);self.assertLess(a['distance_change_pct'],0);self.assertLess(a['mean_speed_change_pct'],0)
 def test_all_reservations_including_failed_exports_are_charged(self):
  rows=read(D/'budget.json');self.assertTrue(all(r['Complete'] and r['CpuSeconds']>0 for r in rows));self.assertEqual(len(rows),len({r['Name'] for r in rows}));self.assertLessEqual(sum(r['CpuSeconds'] for r in rows),1000);self.assertEqual(sum(r['NativeWorlds'] for r in rows),len(list((D/'native').glob('*-result.json'))));self.assertLessEqual(sum(r['NativeWorlds'] for r in rows),15)
if __name__=='__main__':unittest.main()
