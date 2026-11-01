from pathlib import Path
import copy,gzip,hashlib,importlib.util,json,subprocess,unittest
R=Path(__file__).resolve().parents[2];D=R/'docs/experiments/robust-three-lap'
s=importlib.util.spec_from_file_location('study158',R/'tools/robust-three-lap/analyze158.py');m=importlib.util.module_from_spec(s);s.loader.exec_module(m)

class RobustThreeLap158Tests(unittest.TestCase):
 def test_duplicate_or_missing_conditions_cannot_pass_the_series(self):
  manifest=m.read(D/'final-v6/scenarios-frozen.json');rows=[dict(scenario=s,pass_=True,finish_s=100) for s in manifest['Scenarios']]
  self.assertTrue(m.series(rows,manifest)['all_pass'])
  self.assertFalse(m.series(rows[:-1],manifest)['all_pass'])
  duplicate=copy.deepcopy(rows);duplicate[-1]=copy.deepcopy(duplicate[0]);self.assertFalse(m.series(duplicate,manifest)['all_pass'])
  rows[-1].update(pass_=False,finish_s=None);self.assertFalse(m.series(rows,manifest)['all_pass']);self.assertEqual(m.series(rows,manifest)['dnf'],1)
  rows[-1]['safety_violation']=True;self.assertEqual(m.series(rows,manifest)['status'],'FAIL')
 def test_full_native_evidence_and_every_failure_remain_visible(self):
  a=m.read(D/'analysis.json');raw=list(D.rglob('native/*.json.gz'));self.assertEqual(len(raw),len(a['trials']))
  for r in a['trials']:
   if r['pass_']:
    self.assertEqual(r['completed_laps'],3);self.assertTrue(r['stopped_start']);self.assertIsNotNone(r['finish_s']);self.assertGreaterEqual(r['minimum_sweep_m'],.5);self.assertGreaterEqual(r['minimum_pose_m'],.5);self.assertEqual(r['abstain'],0);self.assertEqual(r['accepted_decisions'],r['certified_decisions'])
   if not r['complete']:self.assertFalse(r['pass_']);self.assertIsNone(r['finish_s'])
  self.assertTrue(any(r['collector_recovery'] for r in a['trials']));self.assertTrue(any(not r['pass_'] for r in a['trials']))
 def test_all_six_bends_and_lap_times_are_required(self):
  a=m.read(D/'analysis.json')
  for r in a['trials']:
   bends=[b for b in a['bends'] if b['trial']==r['label']];self.assertEqual(len(bends),6)
   if r['pass_']:
    self.assertTrue(all(b['status']=='complete' for b in bends));self.assertAlmostEqual(sum(r['lap_times_s']),r['finish_s'],places=8)
 def test_pairing_and_no_fabricated_time_gain_for_dnf_baseline(self):
  for p in m.read(D/'analysis.json')['pairs']:
   self.assertTrue(p['identical_initial'])
   if not p['base_pass'] or not p['new_pass']:self.assertIsNone(p['time_gain_s'])
 def test_all_final_start_offsets_fit_their_declared_legal_fields(self):
  # Original12m straight,1m inner-reference origin, stopped heading0.
  # Maximum lateral physical half-width is the original0.4m handlebar.
  for c in m.read(D/'final-v6/scenarios-frozen.json')['Scenarios']:
   physical=c['OffsetMeters']+1;lower=3*'ABCD'.index(c['Gate'])
   self.assertGreaterEqual(physical-.4,lower);self.assertLessEqual(physical+.4,lower+3)
 def test_frozen_sources_and_no_unseen_development(self):
  final=D/'final-v6';f=m.read(final/'controller-freeze.json');self.assertFalse(f['HeldOutcomesRead']);self.assertEqual(f['RequiredTrials'],10)
  for name,h in f['Sources'].items():self.assertEqual(hashlib.sha256((R/'tools/robust-three-lap'/name).read_bytes()).hexdigest().upper(),h,name)
  first=m.read(R/f['FirstCompleteNative']);self.assertTrue(first['Pass']);self.assertFalse(first['Validation']);self.assertEqual(first['Fingerprint'],f['FirstNativeFingerprint'])
  for p in (final/'native').glob('*-result.json'):
   q=m.read(p)
   if not q['Validation']:self.assertFalse(q['Scenario']['Held'])
 def test_protocol_and_physics_remain_exact(self):
  p=subprocess.run(['node','tools/robust-three-lap/generate158.mjs','--check'],cwd=R,capture_output=True,text=True);self.assertEqual(p.returncode,0,p.stdout+p.stderr)
  for path,tree in m.read(D/'source-proof.json')['FrozenTrees'].items():self.assertEqual(subprocess.check_output(['git','rev-parse','HEAD:'+path],cwd=R,text=True).strip(),tree,path)
 def test_every_native_world_and_cost_is_accounted(self):
  ledgers=list(D.rglob('budget.json'));worlds=0
  for p in ledgers:
   rows=m.read(p);self.assertTrue(all(r['Complete'] and r['CpuSeconds']>0 for r in rows));self.assertEqual(len(rows),len({r['Name'] for r in rows}));worlds+=sum(r['NativeWorlds'] for r in rows)
  self.assertEqual(worlds,len(list(D.rglob('native/*.json.gz'))))
 def test_final_status_matches_all_predeclared_trials(self):
  a=m.read(D/'analysis.json');rows=[r for r in a['trials'] if r['phase']=='final-v6' and r['validation'] and not r['baseline']];actual=m.series(rows,m.read(D/'final-v6/scenarios-frozen.json'));published=next(g for g in a['groups'] if g['phase']=='final-v6' and not g['baseline']);self.assertEqual(actual['all_pass'],published['all_pass']);self.assertEqual(actual['dnf'],published['dnf']);self.assertEqual(len(rows),10)
 def test_sealed_native_and_policy_evidence_is_unchanged(self):
  seal=m.read(D/'evidence-manifest.json');self.assertTrue(seal['AllReservationsClosed']);self.assertEqual(seal['FinalNewTrials'],10);self.assertEqual(seal['FinalBaseTrials'],10)
  for name,h in seal['Files'].items():
   with (R/name).open('rb') as f:self.assertEqual(hashlib.file_digest(f,'sha256').hexdigest(),h,name)
 def test_only_canonical_prefix_variant_has_full_bit_agreement(self):
  canonical=m.read(D/'prefix-extraction-canonical-v2.json.gz');rejected=m.read(D/'prefix-extraction-v1.json.gz');self.assertEqual(len(canonical),72);self.assertTrue(all(r['Equal'] for r in canonical));self.assertEqual(sum(not r['Equal'] for r in rejected),4)
if __name__=='__main__':unittest.main()
