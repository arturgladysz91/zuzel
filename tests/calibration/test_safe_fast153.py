"""Sealed153 contracts. No new native world or planner search in Python."""
from pathlib import Path
import gzip,hashlib,json,subprocess,unittest,zipfile
R=Path(__file__).resolve().parents[2];D=R/'docs/experiments/safe-fast-contingency'
def read(p):return json.loads(gzip.decompress(p.read_bytes()) if str(p).endswith('.gz') else p.read_bytes())
class SafeFast153Tests(unittest.TestCase):
 def test_historical_half_meter_audit_does_not_relabel_legacy_completions(self):
  g=read(D/'geometry-audit.json');self.assertTrue(g['Passed']);self.assertTrue(g['OriginalAndIndependentGapBitsEqual'])
  self.assertEqual([(r['Poses'],r['PoseViolations'],r['LegalExecutedDecisions']) for r in g['Runs']],[(5412,969,199),(5530,983,206)])
  for r in g['Runs']:self.assertFalse(r['LegalThreeLapsUnderS']);self.assertGreater(r['DefinitelyPhysicalPoseViolations'],900);self.assertGreater(r['MinimumCenterGap'],.5);self.assertLess(r['MinimumGap'],.5)
 def test_exact_completed_native_race_preserves_all152_commands_and_fingerprint(self):
  r=read(D/'native/exact-L-v3-result.json');self.assertTrue(r['Passed']);self.assertTrue(r['Exact152NativeFingerprint']);self.assertEqual(r['Exact152Decisions'],276);self.assertEqual(r['CompletedLaps'],3);self.assertAlmostEqual(r['FinishSeconds'],55.1670788250403)
 def test_all_required_finite_scenarios_and_accepted_safety_reserves_remain(self):
  count=0
  for folder in (D/'policies').iterdir():
   result=D/'native'/(folder.name+'-result.json')
   if not result.exists():continue
   safety=read(result)['Regime']=='SafetyHalfMeter'
   for p in folder.glob('decision-*.json.gz'):
    x=read(p);count+=1;self.assertTrue(x['ConditionalCertificate']);self.assertTrue(x['Independent']);self.assertEqual(len(x['Tree']),13);self.assertEqual(len(x['Terminals']),9)
    for n in x['Tree']:
     if n['Depth']<2:self.assertEqual(sorted(c['Sign'] for c in x['Tree'] if c['Parent']==n['Id']),[-1,0,1])
    for terminal in x['Terminals']:
     s=x['Tapes'][terminal['Tape']]['Safety'];self.assertTrue(s['Legal']);self.assertGreaterEqual(s['MinimumGapMeters'],.5 if safety else .1);self.assertGreaterEqual(s['SweepLowerBoundMeters'],.5 if safety else 0)
  self.assertGreaterEqual(count,276)
 def test_a_safety_completion_requires_the_actual_native_swept_margin(self):
  rows=[r for r in read(D/'analysis.json')['full_runs'] if r['new'] and r['regime']=='SafetyHalfMeter'];self.assertTrue(rows)
  for r in rows:
   if r['outcome']=='completed':self.assertEqual(r['laps'],3);self.assertGreaterEqual(r['minimum_sweep_m'],.5);self.assertEqual(r['half_meter_pose_violations'],0);self.assertEqual(r['contacts'],0)
   else:self.assertIsNone(r['finish_s'])
 def test_budget_and_negative_trials_are_not_erased(self):
  rows=read(D/'budget.json');self.assertTrue(all(r['Complete'] for r in rows));self.assertLessEqual(sum(r['CpuSeconds'] for r in rows),1500);self.assertLessEqual(sum(r['NativeWorlds'] for r in rows),20)
  self.assertFalse(read(D/'bench-v1.json')['Passed']);self.assertTrue(read(D/'bench-v2.json')['Passed']);self.assertTrue(read(D/'bench-v3.json')['Passed']);self.assertFalse(read(D/'native/ranked-L-v1-result.json')['Passed']);self.assertFalse(read(D/'native/ranked-L-v2-result.json')['Passed'])
 def test_source_snapshots_match_the_measured_attempts(self):
  for p in (D/'sources').glob('*.json'):
   with zipfile.ZipFile(p.with_suffix('.zip')) as z:
    self.assertIsNone(z.testzip())
    for name,digest in read(p).items():self.assertEqual(hashlib.sha256(z.read(name)).hexdigest().upper(),digest)
 def test_every_frozen_blob_and_historical_tree_remains(self):
  proof=read(D/'source-proof.json');self.assertEqual(proof['Base'],'a9024b5bb141e8158fc7c441705768a7b7354d35');raw=subprocess.check_output(['git','ls-tree','-r','HEAD'],cwd=R,text=True);blobs={s.split('\t',1)[1]:s.split('\t',1)[0].split()[2] for s in raw.splitlines()}
  for path,digest in proof['FrozenGitBlobs'].items():self.assertEqual(blobs[path],digest,path)
  self.assertEqual(len(proof['FrozenHistoricalGitTrees']),150)
  for path,digest in proof['FrozenHistoricalGitTrees'].items():self.assertEqual(subprocess.check_output(['git','rev-parse','HEAD:'+path],cwd=R,text=True).strip(),digest,path)
 def test_generated_sources_reproduce_the_final_adapter(self):
  result=subprocess.run(['python',str(R/'tools/safe-fast-contingency/generate153.py'),'--check'],cwd=R,text=True,capture_output=True);self.assertEqual(result.returncode,0,result.stdout+result.stderr)
if __name__=='__main__':unittest.main()
