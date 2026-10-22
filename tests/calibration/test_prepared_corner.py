import hashlib
import importlib.util
import json
import unittest
from pathlib import Path

ROOT=Path(__file__).resolve().parents[2]
spec=importlib.util.spec_from_file_location('prepared_report',ROOT/'tools/shared-world-speedway/prepared-corner-report.py')
report=importlib.util.module_from_spec(spec);spec.loader.exec_module(report)
DATA=report.DATA

class PreparedCornerEvidenceTests(unittest.TestCase):
    def test_exact_initial_state_and_unchanged_physics_flags(self):
        old=report.load(report.OLD,'combined.json')['Initial']
        parameters=report.load(DATA,'protocol.json')['Parameters'].copy()
        self.assertTrue(parameters.pop('EnablePreparedCornerPlan'))
        self.assertEqual(parameters,report.load(report.OLD,'protocol.json')['Parameters'])
        for name in ['A','B','C']:
            r=report.load(DATA,name+'.json');self.assertEqual(r['Initial'],old)
            self.assertEqual(r['LegacyYawCalls'],0)
            self.assertEqual(r['Result']['Work']['RearWheelCommits'],r['Result']['Work']['SolverCalls'])

    def test_independent_energy_and_original_contact_bounds(self):
        for name in ['A','B','C']:
            r,cache=report.metrics(DATA,name)
            self.assertLess(abs(r['UnexplainedEnergy']),1e-6)
            self.assertLess(r['FrontPeakUse'],1+1e-6);self.assertLess(r['RearPeakUse'],1+1e-6)
            self.assertEqual(r['PlannerQueries'],13*r['PlannerReviews'])

    def test_c_real_exit_is_safe_but_quality_targets_remain_unmet(self):
        analysis=report.load(DATA,'analysis.json');r=analysis['Results']
        self.assertEqual(analysis['NativeTrials'],3);self.assertEqual(analysis['NewLaps'],0)
        self.assertFalse(r['A']['Pass']);self.assertFalse(r['B']['Pass']);self.assertTrue(r['C']['Pass'])
        self.assertGreater(r['C']['Inner'],0);self.assertGreater(r['C']['Outer'],.5)
        self.assertGreater(r['C']['MaximumErrorToPlan'],3);self.assertLess(r['C']['Landmarks']['Exit']['SpeedKph'],60)
        self.assertLess(r['C']['MaximumErrorToPlan'],r['#119']['MaximumErrorToPlan'])
        self.assertGreater(analysis['ExitGainKph'],10);self.assertEqual(analysis['Grade'],'B')
        replay=report.load(DATA,'replay.json');self.assertTrue(next(x for x in replay if x['Name']=='C')['RealExit'])
        self.assertTrue(all(x['MaximumForceOrSteerDifference']==0 and x['ChangedReviews']==0 for x in replay))

    def test_delayed_commands_and_a_fixed_throttle(self):
        for name in ['A','B','C']:
            r=report.load(DATA,name+'.json')
            for c in r['Result']['Decisions']:
                self.assertAlmostEqual(c['RequestedActivation']-c['DecisionAt'],.24,places=9)
            rows=report.read(DATA,name)
            self.assertTrue(all(row['drive']==1 for row in rows if row['t']<.24-1e-8))
            if name=='A':self.assertTrue(all(abs(row['drive']-.15)<1e-8 for row in rows if row['t']>=.24-1e-8))
        self.assertTrue(report.load(DATA,'geometry.json')['InitialReview']['NoCertifiedCoastSolution'])

    def test_history_manifest_and_unchanged_contact_sources(self):
        a=report.load(DATA,'analysis.json')
        for n,h in a['HistoricalSha256'].items():self.assertEqual(hashlib.sha256((ROOT/n).read_bytes()).hexdigest().upper(),h.upper())
        for n,h in report.load(DATA,'manifest.json').items():self.assertEqual(hashlib.sha256((DATA/n).read_bytes()).hexdigest().upper(),h.upper())
        # #123 is explicitly allowed to evolve planning/integration. Keep the
        # original snapshot in its manifest; contacts/engine/#117 remain frozen.
        protected={'Engine500.cs','LoadBasedTyreGrip.cs','RearWheelDynamics.cs','TwoContactBikeDynamics.cs','TravelDirectionSteering.cs'}
        for n,h in report.load(DATA,'source-canonical.json').items():
            if n in protected:self.assertEqual(hashlib.sha256((ROOT/'tools/shared-world-speedway'/n).read_bytes().replace(b'\r\n',b'\n')).hexdigest().upper(),h)

if __name__=='__main__':unittest.main()
