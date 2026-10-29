"""Immutable saved evidence checks; no native worlds or planner evaluations."""
from pathlib import Path
import gzip,hashlib,json,subprocess,unittest
R=Path(__file__).resolve().parents[2];D=R/'docs/experiments/efficient-contingency'
def read(p):return json.loads(gzip.decompress(p.read_bytes()) if p.suffix=='.gz' else p.read_bytes())
class Efficient152Tests(unittest.TestCase):
 def test_exact_prefix_checks_commands_states_queues_and_full_forecast_certificates(self):
  x=read(D/'analysis.json')['prefix'];self.assertEqual(x['accepted_commands'],173);self.assertTrue(x['accepted_commands_all_bits_equal']);self.assertTrue(x['native_samples_all_fields_equal']);self.assertTrue(x['observations_queues_and_activations_equal']);self.assertLessEqual(x['new_controller_cpu_s'],x['old_controller_cpu_s']);self.assertAlmostEqual(x['end_s'],34.6)
 def test_every_accepted_decision_has_independent_complete_finite_tree(self):
  for audit in read(D/'analysis.json')['plan_audits'].values():
   self.assertEqual(audit['reviews'],audit['native_decisions']);self.assertTrue(audit['queues_match']);self.assertEqual(sum(audit['exclusive_phases'].values()),sum(r['cpu_s'] for r in audit['counters']))
   for r in audit['rows']:self.assertEqual((r['nodes'],r['leaves']),(13,9));self.assertTrue(r['forecast_legal'])
 def test_budget_and_negative_results_are_preserved(self):
  rows=read(D/'budget.json');self.assertTrue(all(r['Complete'] for r in rows));self.assertLessEqual(sum(r['CpuSeconds'] for r in rows),1200);self.assertLessEqual(sum(r['NativeWorlds'] for r in rows),15);self.assertTrue(any('failed' in r.get('Outcome','') for r in rows));self.assertTrue((D/'bench-cold.json').exists());self.assertTrue((D/'bench-project.json').exists())
 def test_shared_policy_inputs_preserve_all_reserves_and_rejected_trees(self):
  for path in (D/'policies/efficient152-A-v1').glob('decision-*.json.gz'):
   x=read(path);self.assertTrue(x['Independent']);self.assertTrue(x['ConditionalCertificate']);self.assertEqual(x['Format'],'PolicyDag152-v1');self.assertEqual(len(x['Terminals']),9);self.assertEqual(len(x['PhysicalHash']),64)
   for t in x['Terminals']:
    tape=x['Tapes'][t['Tape']];self.assertTrue(tape['Safety']['Legal']);self.assertGreater(len(tape['Actions']),0);self.assertEqual(tape['Start']['Input']['Time'],tape['CreatedAt'])
 def test_frozen_sources_and_native_inputs_have_unchanged_hashes(self):
  p=read(D/'source-proof.json');self.assertEqual(p['Base'],'5558a30c8864199fb1c7abc18852ca27f4d29d9b')
  for path,digest in p['FrozenHashes'].items():self.assertEqual(hashlib.sha256((R/path).read_bytes()).hexdigest(),digest)
  for path,tree in p['FrozenHistoricalGitTrees'].items():self.assertEqual(subprocess.check_output(['git','-c','safe.directory='+R.as_posix(),'rev-parse','HEAD:'+path],cwd=R,text=True).strip(),tree)
  for row in read(D/'analysis.json')['source_files']:self.assertEqual(hashlib.sha256((R/row['path']).read_bytes()).hexdigest(),row['sha256'])
 def test_completed_means_three_certified_native_laps(self):
  for r in read(D/'analysis.json')['full_runs']:
   if r['full_autonomous'] and r['outcome']=='completed':self.assertEqual(r['laps'],3);self.assertIsNotNone(r['finish_s']);self.assertGreaterEqual(r['minimum_sweep_m'],0);self.assertLessEqual(r['maximum_beta_deg'],45);self.assertEqual(r['contacts'],0)
 def test_selected_algorithm_and_its_actual_sources_match_the_completed_run(self):
  result=read(D/'native/efficient152-A-portfolio-v1-result.json');self.assertTrue(result['Gate4']);self.assertEqual(result['CompletedLaps'],3);self.assertFalse(result['Counts']['Search']['ShortlistK1']);self.assertTrue(result['Counts']['options']['ShareCurrentPolicies'])
  hashes=read(D/'sources/efficient152-A-portfolio-v1.json')
  for name in ['Planner152.Generated.cs','Runtime152.cs','ExactRuntimeA152.Generated.cs','ExactRuntimeB152.Generated.cs','PredictorA152.Generated.cs','PredictorB152.Generated.cs','ContactA152.Generated.cs','ContactB152.Generated.cs','Feedback152.Generated.cs','PreparedWheel152.cs']:
   self.assertEqual(hashlib.sha256((R/'tools/efficient-contingency'/name).read_bytes()).hexdigest().upper(),hashes[name])
 def test_algorithmic_counterexample_has_a_complete_fresh_certificate(self):
  rejected=read(D/'policies/efficient152-A-v1/abstain.json.gz')['Review'];repaired=read(D/'repair38.6-v7.json.gz')['Review'];self.assertEqual(rejected['Time'],repaired['Time']);self.assertEqual(rejected['Observed'],repaired['Observed']);self.assertFalse(rejected['ConditionalCertificate']);self.assertTrue(repaired['ConditionalCertificate']);self.assertTrue(repaired['Independent']);self.assertEqual(len(repaired['Terminals']),9)
if __name__=='__main__':unittest.main()
