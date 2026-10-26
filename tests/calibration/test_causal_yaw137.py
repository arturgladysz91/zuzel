"""Frozen evidence checks; never create native worlds or train policies."""
import gzip,hashlib,json
from pathlib import Path
import unittest
ROOT=Path(__file__).resolve().parents[2]
D=ROOT/'docs/experiments/causal-yaw-throttle'
def read(name):
    p=D/name
    return json.loads(gzip.decompress(p.read_bytes()) if name.endswith('.gz') else p.read_bytes())

class CausalYawEvidenceTests(unittest.TestCase):
    def test_frozen_sources_original_guards_and_no_changed_physics(self):
        p=read('protocol.json');self.assertEqual('060d6819cbae1ab701f3439d74a6e6266230419a',p['BaseHead'])
        for name,digest in p['Sources'].items():
            file=ROOT/'tools'/name if name.startswith('causal-yaw-throttle/') else ROOT/'tools/shared-world-speedway'/name
            content=file.read_text(encoding='utf-8-sig').replace('\r\n','\n').encode()
            self.assertEqual(digest,hashlib.sha256(content).hexdigest().upper(),name)
        old=(ROOT/'tools/shared-world-speedway/MultiActionNative135.cs').read_text(encoding='utf-8')
        current=(ROOT/'tools/shared-world-speedway/CausalYawNative137.cs').read_text(encoding='utf-8')
        for line in old.splitlines():
            if line.strip().startswith('if(') and 'failure=' in line or 'var energy=bodyDelta+' in line:
                self.assertIn(line.strip(),current)
        self.assertIn('physics.Step', (ROOT/'tools/shared-world-speedway/WorldRunner.cs').read_text())
        self.assertNotIn('physics.Step',current)

    def test_every_world_reserved_once_has_complete_hash_verified_raw(self):
        ledger=read('attempts.json');manifest=read('raw-manifest.json');complete=read('complete.json')
        self.assertEqual(60,sum(x['NativeWorlds'] for x in ledger));self.assertEqual(60,len(manifest));self.assertTrue(all(x['Complete'] for x in ledger))
        self.assertEqual(len(ledger),len({x['Name'] for x in ledger}))
        self.assertLessEqual(complete['CpuSeconds'],900);self.assertLessEqual(complete['Contacts'],30000000);self.assertLessEqual(complete['Forecasts'],300000)
        for x in manifest:
            data=(D/'native'/x['Name']).read_bytes();self.assertEqual(x['Bytes'],len(data));self.assertEqual(x['Sha256'],hashlib.sha256(data).hexdigest().upper())
        for x in read('native-summaries.json'):
            self.assertEqual(x['NativeContacts']+x['PredictionContacts'],x['Metrics']['Native']['PhysicsEvaluations'])
            self.assertLessEqual(x['SearchCpu'],x['Metrics']['Native']['CpuSeconds']+1e-8)

    def test_cached_prefix_queue_and_native_prediction_accuracy(self):
        p=read('preflight.json');self.assertTrue(p['Passed'])
        for check in p['Checks']:
            if 'Off136Exact' in check:self.assertTrue(check['Off136Exact']);continue
            self.assertTrue(check['ExactPrefix']);self.assertTrue(check['ExactPredictedPending']);self.assertEqual(0,check['CachedVsCallbackXY'])
            self.assertTrue(check['Accuracy']['Passed']);self.assertGreaterEqual(check['Accuracy']['Compared'],91)
            self.assertLess(check['Accuracy']['XY'],.00014)
        for p in D.glob('offline-*.json.gz'):
            x=json.loads(gzip.decompress(p.read_bytes()));s=x['Search'];state=x['State']
            self.assertLessEqual(s['Roots'],5);self.assertLessEqual(s['MaximumCorrections'],7);self.assertLessEqual(len(s['Pairs']),35)
            self.assertEqual(len(s['Held'])+1+len(s['Pairs']),s['Forecasts'])
            for leaf in s['Pairs']:
                q=leaf['SecondObservation'];self.assertAlmostEqual(state['Time']+.2,q['Time'],8)
                self.assertTrue(any(abs(c['ObservedAt']-state['Time'])<1e-8 and abs(c['ActivationAt']-state['Time']-.24)<1e-8 and c['Action']==leaf['Actions'][0] for c in q['Pending']))
                if leaf['Actions'][0]==leaf['Actions'][1]:
                    held=next(h for h in s['Held'] if h['Actions'][0]==leaf['Actions'][0]);self.assertAlmostEqual(held['Evaluation']['Score'],leaf['Evaluation']['Score'],7)

    def test_controlled_and_autonomous_scopes_are_not_conflated(self):
        controlled=read('controlled.json');self.assertEqual(24,len(controlled))
        self.assertTrue(all(x['ExactPrefix'] and x['Accuracy']['Passed'] for x in controlled))
        final=read('confirmation.json');self.assertEqual(8,len(final));self.assertTrue(all(x['Metrics']['Native']['Completed'] for x in final))
        pairs=read('held-comparisons.json');self.assertEqual(4,len(pairs));self.assertFalse(any(x['NewDnf'] for x in pairs));self.assertFalse(any(x['JointImprovement'] for x in pairs))
        self.assertEqual('B',read('selection-lock.json')['Winner']);self.assertTrue(read('selection-lock.json')['ConfirmationNotOpened'])
        self.assertTrue(read('thresholds.json')['BeforeSelectionAndConfirmation']);self.assertTrue(read('evaluation-definition.json')['FrozenBeforeOpeningConfirmation'])
        self.assertEqual('PARTIAL',read('analysis.json')['Verdict'])

    def test_extended_worlds_retain_the_original_native_physical_prefix(self):
        for c in read('protocol.json')['Confirmation']:
            for arm in ['A','B']:
                name=c['Name']+'-'+arm;short=read('native/final-'+name+'.json.gz');long=read('native/extended-'+name+'.json.gz')
                lookup={round(s['Input']['Time'],8):s for s in long['Replay']['Samples']};end=short['Metrics']['Native']['DurationSeconds']
                for s in short['Replay']['Samples']:
                    q=lookup[round(s['Input']['Time'],8)];self.assertEqual(s['Input']['Rider'],q['Input']['Rider'])
                    if s['Input']['Time']<end-1e-8:self.assertEqual(s['Input']['Pending'],q['Input']['Pending'])
                scope=read('scope-extended-'+name+'.json');self.assertTrue(scope['HistoricalTwoBendsCompleted']);self.assertTrue(scope['WholeCaptureCompleted'])
                self.assertGreaterEqual(long['Replay']['Samples'][-1]['Input']['Rider']['TotalReferenceMeters'],scope['FullSecondStraightReference'])

    def test_derived_metrics_use_actual_raw_and_keep_forecasts_separate(self):
        for x in read('raw-manifest.json'):
            metrics=read('native-metrics/'+x['Name']);self.assertEqual(x['Sha256'],metrics['NativeRawSha256']);self.assertEqual(0,metrics['WorldsAdded'])
        facts=read('same-first-full-comparison.json');self.assertEqual(18,len(facts));self.assertEqual(17,sum(x['CorrectedYaw']<x['HeldYaw']-1e-6 and x['CorrectedSafety']<=x['HeldSafety'] for x in facts))
        dnfs=read('dnf.json');self.assertEqual(2,len(dnfs));self.assertTrue(all(x['Arm']=='C' and x['Metrics']['Native']['Outcome']=='uncertified interval' for x in dnfs))

if __name__=='__main__':unittest.main()
