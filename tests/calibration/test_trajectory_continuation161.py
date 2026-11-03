from pathlib import Path
import gzip,hashlib,json,subprocess,unittest,zipfile
R=Path(__file__).resolve().parents[2];D=R/'docs/experiments/trajectory-continuation'
def read(p):
 p=Path(p);return json.loads(gzip.decompress(p.read_bytes()) if p.suffix=='.gz' else p.read_bytes())
class Continuation161Tests(unittest.TestCase):
 @classmethod
 def setUpClass(cls):cls.a=read(D/'analysis.json')
 def test_verdict_requires_five_distinct_complete_faster_pairs(self):
  a=self.a;held=[q for q in a['native'] if q['validation']]
  if a['verdict']=='PASS':self.assertEqual(len(held),5);self.assertEqual(len({q['scenario']['Id'] for q in held}),5);self.assertTrue(all(q['pass_'] and q['pair']['delta_s']<0 for q in held))
  else:self.assertIn(a['verdict'],['PARTIAL','FAIL'])
 def test_historical_audit_runs_no_new_physics(self):
  h=read(D/'historical-forensics.json.gz');self.assertEqual(h['forecasts_run'],0);self.assertEqual(h['native_worlds_created'],0);self.assertEqual(len(h['portfolio']['tapes']),10);self.assertEqual(h['native_end']['Input']['Time'],67);self.assertAlmostEqual(h['first_target_spike']['time'],74.6);self.assertGreater(h['first_target_spike']['target_speed'],38)
 def test_authentic_terminal_state_and_last_review_are_recovered(self):
  q={v['InitialTime']:v for v in self.a['diagnostics']};self.assertTrue(q[67]['Legal']);self.assertTrue(q[66.8]['Legal']);self.assertEqual(q[67]['InitialSpace']['PendingCount'],1);self.assertAlmostEqual(q[67]['SafetyGoal'],988.3362498283386)
 def test_rejected_maneuver_does_not_erase_other_legal_alternatives(self):
  q=next(v for v in self.a['diagnostics'] if abs(v['InitialTime']-66.8)<1e-8);self.assertTrue(any(not x['Legal'] for x in q['Outcomes']));self.assertTrue(q['Legal']);self.assertEqual(len({x['actions_sha256'] for x in q['candidates']}),3)
 def test_full_forecast_includes_unchanged_post_finish_reserve(self):
  q=next(v for v in self.a['diagnostics'] if v['InitialTime']==0);self.assertTrue(q['Legal']);raw=read(R/q['source']);self.assertEqual(raw['Initial']['Input']['Rider']['Speed'],0);self.assertGreaterEqual(raw['Selected']['Points'][-1]['Rider']['TotalReferenceMeters'],q['SafetyGoal']);self.assertGreaterEqual(raw['Selected']['Safety']['SweepLowerBoundMeters'],.5)
 def test_every_probe_requires_full_independent13_9(self):
  self.assertLessEqual(len(self.a['certificates']),2)
  for p in (D/'certificates').glob('*.json.gz'):
   q=read(p)
   if q['Error'] is None:r=q['Review'];self.assertTrue(r['ConditionalCertificate'] and r['Independent']);self.assertEqual(len(r['Tree']),13);self.assertEqual(len(r['Terminals']),9)
 def test_every_native_accepted_action_has_fresh_complete_certificate(self):
  for row in self.a['native']:
   count=0
   for p in sorted((D/'policies'/row['label']).glob('*.json.gz')):
    q=read(p)
    if q['Error'] is None:r=q['Review'];self.assertTrue(r['ConditionalCertificate'] and r['Independent']);self.assertEqual(len(r['Tree']),13);self.assertEqual(len(r['Terminals']),9);self.assertEqual(r['Time'],q['Observed']['Input']['Time']);count+=1
   self.assertEqual(count,row['accepted_decisions'])
 def test_frozen_sources_precede_and_match_all_held_cases(self):
  held=[r for r in self.a['native'] if r['validation']]
  if not held:return
  freeze=read(D/'controller-freeze.json');self.assertEqual(freeze['HeldCount'],5);self.assertTrue(freeze['BeforeHeldResults'])
  for row in held:
   with zipfile.ZipFile(D/'sources'/f"{row['label']}.zip") as z:
    for name,h in freeze['Sources'].items():self.assertEqual(hashlib.sha256(z.read(name)).hexdigest().upper(),h)
 def test_replay_keeps_only_original_native_states_and_stops_old_dnf(self):
  replay=read(D/'native-replay.json.gz');self.assertEqual(replay['runs'][0]['samples'][-1]['t'],67);self.assertEqual(replay['runs'][0]['status'],'DNF')
  for row in replay['runs']:
   raw=read(R/row['source'])['Riders'][0]['Samples'];self.assertEqual(row['samples'][-1]['index'],len(raw)-1);self.assertEqual(hashlib.sha256((R/row['source']).read_bytes()).hexdigest(),row['sha256'])
   for p in row['samples']:
    s=raw[p['index']];v=s['Input']['Rider'];self.assertEqual((p['t'],p['x'],p['y'],p['wheel'],p['crank']),(s['Input']['Time'],v['Position']['X'],v['Position']['Y'],s['Input']['Omega'],s['CrankOmega']))
 def test_budget_is_predeclared_bounded_and_dnf_cannot_be_pass(self):
  plan=read(D/'budget-plan.json');rows=read(D/'budget-journal.json');self.assertTrue(plan['DeclaredBeforeExperiments']);self.assertEqual(plan['MaximumDiagnostics'],6);self.assertEqual(plan['MaximumCertificateProbes'],2);self.assertEqual(plan['MaximumHeldNativeWorlds'],5);self.assertTrue(all(r['Complete'] for r in rows));self.assertLessEqual(sum(r['Kind']=='diagnostic' for r in rows),6);self.assertLessEqual(sum(r['Kind']=='certificate' for r in rows),2);self.assertLessEqual(sum(r['Kind']=='native' for r in rows),6)
  for q in self.a['native']:
   if q['pass_']:self.assertEqual(q['completed_laps'],3);self.assertIsNotNone(q['finish_s']);self.assertGreaterEqual(q['minimum_swept_m'],.5);self.assertLessEqual(q['maximum_beta_deg'],45);self.assertTrue(q['pair']['initial_state_exact'])
 def test_complete_evidence_and_older_trees_match_seal(self):
  seal=read(D/'evidence-manifest.json')
  for path,h in seal['Files'].items():self.assertEqual(hashlib.sha256((R/path).read_bytes()).hexdigest(),h,path)
  for path,h in seal['FrozenTrees'].items():self.assertEqual(subprocess.check_output(['git','rev-parse','HEAD:'+path],cwd=R,text=True).strip(),h,path)
if __name__=='__main__':unittest.main()
