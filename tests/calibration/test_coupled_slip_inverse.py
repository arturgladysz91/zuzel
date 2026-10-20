import gzip
import csv
import hashlib
import importlib.util
import json
from pathlib import Path
import unittest

ROOT=Path(__file__).resolve().parents[2]
DATA=ROOT/'docs/experiments/coupled-slip-inverse'
spec=importlib.util.spec_from_file_location('coupled_report',ROOT/'tools/shared-world-speedway/coupled-slip-inverse-report.py')
report=importlib.util.module_from_spec(spec)
spec.loader.exec_module(report)
load=lambda p:json.loads(p.read_text(encoding='utf-8-sig'))

class CoupledSlipInverseTests(unittest.TestCase):
    def test_rms_improvement_cannot_erase_peak_or_between_step_regression(self):
        a={'RmsError':.45,'MaxError':1.2,'MinimumInner':.18,'MinimumOuter':8,'ConservativeInner':.05,'ConservativeOuter':7.8,
            'TrackingPass':False,'BoundarySafe':True,'Periodic':True}
        self.assertEqual('FAIL',report.classify_outcome(a,dict(a,RmsError=.4,MaxError=1.21)))
        self.assertEqual('FAIL',report.classify_outcome(a,dict(a,RmsError=.4,ConservativeInner=-.01,BoundarySafe=False)))
        self.assertEqual('FAIL',report.classify_outcome(a,dict(a,RmsError=.4,Periodic=False)))

    def test_frozen_groups_and_original_sources_are_unchanged(self):
        frozen=load(DATA/'baseline-bins.json')
        for name,digest in frozen['Sources'].items():self.assertEqual(digest,report.sha(DATA/name))
        for item in load(DATA/'freeze.json')['Sources']:
            self.assertEqual(item['Sha256'],report.sha(report.SOURCE/item['File']))
        for run in load(DATA/'runs.json'):self.assertEqual(run['BaselineBinsSha256'],report.sha(DATA/'baseline-bins.json'))
        self.assertTrue(any(v['Group']==3 for line in frozen['Cases'].values() for v in line.values()))

    def test_native_evidence_keeps_safety_energy_and_force_verification_separate(self):
        a=load(DATA/'analysis.json');self.assertEqual(2,a['NewMainWorlds']);self.assertEqual(0,a['LegacyWorldsRerun'])
        for case in a['Cases']:
            self.assertLess(abs(case['EnergyResidualJoules']),5);self.assertEqual(0,case['ForceReplayError'])
            if case['Mode']=='coupled':self.assertEqual('FAIL',case['Outcome'])
            if case['Line']=='wide':
                self.assertGreater(case['MinimumInner'],0);self.assertLess(case['ConservativeInner'],0)
                self.assertFalse(case['BoundarySafe'])
        for case in load(DATA/'native-force-validation.json')['Cases']:self.assertEqual(0,case['MaximumForceReplayErrorNewtons'])

    def test_runtime_inverse_cost_and_fallback_labels_match_saved_steps(self):
        for run in load(DATA/'runs.json'):
            name=run['Name']
            with gzip.open(DATA/(name+'-decisions.csv.gz'),'rt') as f:decisions=list(csv.DictReader(f))
            self.assertEqual(run['ActualInverseCalls'],len(decisions))
            self.assertEqual(run['ContactEvaluations'],sum(int(row['evaluations']) for row in decisions))
            with gzip.open(DATA/(name+'-inverse.csv.gz'),'rt') as f:classified=list(csv.DictReader(f))
            self.assertEqual(len(decisions),len(classified))
            for x,y in zip(decisions,classified):
                self.assertLessEqual(abs(float(x['t'])-float(y['t'])),1e-12)
                self.assertEqual(int(x['status']),int(y['status']))
            for row in decisions:
                if int(row['status'])!=0:self.assertEqual(float(row['legacy_beta']),float(row['selected_beta']))
        for sample in load(DATA/'force-cost.json')['Samples']:
            for mode in sample['Modes']:self.assertEqual(0,mode['AllocatedBytes'])

if __name__=='__main__':unittest.main()
