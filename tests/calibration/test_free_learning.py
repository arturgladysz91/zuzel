"""Audit saved native evidence; no simulation or training is run in CI."""
import gzip
import hashlib
import json
import math
from pathlib import Path
import unittest

ROOT=Path(__file__).resolve().parents[2]
DATA=ROOT/'docs/experiments/free-racing-learning'
def load(name):
    b=(DATA/name).read_bytes()
    return json.loads(gzip.decompress(b) if name.endswith('.gz') else b)
def sha(b):return hashlib.sha256(b).hexdigest().upper()

class FreeLearningEvidenceTests(unittest.TestCase):
    def test_native_world_budget_counts_failures_and_diagnostic_retries(self):
        ledger=load('budget.json');a=load('analysis.json');counts={c:sum(e['Category']==c for e in ledger) for c in ['training','validation','physics']}
        self.assertEqual(counts['training'],a['TrainingEpisodes']);self.assertGreaterEqual(counts['training'],200)
        self.assertLessEqual(counts['training'],1000);self.assertLessEqual(counts['validation'],36);self.assertEqual(counts['physics'],7)
        self.assertEqual(len(ledger),a['TotalNativeWorlds']);self.assertEqual(len({e['Name'] for e in ledger}),len(ledger))
        self.assertLess(a['CpuSeconds'],7200);self.assertLess(a['ContactEvaluations'],160000000)
        self.assertEqual(sum(not e['Complete'] for e in ledger),1) # retained partial export, native work recovered
        names={e['Name'] for e in ledger}
        self.assertIn('g005-p0025-bend-14-6',names);self.assertIn('g005-p0025-bend-14-6-rate-retry',names)
        self.assertIn('initial-holdout-11-7-export-retry',names)

    def test_five_same_state_physical_pulses_confirm_sign_and_mirror_symmetry(self):
        p=load('physics.json');self.assertTrue(p['Passed']);self.assertGreater(p['LeftBendHeadingChange'],0)
        records={r['AngleDegrees']:r for r in p['Records']};self.assertEqual(set(records),{0,5,-5,15,-15})
        for r in records.values():self.assertEqual(r['Initial'],records[0]['Initial'])
        self.assertEqual(records[0]['DeltaY'],0)
        for angle in [5,15]:
            pos,neg=records[angle],records[-angle]
            for key in ['DeltaPsi','DeltaChi','DeltaBeta','DeltaY','EndSteering']:
                self.assertGreater(pos[key],0);self.assertAlmostEqual(pos[key],-neg[key],places=6)
            self.assertAlmostEqual(pos['DeltaX'],neg['DeltaX'],places=6)
            self.assertNotAlmostEqual(pos['DeltaPsi'],pos['DeltaChi'],places=3)

    def test_checkpoints_use_frozen_validation_not_policy_selection(self):
        p=load('protocol.json');self.assertEqual(p['BaseHead'],'e23261afe1205c71a8db3733b6553485f4de29f5');self.assertEqual(p['Seed'],1292026)
        train={(s['Speed'],s['Offset'],s['Start']) for level in p['Curriculum'] for s in level}
        held={(s['Speed'],s['Offset'],s['Start']) for s in p['Validation']};self.assertFalse(train&held)
        for count in [50,100,200]:
            checkpoint=load(f'assessment-{count}.json');self.assertEqual(checkpoint['TrainingEpisodes'],count);self.assertFalse(checkpoint['UseValidationForSelection'])
        self.assertTrue(load('extension-200.json')['Extend']);self.assertFalse(load('analysis.json')['ParameterSelectionUsedValidation'])

    def test_failed_candidates_influence_cem_and_diversity_is_retained(self):
        gs=load('generations.json');p=load('protocol.json')
        # Generation 2 has only one completion, so no policy completes both cases.
        g=gs[2];self.assertEqual(g['Level'],2);self.assertEqual(g['Completed'],1)
        self.assertFalse(any(all(r['Completed'] for r in c['Results']) for c in g['Candidates']))
        def key(c):
            rs=c['Results'];n=sum(r['Completed'] for r in rs)
            return (-n,max(r['GateSeconds'] for r in rs) if n==2 else math.inf,-min(r['LegalProgress'] for r in rs),-sum(r['LegalProgress'] for r in rs)/2,
                sum(r['BoundaryApproach'] for r in rs)/max(1,sum(r['LegalProgress'] for r in rs)),-sum(r['RecoverySeconds'] for r in rs)/max(1,sum(r['LegalProgress'] for r in rs)),c['Id'])
        elite=sorted(g['Candidates'],key=key)[:2]
        for i,name in enumerate(p['Names']):
            expected=.4*gs[1]['Mean'][i]+.6*sum(c['Policy'][name] for c in elite)/2
            self.assertAlmostEqual(g['Mean'][i],expected)
        self.assertNotEqual(g['Mean'],gs[1]['Mean'])
        for generation in gs:
            self.assertEqual(len(generation['Candidates']),5)
            for i,std in enumerate(generation['Std']):self.assertGreaterEqual(std,(p['Upper'][i]-p['Lower'][i])*.15-1e-12)
            for c in generation['Candidates']:
                for i,name in enumerate(p['Names']):self.assertTrue(p['Lower'][i]<=c['Policy'][name]<=p['Upper'][i])
                for r in c['Results']:
                    self.assertEqual(r['GateSeconds'] is not None,r['Completed'])
                    self.assertLessEqual(r['LegalSeconds'],r['DurationSeconds'])

    def test_milestones_are_actual_causal_actuated_records_without_a_target(self):
        index=load('index.json');self.assertEqual(index['MissingMilestones'],[])
        self.assertEqual(set(index['Milestones']),{'overlay-initial','overlay-first-improvement','overlay-first-safe-bend','overlay-after-100','overlay-after-200','overlay-final'})
        for name in index['Milestones']:
            run=load(name+'.json.gz');samples=run['Samples'];self.assertEqual(run['Scenario'],load('protocol.json')['Validation'][0])
            self.assertAlmostEqual(samples[-1]['Input']['Time'],run['Metrics']['DurationSeconds'])
            for s in samples:
                x=s['Input'];v=x['Rider'];self.assertTrue(all(math.isfinite(v[k]) for k in ['Speed','Heading','AngularVelocity']))
                self.assertLessEqual(abs(x['Steering']),math.radians(35)+1e-7)
                for c in x['Pending']:
                    self.assertLessEqual(c['ObservedAt'],x['Time']+1e-8);self.assertGreaterEqual(c['ActivationAt'],x['Time']-1e-8)
                    self.assertAlmostEqual(c['ActivationAt']-c['ObservedAt'],.24)
            for d in run['Decisions']:
                angles={round(c['Action']['SteeringRadians']*180/math.pi,2) for c in d['Candidates']}
                self.assertTrue({-15,-5,0,5,15}<=angles)
                self.assertEqual(d['NoSafeContinuation'],not any(c['Safety']==0 for c in d['Candidates']))
                self.assertEqual(d['Safety'],min(c['Safety'] for c in d['Candidates']))
            for c in run['Activations']:self.assertAlmostEqual(c['ActivationAt']-c['ObservedAt'],.24)
            self.assertNotIn('Offset',run['Policy']);self.assertNotIn('Path',run['Policy'])

    def test_final_native_continuations_and_repeated_policy_agree(self):
        self.assertTrue(load('prediction-validation.json')['Passed']);post=load('post-training-prediction.json')
        self.assertTrue(post['Passed']);self.assertGreaterEqual(len(post['Records']),12);self.assertGreater(post['DecisionsMatched'],20)
        self.assertEqual(post['NativeWorlds'],0);self.assertTrue(load('determinism.json')['SameFingerprint'])
        a=load('analysis.json');self.assertTrue(a['VerifiedPolicy']);self.assertGreaterEqual(a['ImprovedHeldOutScenarios'],2)
        self.assertEqual(sum(r['Completed'] for r in a['FinalValidation']),2);self.assertEqual(a['Grade'],'PARTIAL')
        for r in a['FinalValidation']:
            self.assertEqual(r['GateSeconds'] is not None,r['Completed'])
            if r['Completed']:
                self.assertGreater(r['MinimumClearanceMeters'],0);self.assertGreaterEqual(r['DurationSeconds'],r['GateSeconds']+1-1e-8)
            else:self.assertLess(r['MinimumClearanceMeters'],0)

    def test_rate_guard_repair_is_a_float_measurement_fix_with_retained_abort(self):
        p=load('rate-guard-recovery.json');self.assertLess(p['RoundingErrorRadians'],p['HalfFloatUlpAtAngle'])
        run=load(p['Episode']+'.json.gz');self.assertEqual(run['Metrics']['Fingerprint'],p['RetainedFingerprint'])
        self.assertEqual(run['Metrics']['Outcome'],'actuator invariant');self.assertFalse(run['Metrics']['Completed'])

    def test_integration_is_exactly_reversible_to_128_and_manifest_matches(self):
        for name,expected in load('protocol.json')['Sources'].items():
            self.assertEqual(sha((ROOT/'tools/shared-world-speedway'/name).read_bytes()),expected,name)
        for name,item in load('source-extensions.json')['Sources'].items():
            current=(ROOT/'tools/shared-world-speedway'/name).read_bytes().replace(b'\r\n',b'\n').decode()
            self.assertEqual(sha(current.encode()),item['ApprovedSha256'])
            for edit in reversed(item['Edits']):
                self.assertEqual(current.count(edit['After']),1);current=current.replace(edit['After'],edit['Before'],1)
            self.assertEqual(sha(current.encode()),item['BaseSha256'])
        for name,expected in load('manifest.json').items():self.assertEqual(sha((DATA/name).read_bytes()),expected,name)

if __name__=='__main__':unittest.main()
