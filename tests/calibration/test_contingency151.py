"""Finite recourse, delayed queues, actual native scope and corrected same-time model errors."""
import gzip,hashlib,json,unittest
from pathlib import Path
R=Path(__file__).resolve().parents[2];D=R/'docs/experiments/contingency-recourse'
def read(p):return json.loads(gzip.decompress(p.read_bytes()) if p.name.endswith('.gz') else p.read_bytes())
class Study151Tests(unittest.TestCase):
 @classmethod
 def setUpClass(cls):cls.a=read(D/'analysis.json')
 def test_root_is_shared_but_future_observations_can_have_different_commands(self):
  x=read(D/'gate1-v4.json.gz')['Review'];self.assertTrue(x['ConditionalCertificate']);self.assertTrue(x['Independent']);self.assertEqual(len(x['Tree']),13);self.assertEqual(len(x['Terminals']),9)
  children=[n for n in x['Tree'] if n['Parent']==1];self.assertEqual({n['Sign'] for n in children},{-1,0,1});self.assertGreater(len({tuple(n['Action'].values()) for n in children}),1)
  self.assertEqual(x['Tree'][0]['Observed']['Input']['Time'],.4);self.assertAlmostEqual(x['IrrevocableUntil'],.84)
 def test_every_accepted_native_decision_has_a_complete_audited_tree(self):
  for name,a in self.a['plan_audits'].items():
   self.assertEqual(a['reviews'],a['native_decisions']);self.assertEqual(a['certified_nodes'],13*a['reviews']);self.assertEqual(a['certified_terminal_reserves'],9*a['reviews']);self.assertTrue(a['queues_match'])
   for row in a['rows']:
    self.assertTrue(row['forecast_legal']);self.assertEqual(row['actual_action'],row['selected'][0]);activation=row['actual_activation']
    if activation:self.assertEqual(activation['Action'],row['actual_action']);self.assertAlmostEqual(activation['ActivationAt']-row['time_s'],.24)
 def test_local_gates_do_not_become_three_lap_finishes(self):
  for run in self.a['full_runs']:
   if run['gate'] in (2,3):self.assertLess(run['laps'],3);self.assertFalse(run['finish_s'] is not None and run['laps']==3)
   if run['native'] and run['laps']==3:self.assertEqual(run['outcome'],'completed');self.assertIsNotNone(run['finish_s']);self.assertGreaterEqual(run['minimum_sweep_m'],0);self.assertLessEqual(run['maximum_beta_deg'],45)
 def test_old_rejections_are_remote_and_not_an_immediate_collision(self):
  audit=read(D/'rejection-audit.json');self.assertEqual(len(audit),12)
  for row in audit:
   if row['Sign']==0:self.assertTrue(row['Safety']['Legal']);self.assertIsNone(row['FirstFailure'])
   else:self.assertGreater(row['FirstFailure']['Time'],11)
 def test_identical_command_error_is_small_and_decimation_was_not_model_error(self):
  rows=self.a['error_summary'];short=[r for r in rows if r['horizon_s']==.2]
  self.assertLess(max(r['PositionMeters'] for r in short),.0001);self.assertGreater(max(r['trace_position_m'] for r in short),.25);self.assertGreater(max(r['trace_time_error_s'] for r in short),.009)
  self.assertGreater(max(r['PositionMeters'] for r in rows if r['horizon_s']==10),4)
  for run in read(D/'identical-command-errors-v2.json'):
   for row in run['Rows']:self.assertTrue(row['Delta']['QueueActionsExact']);self.assertTrue(row['Delta']['QueueCausallyEquivalent']);self.assertTrue(row['Delta']['ActiveExact']);self.assertEqual(row['Delta']['SteeringRadians'],0)
 def test_axis_invariant_has_native_support_and_is_explicitly_conditional(self):
  x=read(D/'axis-invariant-audit.json');self.assertEqual(sum(r['EligibleAcceptedPairs'] for r in x),39);self.assertTrue(all(not r['Violations'] for r in x))
  source=(R/'tools/contingency-recourse/Planner151.cs').read_text();self.assertIn('i.Pending.All(c => c.Action.SteeringRadians == 0)',source);self.assertIn('NOT a global bound',source)
 def test_exclusive_cpu_partition_and_failed_worlds_remain_visible(self):
  ledger=read(D/'budget.json');self.assertTrue(all(x['Complete'] for x in ledger));self.assertLessEqual(sum(x['NativeWorlds'] for x in ledger),25);self.assertLessEqual(sum(x['CpuSeconds'] for x in ledger),1200);self.assertTrue(any('ABSTAIN' in x['Outcome'] for x in ledger))
  for row in self.a['cpu'].values():self.assertAlmostEqual(sum(row['exclusive_phases'].values()),row['profile_cpu_s']);self.assertGreaterEqual(row['attempted_controller_cpu_s'],row['profile_cpu_s'])
 def test_original_physics_and_study150_remain_immutable(self):
  proof=read(D/'source-proof.json');self.assertEqual(proof['Base'],'5bc8575f60d9fa1116e45dad85c08786158faccd')
  for path,digest in proof['FrozenHashes'].items():self.assertEqual(hashlib.sha256((R/path).read_bytes().replace(b'\r\n',b'\n') if path.endswith('.cs') else (R/path).read_bytes()).hexdigest(),digest)
if __name__=='__main__':unittest.main()
