from pathlib import Path
import hashlib,importlib.util,json,subprocess,sys,unittest
R=Path(__file__).resolve().parents[2];D=R/'docs/experiments/feasible-speed-profile'
sys.path.insert(0,str(R/'tools/s3-performance'))
from bundle162 import policies,read as bundle_read,verify
def read(p):return json.loads(Path(p).read_bytes())
POLICY={'Control164.cs','Feedback164.cs','Generator164.cs','Planner164.Generated.cs','SpeedProfile164.cs'}
class FeasibleSpeed164Tests(unittest.TestCase):
 def test_one_fixed_variant_and_original_certificate_transformation(self):
  subprocess.check_call([sys.executable,'tools/feasible-speed-profile/generate164.py','--check'],cwd=R)
  s=(R/'tools/feasible-speed-profile/Planner164.Generated.cs').read_text(encoding='utf-8');self.assertIn('Runtime158',s);self.assertNotIn('ResponseCache162',s);self.assertNotIn('CpuProfile163',s)
 def test_declared_quotas_and_all_completed_attempts_are_retained(self):
  p=read(D/'budget-plan.json');j=read(D/'budget-journal.json')
  self.assertEqual(p['MaximumDiagnostics'],3);self.assertEqual(p['MaximumCertificateProbes'],2)
  self.assertLessEqual(sum(x['Kind']=='diagnostic' for x in j),3);self.assertLessEqual(sum(x['Kind']=='certificate' for x in j),2);self.assertLessEqual(sum(x['Kind']=='native' for x in j),6)
  self.assertEqual(len({x['Label'] for x in j}),len(j));self.assertTrue(all(x['Complete'] for x in j))
  self.assertTrue(all(x['CpuSeconds']<=x['CpuCapSeconds']+1 for x in j if x['Kind']!='native'))
  # Native serialization after a stop is charged and retained, never hidden or
  # treated as additional physical execution under a relaxed budget.
  for x in j:
   if x['Kind']=='native' and x['CpuSeconds']>x['CpuCapSeconds']+1:
    n=next(q for q in read(D/'analysis.json')['native'] if q['label']==x['Label']);self.assertFalse(n['pass_']);self.assertTrue(any('CPU_STOP' in f['reason'] for f in n['failures']))
 def test_all_three_diagnostic_gates_are_legal_same_goal_improvements(self):
  for q in read(D/'analysis.json')['diagnostics']:
   self.assertTrue(q['GatePassed']);self.assertLess(q['TrackArrival'],q['Baseline161Arrival']-.02)
   track=next(x for x in q['Outcomes'] if x['Maneuver']=='Track');self.assertTrue(track['Legal']);self.assertGreaterEqual(track['Sweep'],.5);self.assertGreaterEqual(track['EndReference'],q['SafetyGoal'])
   self.assertEqual({x['Maneuver'] for x in q['Outcomes']},{'Track','Prepare','Recover'})
 def test_every_operation_uses_identical_five_file_policy_hash(self):
  seen=set()
  for p in D.rglob('*.zip'):
   sources=bundle_read(p,'sources.json');files={k:v for k,v in sources['Files'].items() if k in POLICY};self.assertEqual(set(files),POLICY)
   h=hashlib.sha256(json.dumps(files,separators=(',',':')).encode()).hexdigest().upper();self.assertEqual(h,sources['PolicyHash']);seen.add(h)
  self.assertEqual(len(seen),1)
 def test_consolidated_payload_hashes_and_crc(self):
  for p in D.rglob('*.zip'):self.assertGreater(verify(p),0)
 def test_fresh_certificates_and_native_review_protocol(self):
  a=read(D/'analysis.json');self.assertEqual(len(a['certificates']),2)
  for c in a['certificates']:
   self.assertTrue(c['GatePassed']);self.assertTrue(c['Independent']);self.assertEqual((c['Nodes'],c['Reserves']),(13,9))
  for n in a['native']:
   count=0
   for key,q,raw in policies(R/n['source']):
    if q['Error'] is not None:continue
    r=q['Review'];self.assertTrue(r['Independent'] and r['ConditionalCertificate']);self.assertEqual((len(r['Tree']),len(r['Terminals'])),(13,9));count+=1
   self.assertEqual(count,n['accepted_certificates'])
 def test_pass_requires_all_five_faster_matched_native_finishes_and_cpu(self):
  a=read(D/'analysis.json');held=[q for q in a['native'] if q['validation']]
  if a['verdict']=='PASS':
   self.assertEqual(len(held),5);self.assertTrue(a['sports_gate'] and a['cpu_gate'] and a['speed_profile_gate']);self.assertLess(a['median_finish_s'],79.60283030281548)
   for q in held:
    self.assertTrue(q['pass_'] and q['exact_initial']);self.assertLess(q['finish_s'],q['baseline_finish_s']);self.assertGreaterEqual(q['minimum_swept_m'],.5);self.assertLessEqual(q['maximum_beta_deg'],45);self.assertEqual(q['abstain'],0);self.assertEqual(q['contacts'],0)
  if any(not q['pass_'] for q in a['native']):self.assertTrue((D/'series-closure.json').exists());self.assertEqual(a['verdict'],'PARTIAL')
  if any(not q['faster_than161'] for q in a['native'] if not q['validation']):self.assertEqual(held,[]);self.assertTrue(read(D/'series-closure.json')['NoFurtherNativeWorlds'])
 def test_held_sources_are_frozen_after_success_and_starts_match161(self):
  a=read(D/'analysis.json');held=[q for q in a['native'] if q['validation']]
  if held:
   f=read(D/'controller-freeze.json');self.assertTrue(f['BeforeHeldResults']);dev=next(q for q in a['native'] if not q['validation']);self.assertTrue(dev['pass_'] and dev['faster_than161'])
   self.assertEqual(len({read((R/q['source']).with_name(q['label']+'-result.json'))['Scenario']['Id'] for q in held}),len(held))
   for q in held:self.assertEqual(bundle_read(R/q['source'],'sources.json')['Files'],f['Sources'])
  for q in a['native']:
   new=read((R/q['source']).with_name(q['label']+'-result.json'));old=read(R/'docs/experiments/trajectory-continuation/native'/(new['BaselineLabel']+'-result.json'));self.assertEqual(new['Initial'],old['Initial']);self.assertEqual(new['Spec'],old['Spec'])
 def test_evidence_manifest_and_all_older_trees(self):
  m=read(D/'evidence-manifest.json')
  for p,h in m['Files'].items():self.assertEqual(hashlib.sha256((R/p).read_bytes()).hexdigest(),h,p)
  for p,h in m['FrozenTrees'].items():self.assertEqual(subprocess.check_output(['git','rev-parse','HEAD:'+p],cwd=R,text=True).strip(),h,p)
if __name__=='__main__':unittest.main()
