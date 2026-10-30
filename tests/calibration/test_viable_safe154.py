"""Sealed154 evidence and provenance; no new planner search or native world."""
from pathlib import Path
import gzip,hashlib,json,subprocess,unittest,zipfile
R=Path(__file__).resolve().parents[2];D=R/'docs/experiments/viable-safe-contingency';OLD=R/'docs/experiments/safe-fast-contingency'
def read(p):return json.loads(gzip.decompress(p.read_bytes()) if p.suffix=='.gz' else p.read_bytes())
class ViableSafe154Tests(unittest.TestCase):
 def test_every_saved153_decision_and_failed_callback_are_attributed(self):
  x=read(D/'diagnosis153.json');self.assertEqual(len(x['rows']),70);self.assertEqual(sum(r['full_k1'] for r in x['rows']),6329);self.assertEqual(sum(r['accepted_full_policy'] for r in x['rows']),69);self.assertFalse(x['rows'][-1]['accepted']);self.assertAlmostEqual(x['rows'][-1]['time_s'],13.8);self.assertEqual(x['rows'][-1]['nodes'],9);self.assertEqual(x['rows'][-1]['leaves'],6);self.assertEqual(x['local_k1_rejection_counts'],'NOT_RECORDED_BY_153')
 def test_gate_a_is_a_local_certificate_not_a_full_race(self):
  old=read(OLD/'policies/ranked-S-v1/decision-0068.json.gz')
  for p in D.glob('S*-A-*.json.gz'):
   x=read(p);self.assertEqual(x['PriorPhysicalHash'],old['PhysicalHash'])
   if x['Error'] is None:self.assertTrue(x['Review']['Independent']);self.assertEqual(len(x['Review']['Tree']),13);self.assertEqual(len(x['Review']['Terminals']),9)
 def test_every_accepted_policy_keeps_all_signs_and_safety_reserves(self):
  n=0
  for p in (D/'policies').glob('*/decision-*.json.gz'):
   x=read(p);n+=1;self.assertTrue(x['Independent']);self.assertTrue(x['ConditionalCertificate']);self.assertEqual(len(x['Tree']),13);self.assertEqual(len(x['Terminals']),9)
   for node in x['Tree']:
    if node['Depth']<2:self.assertEqual(sorted(c['Sign'] for c in x['Tree'] if c['Parent']==node['Id']),[-1,0,1])
   for leaf in x['Terminals']:
    s=x['Tapes'][leaf['Tape']]['Safety'];self.assertTrue(s['Legal']);self.assertGreaterEqual(s['MinimumGapMeters'],.5);self.assertGreaterEqual(s['SweepLowerBoundMeters'],.5)
  self.assertGreater(n,0)
 def test_world_and_cpu_budget_preserves_negative_experiments(self):
  rows=read(D/'budget.json');self.assertTrue(all(x['Complete'] for x in rows));self.assertLessEqual(sum(x['CpuSeconds'] for x in rows),1500);self.assertLessEqual(sum(x['NativeWorlds'] for x in rows),15);self.assertTrue(any('S1-B' in x['Name'] for x in rows));self.assertTrue(any('S2-B' in x['Name'] for x in rows))
 def test_source_archives_match_actual_attempts(self):
  for p in (D/'sources').glob('*.json'):
   with zipfile.ZipFile(p.with_suffix('.zip')) as z:
    self.assertIsNone(z.testzip())
    for path,h in read(p).items():self.assertEqual(hashlib.sha256(z.read(path)).hexdigest().upper(),h)
 def test_all_historical_trees_and_physics_sources_are_unchanged(self):
  x=read(D/'source-proof.json');self.assertEqual(x['Base'],'de500229a6fdb5c57da7cbeb90f5484c28d57130');raw=subprocess.check_output(['git','ls-tree','-r','HEAD'],cwd=R,text=True);blobs={s.split('\t',1)[1]:s.split('\t',1)[0].split()[2] for s in raw.splitlines()}
  for p,h in x['FrozenGitBlobs'].items():self.assertEqual(blobs[p],h,p)
  for p,h in x['FrozenHistoricalGitTrees'].items():self.assertEqual(subprocess.check_output(['git','rev-parse','HEAD:'+p],cwd=R,text=True).strip(),h,p)
 def test_generated_adapters_and_sweep_diagnostic(self):
  r=subprocess.run(['python',str(R/'tools/viable-safe-contingency/generate154.py'),'--check'],cwd=R,text=True,capture_output=True);self.assertEqual(r.returncode,0,r.stdout+r.stderr);s=read(D/'sweep-diagnostic.json');self.assertTrue(s['Passed']);self.assertGreaterEqual(s['NativeTwoConeMinimum'],s['NativeOriginalMinimum']);self.assertEqual(s['Recovered'],6)
 def test_partial_native_runs_are_never_reported_as_full_s(self):
  for x in read(D/'analysis.json')['full_runs']:
   if x['full_s_pass']:self.assertEqual(x['laps'],3);self.assertGreaterEqual(x['minimum_sweep_m'],.5);self.assertEqual(x['half_meter_pose_violations'],0);self.assertEqual(x['contacts'],0)
   else:self.assertIsNone(x['finish_s'])
 def test_s0_preserves_the_entire153_native_prefix_and_chosen_policies(self):
  x=read(D/'enrichment.json');self.assertTrue(x['native_fingerprint_equal']);self.assertTrue(x['all_dense_native_sample_fields_equal']);self.assertTrue(x['all_accepted_physical_hashes_equal']);self.assertEqual(x['accepted_decisions'],69)
 def test_local_half_meter_sweep_and_other_rejections_are_not_conflated(self):
  rows=[r for r in read(D/'enrichment.json')['local_candidate_classes'] if r['available']];self.assertTrue(rows)
  for r in rows:self.assertEqual(r['total'],sum(r[k] for k in ['pose_below','sweep_only','beta_only','other','legal']))
if __name__=='__main__':unittest.main()
