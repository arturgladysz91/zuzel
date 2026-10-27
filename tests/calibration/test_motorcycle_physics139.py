"""Evidence integrity checks: no new worlds, forecasting or parameter fitting."""
import gzip
import hashlib
import json
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[2]
D = ROOT / 'docs/experiments/motorcycle-physics-validation'


def read(name):
    p = D / name
    return json.loads(gzip.decompress(p.read_bytes()) if p.suffix == '.gz' else p.read_bytes())


class MotorcyclePhysicsEvidenceTests(unittest.TestCase):
    def test_original_freeze_and_explicit_infrastructure_amendment(self):
        p = read('protocol.json')
        a = read('infrastructure-amendment.json')
        self.assertEqual('713b0cc4274efbc430d51d0a3874a658defe240a', p['BaseHead'])
        self.assertTrue(p['FrozenBeforeNewPhysicsQueriesAndWorlds'])
        self.assertFalse(p['ProductionDefaultChanged'])
        self.assertEqual(a['OriginalProtocolSha256'], hashlib.sha256((D/'protocol.json').read_bytes()).hexdigest().upper())
        self.assertEqual(0, a['TreatmentWorldsStarted'])
        self.assertEqual(0, a['RerunNativeWorlds'])
        for path, sha in p['Sources'].items():
            if path in a['ExcludedGeneratedFiles']:
                continue
            if path in a['SourceUpdates']:
                self.assertEqual(sha, a['SourceUpdates'][path]['OriginalSha256'])
                sha = a['SourceUpdates'][path]['NewSha256']
            actual = hashlib.sha256((ROOT/path).read_text(encoding='utf-8-sig').replace('\r\n','\n').encode()).hexdigest().upper()
            self.assertEqual(sha, actual, path)

    def test_all_reservations_and_raw_captures_reconcile_without_reruns(self):
        ledger = read('attempts.json')
        self.assertTrue(all(x['Complete'] for x in ledger))
        self.assertEqual(len(ledger), len({x['Name'] for x in ledger}))
        self.assertEqual(22, sum(x['NativeWorlds'] for x in ledger))
        self.assertLessEqual(sum(x['CpuSeconds'] for x in ledger), 450)
        self.assertEqual(sum(x['CpuSeconds'] for x in ledger), read('cost.json')['CpuSeconds'])
        native = [x for x in ledger if x['NativeWorlds']]
        self.assertEqual(22, len(read('native-manifest.json')))
        for x in native:
            blob = (D/'native'/(x['Name']+'.json.gz')).read_bytes()
            self.assertEqual(x['RawBytes'], len(blob))
            self.assertEqual(x['RawSha256'], hashlib.sha256(blob).hexdigest().upper())
        maps = ledger[0]
        self.assertEqual(0, maps['NativeWorlds'])
        self.assertEqual(3951, maps['Evaluations'])

    def test_native_samples_timing_actuation_and_publication_are_not_fabricated(self):
        for x in read('native-summaries.json'):
            d = read(x['Raw'])
            v = read(x['Viewer'])
            s = d['Replay']['Samples']
            self.assertEqual(len(s), len(v['Frames']))
            self.assertEqual(len(s)-1, len(d['Telemetry']))
            self.assertTrue(x['ExactInitial'])
            self.assertTrue(x['PrefixExact'])
            self.assertEqual(x['Metrics']['Native']['Fingerprint'], d['Replay']['Metrics']['Fingerprint'])
            for a, b in zip(s, v['Frames']):
                self.assertEqual(a['Input']['Time'], b['time'])
                self.assertEqual(a['Input']['Rider']['Position']['X'], b['x'])
                self.assertEqual(a['Input']['Active']['Drive'], b['gas'])
            self.assertTrue(all(a['Input']['Time'] < b['Input']['Time'] for a,b in zip(s,s[1:])))
            for a in d['Replay']['Activations']:
                self.assertAlmostEqual(.24, a['ActivationAt']-a['ObservedAt'], places=7)
            for f in d['Telemetry']:
                self.assertAlmostEqual(f['Forces']['NetTorque'], f['Forces']['FrontTorque']+f['Forces']['RearTorque'], delta=.0002)
                self.assertEqual(0, f['Wheel']['FrontLateralNewtons'])

    def test_confirmation_is_distinct_and_success_requires_separate_straight_gate(self):
        cases = read('cases.json')
        self.assertEqual(4, len({json.dumps(x['Case']['Scenario'],sort_keys=True) for x in cases}))
        self.assertEqual({1,2}, {x['Corner'] for x in cases})
        self.assertTrue(read('confirmation-lock.json')['FrozenBeforeConfirmationWorlds'])
        rows = read('native-summaries.json')
        c = [x for x in rows if x['Arm']=='C']
        self.assertEqual(4, len(c))
        self.assertTrue(all(x['HistoricalTwoBendsCompleted'] and x['FullNextStraightCertified'] and x['Exit']['gas']==1 for x in c))
        self.assertTrue(all(x['StraightPoints']['62'] for x in c))
        self.assertFalse(any(x['Metrics']['Native']['Completed'] for x in rows if x['Arm'] in ['A','B']))
        failed = {x['Name'] for x in rows if not x['Metrics']['Native']['Completed']}
        self.assertEqual(failed, {x['Name'] for x in read('dnf.json')})
        self.assertEqual(16, len(failed))

    def test_off_fingerprints_and_variant_initial_states_match_historical_evidence(self):
        for case in ['D0','D1']:
            off = read('native/off-'+case+'.json.gz')
            original = json.loads(gzip.decompress((ROOT/'docs/experiments/throttle-steering-decoupling/native'/('off-'+case+'.json.gz')).read_bytes()))
            self.assertEqual(original['Replay']['Metrics']['Fingerprint'], off['Replay']['Metrics']['Fingerprint'])
            initial = read('native/'+case+'-B.json.gz')['Replay']['Samples'][0]['Input']
            for x in read('native-summaries.json'):
                if x['Name'].startswith('sensitivity-'+case):
                    self.assertEqual(initial, read(x['Raw'])['Replay']['Samples'][0]['Input'])

    def test_static_queries_obey_passivity_and_never_claim_dynamic_stability(self):
        check = read('contact-invariants.json')
        self.assertTrue(check['Passed'])
        self.assertEqual(0, check['FrontForceErrorNewtons'])
        self.assertEqual(0, check['RearForceErrorNewtons'])
        self.assertLess(check['FrontContactPowerWatts'], 0)
        maps = read('contact-maps.json.gz')
        self.assertEqual(3490, len(maps))
        for x in maps:
            self.assertLessEqual(abs(x['FrontLocalFx']), .001)
            self.assertLessEqual(x['RearUse'], 1.00001)
            self.assertGreaterEqual(x['FrontLossWatts'], -.001)
            self.assertGreaterEqual(x['RearLongLossWatts'], -.001)
            self.assertGreaterEqual(x['RearLatLossWatts'], -.001)
        root = next(x for x in read('yaw-balance-roots.json') if x['State']=='D0-failed')['Roots'][0]
        self.assertLess(abs(root['NetTorque']), .01)
        self.assertGreater(root['BetaRate'], 2)
        self.assertFalse(root['AnchorSnapshotLegal'])


if __name__ == '__main__':
    unittest.main()
