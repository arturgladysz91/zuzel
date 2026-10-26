"""Evidence contracts, without launching simulation worlds or training."""
import hashlib
import gzip
import base64
import json
import math
from pathlib import Path
import unittest

ROOT=Path(__file__).resolve().parents[2]
D=ROOT/'docs/experiments/speed-throttle-root-cause'
def read(name):return json.loads((D/name).read_text(encoding='utf-8'))

class SpeedThrottleEvidenceTests(unittest.TestCase):
    def test_every_reserved_world_has_hash_verified_native_evidence(self):
        entries=read('attempts.json');manifest=read('raw-manifest.json');complete=read('complete.json')
        self.assertEqual(54,len(manifest));self.assertEqual(54,sum(x['NativeWorlds'] for x in entries))
        self.assertTrue(all(x['Complete'] for x in entries));self.assertEqual(len(manifest),len({x['Name'] for x in manifest}))
        for x in manifest:
            content=(D/'native'/x['Name']).read_bytes()
            self.assertEqual(x['Bytes'],len(content));self.assertEqual(x['Sha256'],hashlib.sha256(content).hexdigest().upper())
        self.assertLessEqual(complete['CpuSeconds'],1400);self.assertLessEqual(complete['ContactEvaluations'],25000000)
        self.assertLessEqual(complete['Forecasts'],250000);self.assertTrue(complete['ExactPrefixes'])
        self.assertTrue(read('preflight.json')['Passed'])

    def test_cost_decomposition_preserves_original_score_and_ranking(self):
        rows=read('costs.json');states={}
        fields=['ProgressCost','DirectionCost','YawCost','ThrottleCost','SteeringCost','GeometryCost','ClearanceCost','UnsafeCost']
        for row in rows:
            self.assertAlmostEqual(row['Score'],sum(row[k] for k in fields),places=8)
            states.setdefault((row['Case'],row['Corner'],row['Distance']),[]).append(row)
        self.assertEqual(20,len(states))
        for rows in states.values():
            chosen=[x for x in rows if x['Chosen']];self.assertEqual(1,len(chosen));self.assertEqual(1,chosen[0]['Rank'])
            for angle in {x['Action']['SteeringRadians'] for x in rows}:
                self.assertEqual(3,len([x for x in rows if x['Action']['SteeringRadians']==angle]))
        findings=read('score-findings.json')
        self.assertEqual(18,len(findings['SameClassMoreProgressButRejected']))
        self.assertFalse(any(x['ThrottleRemovalWouldFlip'] for x in findings['SameClassMoreProgressButRejected']))

    def test_actual_queue_events_and_dnf_cannot_be_reported_as_success(self):
        results=read('paired-results.json');failures={x['Name'] for x in read('dnf.json')}
        self.assertEqual(42,len(results));self.assertEqual(7,len(failures))
        for r in results:
            self.assertTrue(r['ExactPrefix']);e=r['Events']
            if e['ActivationAt'] is not None:self.assertAlmostEqual(.24,e['ActivationAt']-e['ObservedAt'],places=8)
            self.assertEqual(r['Name'] in failures,not r['Metrics']['Native']['Completed'])
            if r['Name'] in failures:self.assertIsNone(r['GainSeconds'])
        held=[x for x in results if x['Case'].startswith('V-')]
        self.assertEqual(4,len(held));self.assertTrue(all(x['Metrics']['Native']['Completed'] for x in held))

    def test_straight_uses_real_62m_and_coherent_rolling_wheel(self):
        runs=read('straight.json');self.assertEqual(8,len(runs))
        for r in runs:
            self.assertIsNone(r['Failure']);self.assertEqual(62,r['EndReference']-r['StartReference'])
            self.assertAlmostEqual(r['InitialKph']/3.6/.33,r['InitialWheelOmega'],places=4)
            self.assertEqual(9,r['InitialEngine']['OverallRatio'])
            for m in [10,20,30,40,50,62]:
                actual=r['Milestones'][str(m)]['ActualDistance'];self.assertGreaterEqual(actual,m);self.assertLess(actual,m+.5)

    def test_packaging_preserves_every_executed_computational_source(self):
        archive=json.loads(gzip.decompress((D/'executed-sources.json.gz').read_bytes()))
        protocol=read('protocol.json');audit=read('packaging-audit.json')['Sources']
        self.assertEqual(set(protocol['Sources']),set(archive))
        for name,encoded in archive.items():
            executed=base64.b64decode(encoded);current=(ROOT/'tools/shared-world-speedway'/name).read_bytes()
            self.assertEqual(protocol['Sources'][name],hashlib.sha256(executed).hexdigest().upper())
            if name=='Program.cs':
                executed=executed.replace(b'if(scenario=="speed-throttle"){SpeedThrottleAudit136.Run(args[1],args[2],args[3]);return;}\n',b'')
            self.assertEqual(executed.replace(b'\r\n',b'\n'),current.replace(b'\r\n',b'\n'),name)
            self.assertEqual(audit[name]['ExecutedRawSha256'],protocol['Sources'][name])

if __name__=='__main__':unittest.main()
