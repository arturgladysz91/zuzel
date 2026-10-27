"""Read-only invariants over frozen #138 native evidence. Never create a world."""
import gzip
import hashlib
import json
from pathlib import Path
import unittest

ROOT=Path(__file__).resolve().parents[2]
D=ROOT/'docs/experiments/throttle-steering-decoupling'
def read(p):
    p=D/p
    return json.loads(gzip.decompress(p.read_bytes()) if p.suffix=='.gz' else p.read_bytes())

class ThrottleSteeringEvidenceTests(unittest.TestCase):
    def test_frozen_sources_physics_and_policy_are_unchanged(self):
        p=read('protocol.json')
        self.assertEqual('53e2c00de383fa51930d8a3888b296ca4f7e2b16',p['BaseHead'])
        self.assertTrue(p['ThresholdsFrozenBeforeNewNativeWorlds'])
        for file,digest in p['Sources'].items():
            content=(ROOT/file).read_text(encoding='utf-8-sig').replace('\r\n','\n').encode()
            self.assertEqual(digest,hashlib.sha256(content).hexdigest().upper(),file)
        old=json.loads((ROOT/'docs/experiments/causal-yaw-throttle/protocol.json').read_text())
        self.assertEqual(old['Policy'],p['Policy'])
        for file,digest in old['Sources'].items():
            path=ROOT/'tools'/file if file.startswith('causal-yaw-throttle/') else ROOT/'tools/shared-world-speedway'/file
            self.assertEqual(digest,hashlib.sha256(path.read_text(encoding='utf-8-sig').replace('\r\n','\n').encode()).hexdigest().upper(),file)

    def test_complete_unique_reservations_and_raw_hashes_obey_budget(self):
        entries=read('attempts.json');analysis=read('analysis.json');manifest=read('raw-manifest.json')
        self.assertTrue(all(x['Complete'] for x in entries));self.assertEqual(len(entries),len({x['Name']for x in entries}))
        self.assertEqual(40,sum(x['NativeWorlds']for x in entries));self.assertEqual(40,len(manifest))
        self.assertLessEqual(analysis['NativeWorlds'],48);self.assertLessEqual(analysis['CpuSeconds'],650)
        self.assertAlmostEqual(analysis['CpuSeconds'],sum(x['CpuSeconds']for x in entries))
        for x in manifest:
            data=(D/'native'/x['Name']).read_bytes();self.assertEqual(x['Bytes'],len(data));self.assertEqual(x['Sha256'],hashlib.sha256(data).hexdigest().upper())

    def test_initial_observation_pending_and_native_prefix_are_exact(self):
        preflight=read('preflight.json');self.assertTrue(preflight['Passed'])
        self.assertTrue(all(x.get('OffExact',True)for x in preflight['Checks']))
        for p in D.glob('result-*.json'):
            r=json.loads(p.read_text());self.assertTrue(r['ExactPrefix'],p.name);self.assertTrue(r['ExactObservation'],p.name)
        self.assertTrue(read('selection-lock.json')['ConfirmationNotOpened']);self.assertFalse(read('selection-lock.json')['PrototypeGate'])
        self.assertFalse(read('analysis.json')['AutonomousPrototypeBuilt']);self.assertFalse(read('analysis.json')['AutonomousGo'])

    def test_requested_steering_factorial_and_full_reaction(self):
        for case in ['D0','D1','S0','S1','H0','H1','H2','H3','T0']:
            runs={a:read('native/'+case+'-'+a+'.json.gz')for a in 'ABCD'}
            result=read('result-'+case+'-A.json');start=result['State']['Input']['Time']
            decisions={a:{round(x['Time'],8):x['Action']for x in r['Replay']['Decisions']}for a,r in runs.items()}
            for t in set.intersection(*(set(ds)for ds in decisions.values())):
                if start-1e-8<=t<start+1.6-1e-8:
                    actions={a:ds[t]for a,ds in decisions.items()}
                    self.assertEqual(actions['A']['SteeringRadians'],actions['C']['SteeringRadians'])
                    self.assertEqual(actions['B']['SteeringRadians'],actions['D']['SteeringRadians'])
                    if t>start+1e-8:
                        self.assertEqual(1,actions['A']['Drive']);self.assertEqual(1,actions['B']['Drive'])
                        self.assertAlmostEqual(.31820688,actions['C']['Drive']);self.assertEqual(actions['C']['Drive'],actions['D']['Drive'])
            for r in runs.values():
                self.assertTrue(all(abs(c['ActivationAt']-c['ObservedAt']-.24)<1e-8 for c in r['Replay']['Activations']))

    def test_failures_and_scope_do_not_become_completed_or_ai_success(self):
        rows=read('native-summaries.json');dnf=read('dnf.json');a=read('analysis.json')
        self.assertEqual(36,len(rows));self.assertEqual(len(dnf),sum(not r['Completed']for r in rows))
        self.assertEqual('PHYSICS LIMITATION',a['Verdict']);self.assertEqual(0,a['LegalFullCorrected'])
        self.assertEqual(18,len(read('offline-18-complete.json')))
        for r in rows:
            if r['Completed']:
                self.assertTrue(r['HistoricalCompleted']);self.assertGreater(r['MinimumCertificate'],0)
            if r['Outcome']=='control envelope':
                self.assertGreater(abs(next(x['LastNativeState']['beta']for x in dnf if x['Name']==r['Name'])),45)
        self.assertEqual(0,a['NewNativeWorldsDuringReporting']);self.assertEqual(0,a['NewForceQueriesDuringReporting'])

if __name__=='__main__':unittest.main()
