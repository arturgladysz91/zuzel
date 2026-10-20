import csv
import gzip
import importlib.util
import json
from pathlib import Path
import unittest

ROOT=Path(__file__).resolve().parents[2]
DATA=ROOT/'docs/experiments/full-contact-yaw-feedforward'
spec=importlib.util.spec_from_file_location('yaw109',ROOT/'tools/shared-world-speedway/full-contact-yaw-report.py')
report=importlib.util.module_from_spec(spec)
spec.loader.exec_module(report)
load=report.load

class FullContactYawAnalysisTests(unittest.TestCase):
    def test_lower_rms_cannot_hide_peak_clearance_or_stability_regression(self):
        a={'RmsError':.45,'MaxError':1.2,'MinimumInner':.18,'MinimumOuter':8,'ConservativeInner':.05,'ConservativeOuter':7.8,
            'TrackingPass':False,'BoundarySafe':True,'Periodic':True}
        for change in ({'MaxError':1.21},{'MinimumInner':.17},{'ConservativeInner':-.01,'BoundarySafe':False},{'Periodic':False}):
            self.assertEqual('FAIL',report.old.classify_outcome(a,dict(a,RmsError=.4,**change)))
        passed=dict(a,RmsError=.4,MaxError=.9,
            RmsHeadingDegrees=2,MaxHeadingDegrees=6,TrackingPass=True)
        self.assertTrue(report.old.tracking_pass(passed))
        self.assertEqual('PASS',report.old.classify_outcome(a,passed))

    def test_frozen_control_bins_and_inverse_source_are_unchanged(self):
        f=load(DATA/'freeze.json')
        for item in f['Sources']:self.assertEqual(item['Sha256'],report.sha(report.SOURCE/item['File']))
        self.assertEqual(f['InverseSourceSha256'],report.sha(ROOT/'tools/shared-world-speedway/CoupledSlipInverse.cs'))
        self.assertEqual(load(DATA/'analysis.json')['FrozenBinsSha256'],report.sha(report.SOURCE/'baseline-bins.json'))
        a=f['ControlParameters'];b=dict(f['FullParameters']);self.assertTrue(b.pop('EnableFullContactYawFeedForward'))
        self.assertEqual(a,b);self.assertTrue(a['EnableCoupledSlipInverse'])

    def test_native_safety_and_energy_are_independent_of_rms(self):
        a=load(DATA/'analysis.json');self.assertEqual(2,a['NewMainWorlds']);self.assertEqual(0,a['ControlMainWorldsRerun'])
        for c in a['Cases']:
            self.assertEqual('FAIL',c['Outcome']);self.assertLess(abs(c['EnergyResidualJoules']),5)
            self.assertEqual(0,c['MaximumYawGuardExcessWatts']);self.assertTrue(c['Periodic'])
            if c['Line']=='wide':
                self.assertGreater(c['MinimumInner'],0);self.assertLess(c['ConservativeInner'],0);self.assertFalse(c['BoundarySafe'])
        for c in a['Mathematics']['Cases']:
            for key in ('MaximumRequestedReplayError','MaximumAuthorityReplayError','MaximumGuardReplayError','AdditionalContactEvaluations','SlipErrorDeltaOnFrozenState'):
                self.assertEqual(0,c[key])
        for line in ('inner','wide'):
            a,b=[c for c in load(DATA/'analysis.json')['Cases'] if c['Line']==line]
            self.assertLess(b['RmsError'],a['RmsError']);self.assertGreater(b['MaxError'],a['MaxError'])
            self.assertLess(b['MinimumInner'],a['MinimumInner'])

    def test_saved_runtime_counts_and_fixed_state_cost_add_no_force_query_or_commit(self):
        for run in load(DATA/'runs.json'):
            with gzip.open(DATA/(run['Name']+'-decisions.csv.gz'),'rt') as f:rows=list(csv.DictReader(f))
            self.assertEqual(run['InverseCalls'],len(rows))
            self.assertEqual(run['InverseContactEvaluations'],sum(int(r['evaluations']) for r in rows))
            work=run['Result']['Summary']['Result']['Work']
            self.assertEqual(len(rows),work['SolverCalls']);self.assertEqual(len(rows),work['RearWheelCommits'])
            for r in rows:
                if int(r['status'])!=0:self.assertEqual(float(r['legacy_beta']),float(r['selected_beta']))
        cost=load(DATA/'force-cost.json');self.assertEqual(0,cost['BodySteps']);self.assertEqual(0,cost['WheelCommits'])
        for s in cost['Samples']:
            self.assertEqual(0,s['AdditionalContactEvaluations']);self.assertTrue(s['TargetInverseUnchanged'])
            self.assertEqual([False,True],[m['Full'] for m in s['Modes']])
            for m in s['Modes']:self.assertEqual(0,m['AllocatedBytes'])

if __name__=='__main__':unittest.main()
