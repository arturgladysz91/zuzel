from pathlib import Path
import gzip,hashlib,json,subprocess,unittest
R=Path(__file__).resolve().parents[2];D=R/'docs/experiments/dynamic-slip-preview'
def read(p):
 p=Path(p);return json.loads(gzip.decompress(p.read_bytes()) if p.suffix=='.gz' else p.read_bytes())
class Dynamic160Tests(unittest.TestCase):
 @classmethod
 def setUpClass(cls):cls.a=read(D/'analysis.json')
 def test_prediction_is_not_native_success(self):
  self.assertEqual(self.a['new_native_worlds'],len(self.a['native']))
  held=[q for q in self.a['native'] if q['validation']]
  if self.a['verdict']=='PASS':self.assertGreaterEqual(len(held),5);self.assertTrue(all(q['pass_'] and q['pair']['delta_s']<0 for q in held))
  else:self.assertIn(self.a['verdict'],['PARTIAL','FAIL'])
 def test_failure_reproduction_is_dense_and_exact(self):
  a=read(D/'forecasts/exact-rest-speed12.json.gz');b=read(R/'docs/experiments/geometry-speed-audit/forecasts/rest-speed12.json.gz')
  for key in ['Initial','Points','End']:self.assertEqual(a[key],b[key])
  self.assertEqual([(q['State'],q['Action']) for q in a['Actions']],[(q['State'],q['Action']) for q in b['Actions']])
 def test_failed_diagnostics_are_kept(self):
  rows={q['Label']:q for q in self.a['forecasts']};self.assertFalse(rows['adaptive-v2']['Reached']);self.assertIsNotNone(rows['adaptive-v2']['Error']);self.assertFalse(rows['adaptive-wide-v5']['Legal']);self.assertFalse(rows['ablate-actuator-v2']['Legal'])
 def test_recorded_certificate_is_complete(self):
  for p in (D/'certificates').glob('*.json.gz'):
   q=read(p)
   if q['Error'] is None:r=q['Review'];self.assertTrue(r['Independent']);self.assertTrue(r['ConditionalCertificate']);self.assertEqual(len(r['Tree']),13);self.assertEqual(len(r['Terminals']),9)
 def test_every_accepted_native_command_had_fresh_certificate(self):
  for run in self.a['native']:
   accepted=0
   for p in sorted((D/'policies'/run['label']).glob('*.json.gz')):
    q=read(p)
    if q['Error'] is None:
     r=q['Review'];self.assertTrue(r['ConditionalCertificate'] and r['Independent']);self.assertEqual(len(r['Tree']),13);self.assertEqual(len(r['Terminals']),9);self.assertEqual(r['Time'],q['Observed']['Input']['Time']);accepted+=1
   self.assertEqual(accepted,run['accepted_decisions'])
 def test_native_replay_references_only_exact_original_samples(self):
  for run in read(D/'native-replay.json.gz')['runs']:
   raw=read(R/run['source'])['Riders'][0]['Samples'];self.assertEqual(run['sha256'],hashlib.sha256((R/run['source']).read_bytes()).hexdigest());self.assertEqual(run['samples'][0]['index'],0);self.assertEqual(run['samples'][-1]['index'],len(raw)-1)
   for q in run['samples']:
    s=raw[q['index']];v=s['Input']['Rider'];self.assertEqual((q['t'],q['x'],q['y'],q['wheel'],q['crank']),(s['Input']['Time'],v['Position']['X'],v['Position']['Y'],s['Input']['Omega'],s['CrankOmega']))
 def test_pairs_have_full_identical_initial_conditions(self):
  for q in self.a['native']:self.assertTrue(q['pair']['initial_state_exact']);self.assertEqual(len(q['six_bends']),6)
 def test_predeclared_budget_is_not_silently_increased(self):
  p=read(D/'budget-plan.json');rows=read(D/'budget-journal.json');self.assertFalse(p['DefaultEnabled']);self.assertTrue(p['DeclaredBeforeExperiments']);self.assertTrue(all(q['Complete'] for q in rows));self.assertEqual(len(rows),len({q['Label'] for q in rows}))
  for kind,limit,cap in [('diagnostic',60,1800),('certificate',6,360),('native',11,19800)]:
   part=[q for q in rows if q['Kind']==kind];self.assertLessEqual(len(part),limit);self.assertLessEqual(sum(q['ChargedCpuSeconds'] for q in part),cap+2*len(part))
 def test_first_bend_yaw_error_precedes_boundary_crossing(self):
  q=self.a['first_response_error'];self.assertAlmostEqual(q['Time'],4.8);self.assertGreater(abs(q['Actual']['YawAcceleration']-q['Imagined']['YawAcceleration']),.1);q=self.a['first_opposite_yaw_response'];self.assertAlmostEqual(q['Time'],6.2);self.assertLess(q['DesiredAlpha']*q['Actual']['YawAcceleration'],0)
 def test_evidence_sources_and_frozen_physics_match_seal(self):
  seal=read(D/'evidence-manifest.json')
  for path,h in seal['Files'].items():self.assertEqual(hashlib.sha256((R/path).read_bytes()).hexdigest(),h,path)
  for path,h in seal['FrozenTrees'].items():self.assertEqual(subprocess.check_output(['git','rev-parse','HEAD:'+path],cwd=R,text=True).strip(),h,path)
if __name__=='__main__':unittest.main()
