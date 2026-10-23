from extended_source_guard import source130
import copy
import hashlib
import importlib.util
import math
import unittest
from pathlib import Path

ROOT=Path(__file__).resolve().parents[2]
spec=importlib.util.spec_from_file_location('continuous_report',ROOT/'tools/shared-world-speedway/continuous-corners-report.py')
report=importlib.util.module_from_spec(spec);spec.loader.exec_module(report)
DATA=report.DATA

class ContinuousCornerEvidenceTests(unittest.TestCase):
    def test_one_world_identical_initial_and_exact_c_prefix(self):
        a=report.load(DATA,'analysis.json');r=report.load(DATA,'continuous.json');old=report.load(report.OLD,'C.json')
        self.assertEqual(a['NewNativeWorlds'],1);self.assertEqual(a['FullLaps'],0);self.assertEqual(report.load(DATA,'attempt.json')['Worlds'],1)
        self.assertEqual(r['Initial'],old['Initial']);self.assertTrue(a['Prefix']['Equal']);self.assertEqual(a['Prefix']['Actual'],old['Result']['Fingerprint'])
        prefix=report.read(report.OLD,'C');self.assertEqual(report.read(DATA,'continuous')[:len(prefix)],prefix)
        p=report.load(DATA,'protocol.json')['Parameters'].copy();self.assertTrue(p.pop('EnableContinuousCornerPlanning'));self.assertEqual(p,report.load(report.OLD,'protocol.json')['Parameters'])

    def test_continuous_geometry_and_delayed_plan_preserve_pending_gas_and_memory(self):
        a=report.load(DATA,'analysis.json');prepared,activated=a['Changes'];d=report.load(DATA,'diagnostic.json')
        self.assertEqual(d['Worlds'],0);self.assertEqual(d['Commits'],0);self.assertTrue(all(d['Continuity'][k]==0 for k in ['PositionJump','HeadingJump','CurvatureJump']))
        self.assertEqual(prepared['Time'],9.91);self.assertEqual(activated['Time'],10.15);self.assertAlmostEqual(activated['Time']-prepared['Time'],.24,places=9)
        self.assertNotEqual(activated['Memory']['Drive'],1);self.assertNotEqual(activated['Steering'],0);self.assertGreater(abs(activated['Motion']['Beta']),0)
        commands=report.load(DATA,'continuous.json')['Result']['Decisions']
        for change in a['Changes']:
            pending=change['Pending'];c=next(c for c in commands if c['DecisionAt']==pending['DecisionAt'])
            self.assertEqual(c['Drive'],pending['Drive']);self.assertEqual(c['RequestedActivation'],pending['RequestedActivation']);self.assertIsNone(c['CancelledAt'])
            self.assertAlmostEqual(c['ActualActivation'],c['RequestedActivation'],places=9)

    def test_independent_work_and_impulse_balance_for_each_window(self):
        a=report.load(DATA,'analysis.json');run=report.load(DATA,'continuous.json');rows=report.read(DATA,'continuous');final=run['Result']['Riders'][0]
        for label,window in [('whole',None)]+[(k,v['EnergyWindow']) for k,v in a['PerBend'].items()]:
            start=0 if window is None else next(i for i,r in enumerate(rows) if abs(r['t']-window['Start'])<1e-8)
            end=len(rows) if window is None else next((i for i,r in enumerate(rows) if r['t']>=window['End']-1e-8),len(rows))
            sub=copy.deepcopy(run)
            if end<len(rows):sub['Result']['Riders'][0]['Velocity']=dict(X=rows[end]['vx'],Y=rows[end]['vy']);sub['Result']['Riders'][0]['AngularVelocity']=rows[end]['body_omega']
            ledger=report.old.ledger.energy_from_new(rows[start:end],sub);impulse=report.old.physical.impulse_work(rows[start:end],sub)
            self.assertLess(abs(ledger['Residual']-impulse['MeasuredNativeImpulseErrorWorkJoules']),1e-6,label)
        self.assertEqual(run['Energy']['MaximumExcessBeyondEngineJoules'],0)

    def test_contacts_steering_and_reaction_keep_original_limits(self):
        rows=report.read(DATA,'continuous');plans=report.read(DATA,'continuous-plan');run=report.load(DATA,'continuous.json')
        self.assertEqual(len(rows),run['Result']['Work']['SolverCalls']);self.assertEqual(len(rows),run['Result']['Work']['RearWheelCommits'])
        for r,p in zip(rows,plans):
            self.assertLessEqual(abs(r['front_lateral']),p['front_capacity']+1e-3)
            self.assertLessEqual(math.hypot(r['rear_lateral'],r['rear_longitudinal']),p['rear_capacity']+1e-3)
            self.assertLessEqual(abs(r['steer_after']-r['steer_before']),math.pi/2*r['dt']+1e-6)
        for c in run['Result']['Decisions']:self.assertAlmostEqual(c['RequestedActivation']-c['DecisionAt'],.24,places=9)

    def test_both_bends_safe_without_progressive_tracking_or_speed_collapse(self):
        a=report.load(DATA,'analysis.json');self.assertTrue(a['Pass']);first=a['PerBend']['First'];second=a['PerBend']['Second']
        self.assertLess(second['MaximumError'],first['MaximumError']);self.assertLess(second['RmsError'],first['RmsError']);self.assertGreater(second['MinimumSpeedKph'],first['MinimumSpeedKph'])
        for bend in [first,second]:
            self.assertGreater(bend['ConservativeInner'],0);self.assertGreater(bend['ConservativeOuter'],0);self.assertLess(bend['MaximumBetaDegrees'],45);self.assertLess(bend['EndNativeBetaDot'],0)
        self.assertLess(second['Landmarks']['Exit']['SpeedKph'],60) # PASS is not a fast-speedway calibration claim.

    def test_every_historical_byte_and_frozen_execution_source(self):
        a=report.load(DATA,'analysis.json')
        for n,h in a['HistoricalSha256'].items():self.assertEqual(hashlib.sha256((ROOT/n).read_bytes()).hexdigest().upper(),h.upper())
        for n,h in report.load(DATA,'manifest.json').items():self.assertEqual(hashlib.sha256((DATA/n).read_bytes()).hexdigest().upper(),h.upper())
        # Approved default-OFF planning/integration extensions have their own exact
        # source guard plus native OFF fingerprint and all2002-contact replay.
        # Preserve this historical manifest; tyre/engine/#117 stay byte-frozen.
        extensions=report.load(ROOT/'docs/experiments/predictive-rider-control','approved-source-extensions.json')['Sources']
        for n,h in report.load(DATA,'protocol.json')['Sources'].items():
            actual=hashlib.sha256(source130(n).replace(b'\r\n',b'\n')).hexdigest().upper()
            if n in extensions:
                self.assertEqual(extensions[n]['BaseSha256'],h)
                import json
                newer=json.loads((ROOT/'docs/experiments/free-racing/source-extensions.json').read_text(encoding='utf-8'))['Sources'].get(n)
                expected=extensions[n]['ApprovedSha256']
                if newer:self.assertEqual(newer['BaseSha256'],expected);expected=newer['ApprovedSha256']
                newest=json.loads((ROOT/'docs/experiments/free-racing-learning/source-extensions.json').read_bytes())['Sources'].get(n)
                if newest:self.assertEqual(newest['BaseSha256'],expected);expected=newest['ApprovedSha256']
                robust=json.loads((ROOT/'docs/experiments/robust-free-racing-learning/source-extensions.json').read_bytes())['Sources'].get(n)
                if robust:self.assertEqual(robust['BaseSha256'],expected);expected=robust['ApprovedSha256']
                self.assertEqual(actual,expected)
            else:
                import json
                newer=json.loads((ROOT/'docs/experiments/free-racing/source-extensions.json').read_text(encoding='utf-8'))['Sources'].get(n)
                expected=h
                if newer:self.assertEqual(newer['BaseSha256'],expected);expected=newer['ApprovedSha256']
                newest=json.loads((ROOT/'docs/experiments/free-racing-learning/source-extensions.json').read_bytes())['Sources'].get(n)
                if newest:self.assertEqual(newest['BaseSha256'],expected);expected=newest['ApprovedSha256']
                robust=json.loads((ROOT/'docs/experiments/robust-free-racing-learning/source-extensions.json').read_bytes())['Sources'].get(n)
                if robust:self.assertEqual(robust['BaseSha256'],expected);expected=robust['ApprovedSha256']
                self.assertEqual(actual,expected)

if __name__=='__main__':unittest.main()
