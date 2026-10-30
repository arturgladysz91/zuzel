"""Sealed155 semantics and source preservation, no experimental search in CI."""
from pathlib import Path
import gzip,hashlib,json,subprocess,sys,unittest,zipfile
R=Path(__file__).resolve().parents[2];D=R/'docs/experiments/hierarchical-maneuvers'
def read(p):return json.loads(gzip.decompress(p.read_bytes()) if p.suffix=='.gz' else p.read_bytes())
class Hierarchical155Tests(unittest.TestCase):
 def test_complete_policies_keep_all_signs_reserves_and_independence(self):
  accepted=[]
  for p in list(D.glob('[ABC]-*.json.gz'))+list(D.glob('stability*.json.gz')):
   x=read(p);r=x['Review']
   if not x['Passed']:self.assertFalse(r['ConditionalCertificate']);continue
   accepted.append(p.name);self.assertTrue(r['Independent']);self.assertTrue(r['ConditionalCertificate']);self.assertEqual((len(r['Tree']),len(r['Terminals'])),(13,9))
   for n in r['Tree']:
    if n['Depth']<2:self.assertEqual(sorted(c['Sign'] for c in r['Tree'] if c['Parent']==n['Id']),[-1,0,1])
   for leaf in r['Terminals']:
    t=r['Tapes'][leaf['Tape']];s=t['Safety'];self.assertTrue(s['Legal']);self.assertGreaterEqual(s['MinimumGapMeters'],.5);self.assertGreaterEqual(s['SweepLowerBoundMeters'],.5);self.assertLessEqual(len(t['Actions']),130);self.assertEqual(t['SafetyGoal'],r['SafetyGoal'])
  self.assertEqual(len(accepted),7)
 def test_final_matrix_is_partial_and_no_failed_speedup_is_promoted(self):
  a=read(D/'analysis.json');self.assertEqual(a['Verdict'],'PARTIAL');self.assertEqual(len(a['Rows']),23);self.assertEqual(len(a['Final']),9)
  for x in a['Final']:
   self.assertEqual(x['passed'],x['mode']=='A' or x['mode']=='C' and x['time_s']==0)
   if x['mode']!='A':self.assertEqual(x['full_k1'],0)
  self.assertEqual(a['NewNativeWorlds'],0)
 def test_own_observation_stability_preserves_source_and_actual_micro_delta(self):
  prior=read(D/'C-0.0-v4.json.gz')['Review']['PhysicalHash']
  for p in D.glob('stability*.json.gz'):
   x=read(p);self.assertTrue(x['Passed']);self.assertEqual(x['PriorPhysicalHash'],prior);self.assertEqual(x['Review']['Counts']['Physical']['FullK1'],0)
   if 'micro' in p.name:self.assertGreater(x['MeasuredPositionDeltaMeters'],0);self.assertLess(x['MeasuredPositionDeltaMeters'],.000001)
 def test_native_reference_is_exact154_and_forecasts_never_native(self):
  x=read(D/'replay.json.gz');n=x['NativeReference'];self.assertEqual(n['Study'],154);self.assertEqual(n['Kind'],'native-reference');self.assertEqual(n['CompletedLaps'],0)
  p=(D/n['File']).resolve();self.assertEqual(hashlib.sha256(p.read_bytes()).hexdigest().upper(),n['Sha256'])
  for c in x['Cases']:
   for s in c['Series']:self.assertEqual(s['Kind'],'forecast');self.assertEqual(s['FullPolicy'],s['Passed']);self.assertTrue(s['Points']);self.assertAlmostEqual(s['Points'][0]['t'],c['Time'],places=8)
 def test_all_experiments_and_exports_are_charged_without_budget_extension(self):
  rows=read(D/'budget.json');self.assertTrue(all(x['Complete'] for x in rows));self.assertEqual(len({x['Name'] for x in rows}),len(rows));self.assertLessEqual(sum(x['CpuSeconds'] for x in rows),1200);self.assertEqual(sum(x['NativeWorlds'] for x in rows),0)
  for p in list(D.glob('[ABC]-*.json.gz'))+list(D.glob('stability*.json.gz')):self.assertIn(p.name.removesuffix('.json.gz'),[x['Name'] for x in rows])
 def test_actual_attempt_sources_are_archived_and_hashed(self):
  for p in (D/'sources').glob('*.json'):
   with zipfile.ZipFile(p.with_suffix('.zip')) as z:
    self.assertIsNone(z.testzip())
    for name,h in read(p).items():self.assertEqual(hashlib.sha256(z.read(name)).hexdigest().upper(),h)
 def test_all_physics_data_old_tools_and_historical_trees_stay_frozen(self):
  x=read(D/'source-proof.json');self.assertEqual(x['Base'],'405111a721d5bc5ba1477999f25b3837e8c62307');raw=subprocess.check_output(['git','ls-tree','-r','HEAD'],cwd=R,text=True);blobs={s.split('\t',1)[1]:s.split('\t',1)[0].split()[2] for s in raw.splitlines()}
  for p,h in x['FrozenGitBlobs'].items():self.assertEqual(blobs[p],h,p)
  for p,h in x['FrozenHistoricalGitTrees'].items():self.assertEqual(subprocess.check_output(['git','rev-parse','HEAD:'+p],cwd=R,text=True).strip(),h,p)
 def test_generated_certificate_protocol_has_no_k1_fallback(self):
  p=R/'tools/hierarchical-maneuvers';r=subprocess.run([sys.executable,str(p/'generate155.py'),'--check'],cwd=R,capture_output=True,text=True);self.assertEqual(r.returncode,0,r.stdout+r.stderr);s=(p/'Planner155.Generated.cs').read_text(encoding='utf-8');self.assertNotIn('Runtime.Seed(',s);self.assertNotIn('SafeSeed(',s);self.assertIn('MaximumReviewForecasts = 32768',s);self.assertIn('>= 30)',s)
if __name__=='__main__':unittest.main()
