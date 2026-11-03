from pathlib import Path
import hashlib, importlib.util, json, subprocess, sys, unittest

R=Path(__file__).resolve().parents[2]
D=R/'docs/experiments/instrumentation-cost'
spec=importlib.util.spec_from_file_location('analyze163',R/'tools/instrumentation-cost/analyze163.py')
analysis=importlib.util.module_from_spec(spec);spec.loader.exec_module(analysis)

class InstrumentationCost163Tests(unittest.TestCase):
 def test_archived_hashes_certificates_work_and_guard_counts_match_all_modes(self):
  a=analysis.summarize();self.assertEqual(a['ActualFullReviews'],18)
  for c in a['Cases']:
   self.assertTrue(c['ExactArchived161']);self.assertTrue(c['IdenticalCountersAndGuardCalls'])
   self.assertEqual(c['Context']['PreviousArchivedHash'],c['Context']['PreviousReconstructedHash'])
   self.assertGreater(c['PhysicalCounters']['IndependentForecasts'],0)
 def test_failed_repeatability_gate_prohibits_all_native_starts(self):
  a=analysis.summarize();gate=analysis.read(D/'native-admission.json')
  self.assertFalse(a['NativeGatePassed']);self.assertFalse(gate['GatePassed']);self.assertEqual(gate['NativeWorldsStarted'],0)
  self.assertFalse(any(c['GatePassed'] for c in a['Cases']))
 def test_predeclared_order_and_three_completed_budget_slots(self):
  p=analysis.read(D/'budget-plan.json');self.assertEqual(p['MaximumMicrobenchmarks'],3)
  self.assertEqual(len(list((D/'micro').glob('*.gz'))),3);self.assertFalse((D/'reservation.json').exists())
  for c in p['Cases']:
   d=analysis.read(D/'micro'/(c['Label']+'.json.gz'))
   self.assertEqual([t['Mode'] for t in d['Trials']],c['Order']);self.assertIsNone(d['Error'])
   self.assertLess(analysis.read(D/('cost-'+c['Label']+'.json'))['CpuSeconds'],p['CpuCapPerMicrobenchmarkSeconds'])
 def test_read_only_analysis_reproduces_recorded_metrics(self):
  self.assertEqual(analysis.summarize(),analysis.read(D/'analysis.json'))
 def test_generated_planner_is_only_permitted_measurement_transformation(self):
  subprocess.check_call([sys.executable,'tools/instrumentation-cost/generate163.py','--check'],cwd=R)
  source=(R/'tools/instrumentation-cost/Planner163.Generated.cs').read_text(encoding='utf-8')
  self.assertNotIn('ResponseCache162',source);self.assertIn('using var measuredContinuation=profile.Continuation(phase);',source)
 def test_clock_and_scope_controls_are_separate_from_real_review_metrics(self):
  a=analysis.summarize()
  for c in a['Calibration']:
   self.assertGreater(c['CpuMicrosecondsPerRead'],0);self.assertGreater(c['CpuMicrosecondsPerScope'],c['CpuMicrosecondsPerRead'])
  for c in a['Cases']:
   self.assertLess(c['Modes']['Aggregate']['Profiler']['ClockReads'],c['Modes']['Fine']['Profiler']['ClockReads'])
   self.assertEqual(c['Modes']['Disabled']['Profiler']['ScopeAllocations'],0)
 def test_frozen_sources_and_evidence_integrity(self):
  m=analysis.read(D/'evidence-manifest.json')
  for p,h in m['Files'].items():self.assertEqual(hashlib.sha256((R/p).read_bytes()).hexdigest(),h,p)
  for p,h in m['FrozenTrees'].items():self.assertEqual(subprocess.check_output(['git','rev-parse','HEAD:'+p],cwd=R,text=True).strip(),h,p)

if __name__=='__main__':unittest.main()
