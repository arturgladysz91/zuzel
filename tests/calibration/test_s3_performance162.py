from pathlib import Path
import gzip,hashlib,importlib.util,json,math,statistics,subprocess,sys,unittest
R=Path(__file__).resolve().parents[2];D=R/'docs/experiments/s3-performance';S=R/'docs/experiments/trajectory-continuation';sys.path.insert(0,str(R/'tools/s3-performance'))
from bundle162 import policies,read as bundle_read,verify

def read(p):
 b=Path(p).read_bytes();return json.loads(gzip.decompress(b) if str(p).endswith('.gz') else b)
class S3Performance162Tests(unittest.TestCase):
 def test_native_performance_pass_requires_all_five_matched_faster_cases(self):
  a=read(D/'analysis.json');held=[q for q in a['native'] if q['validation']]
  if a['performance_verdict']=='PASS':
   dev=next(q for q in a['native'] if not q['validation']);self.assertTrue(dev['pass_']);self.assertLess(dev['cpu_after_s'],dev['cpu_before_s'])
   self.assertEqual(len(held),5);self.assertEqual(len({q['scenario']['Id'] for q in held}),5);self.assertTrue(all(q['pass_'] and q['exact_native_fingerprint'] and q['exact_all_physical_reviews'] and q['all_maneuvers_equal'] for q in held));self.assertAlmostEqual(a['median_finish_s'],79.60283030281548)
   for before,after in [('cpu_before_s','cpu_after_s'),('planner_cpu_before_s','planner_cpu_after_s')]:self.assertLess(sum(q[after] for q in held),sum(q[before] for q in held));self.assertLess(statistics.median(q[after] for q in held),statistics.median(q[before] for q in held))
 def test_independent_pair_is_fresh_and_not_satisfied_by_proposal_memo(self):
  cs=[read(p) for p in sorted((D/'certificates').glob('*.json.gz'))];self.assertEqual(len(cs),2);self.assertEqual(len({q['Review']['PhysicalHash'] for q in cs}),1)
  for q in cs:
   r=q['Review'];self.assertTrue(r['Independent'] and r['ConditionalCertificate']);self.assertEqual(len(r['Tree']),13);self.assertEqual(len(r['Terminals']),9);self.assertGreater(r['Counts']['Physical']['IndependentForecasts'],0)
  q=next(q for q in cs if q['Optimized']);self.assertGreater(q['ResponseStatistics']['Hits'],0);self.assertEqual(q['ResponseStatistics']['Requests'],q['ResponseStatistics']['Hits']+q['ResponseStatistics']['Evaluations'])
 def test_consolidated_bundles_verify_payload_bytes_and_frozen_sources(self):
  a=read(D/'analysis.json')
  for n in a['native']:
   p=R/n['bundle'];self.assertEqual(hashlib.sha256(p.read_bytes()).hexdigest(),n['bundle_sha256']);self.assertEqual(verify(p),n['policy_records'])
   if n['validation']:self.assertEqual(bundle_read(p,'sources.json')['Files'],read(D/'controller-freeze.json')['Sources'])
   self.assertTrue(n['all_maneuvers_equal'])
 def test_corrupt_consolidated_payload_is_rejected(self):
  import tempfile,zipfile
  raw=b'{"value":1}';line=b'{"key":"decision-0000","payload":'+raw+b'}\n';index={'Format':'S3Evidence162-v1','Policies':[{'Key':'decision-0000','Bytes':len(raw),'SHA256':hashlib.sha256(raw).hexdigest().upper()}]}
  with tempfile.TemporaryDirectory() as tmp:
   path=Path(tmp)/'corrupt.zip'
   with zipfile.ZipFile(path,'w') as z:z.writestr('policies.jsonl',line.replace(b':1}',b':2}'));z.writestr('index.json',json.dumps(index))
   with self.assertRaises(AssertionError):list(policies(path))
 def test_tightest_native_point_is_straight_and_swept_formula_is_unchanged(self):
  q=read(D/'tightest161.json');a,b=q['before'],q['after'];dt=b['Input']['Time']-a['Input']['Time'];v,w=a['Input']['Rider'],b['Input']['Rider'];travel=dt*.5*(max(v['Speed'],w['Speed'])+1.05*max(abs(v['AngularVelocity']),abs(w['AngularVelocity'])))
  self.assertAlmostEqual(q['time'],41.8);self.assertEqual(q['segment'],4);self.assertEqual(q['lap'],2);self.assertAlmostEqual(q['minimum_endpoint_pose']-travel-.0005,q['swept'],delta=1e-8);self.assertAlmostEqual(q['swept'],.5174019137391851);self.assertGreaterEqual(q['swept'],.5)
 def test_prepare_has_a_real_conditional_tree_witness_and_is_retained(self):
  w=read(D/'prepare-witness.json');self.assertEqual(len(w),1);self.assertAlmostEqual(w[0]['time'],68.8);self.assertEqual(w[0]['nodes'][0]['Depth'],2);self.assertTrue(w[0]['nodes'][0]['Source'].endswith('/Prepare'));self.assertEqual({q['Maneuver'] for q in w[0]['outcomes']},{'Track','Prepare','Recover'})
 def test_budget_and_original_protocol_sources(self):
  plan=read(D/'budget-plan.json');rows=read(D/'budget-journal.json');self.assertEqual(plan['MaximumDiagnostics'],3);self.assertEqual(plan['MaximumCertificateProbes'],2);self.assertEqual(plan['ProfilingNativeWorlds'],0);self.assertLessEqual(sum(q['Kind']=='diagnostic' for q in rows),3);self.assertLessEqual(sum(q['Kind']=='certificate' for q in rows),2);self.assertLessEqual(sum(q['Kind']=='native' for q in rows),6);self.assertTrue(all(q['Complete'] for q in rows));subprocess.check_call([sys.executable,'tools/s3-performance/generate162.py','--check'],cwd=R)
 def test_evidence_manifest_and_all_older_trees(self):
  m=read(D/'evidence-manifest.json')
  for path,h in m['Files'].items():self.assertEqual(hashlib.sha256((R/path).read_bytes()).hexdigest(),h,path)
  for path,h in m['FrozenTrees'].items():self.assertEqual(subprocess.check_output(['git','rev-parse','HEAD:'+path],cwd=R,text=True).strip(),h,path)
if __name__=='__main__':unittest.main()
