"""Audit immutable native evidence without creating Box2D worlds in CI."""
import gzip
import hashlib
import json
import math
from pathlib import Path
import unittest

ROOT=Path(__file__).resolve().parents[2]
DATA=ROOT/'docs/experiments/robust-free-racing-learning'
def load(name):return json.loads((DATA/name).read_bytes())
def sha(b):return hashlib.sha256(b).hexdigest().upper()
def native(r):return r['Native']

class RobustLearningEvidenceTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.protocol=load('protocol.json');cls.attempts=load('attempts.json')
        cls.bundle=json.loads(gzip.decompress((DATA/'selected-replays.json.gz').read_bytes()))['Runs']

    def test_frozen_pools_have_no_final_test_or_old_holdout_leakage(self):
        p=self.protocol;self.assertEqual(p['BaseHead'],'9470f63a11ccc31e6c7bdb862743d11e4be053d4');self.assertEqual(p['Seed'],1302026)
        key=lambda s:(s['Speed'],s['Offset'],s['Start'])
        train=p['High']+p['Medium']+p['Retention'];selection=p['Selection'];test=p['SealedTest']
        old=json.loads((DATA/'../free-racing-learning/protocol.json').read_bytes())['Validation']
        self.assertEqual(len(test),12);self.assertEqual(len(selection),6)
        self.assertFalse(set(map(key,train))&set(map(key,test)));self.assertFalse(set(map(key,selection))&set(map(key,test)))
        self.assertFalse(set(map(key,train))&set(map(key,selection)));self.assertFalse(set(map(key,train))&set(map(key,old)))
        self.assertEqual(load('selected-policy.json')['TestWorldsBeforeFreeze'],0);self.assertTrue(load('selected-policy.json')['Frozen'])
        first_test=min(e['Number'] for e in self.attempts if e['Category']=='test')
        self.assertTrue(all(e['Number']<first_test for e in self.attempts if e['Category'].startswith('train-') or e['Category']=='selection'))

    def test_same_cem_seed_start_and_four_equal_conditions_per_candidate(self):
        p=self.protocol;generations={v:load('generations-'+v+'.json') for v in ['A','B']}
        self.assertEqual([c['Policy'] for c in generations['A'][0]['Candidates']],[c['Policy'] for c in generations['B'][0]['Candidates']])
        for variant,gs in generations.items():
            for g in gs:
                expected=p['Schedule'+variant][g['Generation']];self.assertEqual(len(g['Candidates']),5)
                for c in g['Candidates']:
                    self.assertEqual(c['Scenarios'],expected);self.assertEqual(len(c['Results']),4)
                    self.assertTrue(all(name[0] in ('H',) if variant=='A' else name[0] in ('H','M','R') for name in c['Scenarios']))
                for i,std in enumerate(g['Std']):self.assertGreaterEqual(std,(4 if i in [1,4] else 2 if i==2 else .79 if i==3 else 1.8 if i==5 else .2 if i==6 else 1 if i==7 else 1.5)*.15-1e-10)
        b=generations['B'];self.assertTrue(all(any(n.startswith('R') for n in g['Candidates'][0]['Scenarios']) for g in b))
        self.assertTrue(all(any(n.startswith('M') for n in g['Candidates'][0]['Scenarios']) for g in b))

    def test_all_attempts_and_resource_limits_are_accounted_for(self):
        a=load('analysis.json');p=self.protocol['Limits'];entries=self.attempts
        self.assertTrue(all(e['Complete'] for e in entries));self.assertEqual(len(entries),len({e['Name'] for e in entries}))
        self.assertEqual(sum(e['Category']=='train-A' for e in entries),100);self.assertTrue(100<=sum(e['Category']=='train-B' for e in entries)<=300)
        self.assertEqual(sum(e['NativeWorlds'] for e in entries),a['NativeWorlds']);self.assertLessEqual(a['NativeWorlds'],p['NativeWorlds'])
        self.assertAlmostEqual(sum(e['CpuSeconds'] for e in entries),a['CpuSeconds']);self.assertLess(a['CpuSeconds'],p['CpuSeconds'])
        self.assertEqual(sum(e['Evaluations'] for e in entries),a['ContactEvaluations']);self.assertLess(a['ContactEvaluations'],p['ContactEvaluations'])
        self.assertEqual(sum(e['Forecasts'] for e in entries),a['Forecasts'])
        self.assertEqual(sum(e['Category']=='test' for e in entries),48)
        self.assertLessEqual(sum(e['Category']=='diagnostic' for e in entries),8);self.assertLessEqual(sum(e['Category']=='selection' for e in entries),72)
        for e in entries:
            if e['Metrics']:
                m=e['Metrics']['Native'];self.assertEqual(m['Completed'],m['GateSeconds'] is not None)
                if m['Completed']:
                    self.assertGreaterEqual(m['DurationSeconds'],m['GateSeconds']+1-1e-7)
                    if e['Category']!='diagnostic':self.assertGreaterEqual(e['Metrics']['MinimumLegacyCertificate'],0)
                self.assertNotIn('invariant',m['Outcome'])

    def test_selection_shortlist_and_test_do_not_replace_an_older_policy_after_test(self):
        selected=load('selected-policy.json')['Selected'];results=load('test-results.json')
        self.assertEqual([e['Variant'] for e in results],['129-100','129-200','129-final','130-final'])
        self.assertEqual(results[-1]['Policy'],selected['Policy']);self.assertEqual(results[-1]['Id'],selected['Id'])
        references=self.protocol['ReferencePolicies']
        for e in results[:3]:self.assertEqual(e['Policy'],references[e['Variant']])
        self.assertTrue(all(e['Scenarios']==[s['Name'] for s in self.protocol['SealedTest']] for e in results))
        for variant in ['A','B']:
            archive=load('archive-'+variant+'.json');generations=load('generations-'+variant+'.json')
            self.assertEqual(len(archive),len(generations)*5)
            self.assertEqual({x['Id'] for x in archive},{c['Id'] for g in generations for c in g['Candidates']})

    def test_boundary_uncertainty_and_confirmed_crossing_are_different(self):
        d=load('boundary-diagnostic.json');audit=load('audit129.json')
        self.assertTrue(d['OriginalPrefixBitExact']);self.assertGreater(d['OriginalEndpointOuter'],0)
        self.assertGreater(audit['LastIntervalCertificate']['Minimum'],0)
        self.assertGreater(d['Continued']['Native']['DurationSeconds'],d['OriginalStop'])
        self.assertEqual(d['Continued']['BoundaryClass'],'confirmed crossing')
        self.assertTrue(d['Alternate']['Native']['Completed']);self.assertGreaterEqual(d['Alternate']['MinimumLegacyCertificate'],0)
        self.assertEqual(audit['InterveneAt'],1);self.assertEqual(audit['ExactDefaultDecisions'],35)
        # The intervention changes no physical state before its full0.24s reaction.
        original=self.bundle['diagnostic-boundary']['Replay']['Samples'];alternate=self.bundle['diagnostic-one-command']['Replay']['Samples']
        by_time={round(s['Input']['Time'],8):s for s in original}
        for s in alternate:
            if s['Input']['Time']<=1.24+1e-8:
                self.assertEqual(s['Input']['Rider'],by_time[round(s['Input']['Time'],8)]['Input']['Rider'])

    def test_native_replays_are_same_condition_and_preserve_queue_and_physics_limits(self):
        index=load('index.json')
        for group in index['Groups']:
            runs=[self.bundle[r['Name']] for r in group['Runs']]
            if group['Id'].startswith('T'):self.assertEqual(len(runs),4)
            self.assertTrue(all(r['Replay']['Samples'][0]['Input']['Rider']==runs[0]['Replay']['Samples'][0]['Input']['Rider'] for r in runs))
            for r in runs:
                replay=r['Replay'];self.assertAlmostEqual(replay['Samples'][-1]['Input']['Time'],replay['Metrics']['DurationSeconds'])
                for s in replay['Samples']:
                    x=s['Input'];self.assertLessEqual(abs(x['Steering']),math.radians(35)+1e-7)
                    self.assertTrue(all(math.isfinite(x['Rider'][key]) for key in ['Speed','Heading','AngularVelocity']))
                    for command in x['Pending']:
                        self.assertLessEqual(command['ObservedAt'],x['Time']+1e-8);self.assertAlmostEqual(command['ActivationAt']-command['ObservedAt'],.24)
                for t in r['Telemetry']:
                    force=t['Forces'];h=t['Seconds'];limit=math.pi/2*h
                    # Original float actuator is checked bit-exactly in native harness; this data tolerance covers one rounded endpoint ULP.
                    self.assertLessEqual(abs(force['SteeringAfter']-force['SteeringBefore']),limit+7e-8)

    def test_native_replay_determinism_and_both_prediction_horizons_pass(self):
        self.assertTrue(load('audit129.json')['PredictionPassed']);self.assertTrue(load('post-prediction.json')['Passed']);self.assertTrue(load('determinism.json')['Passed'])
        self.assertEqual({r['Horizon'] for r in load('audit129.json')['Predictions']},{.3,.6,.9})
        self.assertTrue(all(r['Passed'] for r in load('post-prediction.json')['Records']))
        initial=load('post-prediction-initial.json');self.assertEqual(sum(r.get('Reason')=='No native endpoint' for r in initial['Records']),9)
        self.assertTrue(all(r['Passed'] for r in initial['Records'] if 'XY' in r))
        correction=load('post-audit-alignment.json');self.assertFalse(correction['ChangedThresholds']);self.assertFalse(correction['ChangedPolicy']);self.assertEqual(correction['NativeWorlds'],0)
        costs=load('horizon-cost.json');self.assertEqual(costs['NativeWorlds'],0)
        self.assertGreater(costs['Timings'][1]['Evaluations'],costs['Timings'][0]['Evaluations'])

    def test_two_bends_are_gated_by_frozen_single_bend_rule(self):
        decision=load('two-bend-decision.json');tests=load('test-results.json');counts=[sum(r['Native']['Completed'] for r in e['Results']) for e in tests]
        self.assertEqual(decision['Run'],counts[-1]==12 and counts[-1]>=max(counts[:-1]))
        self.assertEqual(sum(e['Category']=='two-bends' for e in self.attempts),int(decision['Run']))

    def test_source_protocol_and_all_manifests_match_without_modifying129(self):
        for name,expected in self.protocol['Sources'].items():self.assertEqual(sha((ROOT/'tools/shared-world-speedway'/name).read_bytes()),expected,name)
        for name,item in load('source-extensions.json')['Sources'].items():
            current=(ROOT/'tools/shared-world-speedway'/name).read_bytes().replace(b'\r\n',b'\n').decode();self.assertEqual(sha(current.encode()),item['ApprovedSha256'])
            for edit in reversed(item['Edits']):
                self.assertEqual(current.count(edit['After']),1);current=current.replace(edit['After'],edit['Before'],1)
            self.assertEqual(sha(current.encode()),item['BaseSha256'])
        for name,expected in load('manifest.json').items():self.assertEqual(sha((DATA/name).read_bytes()),expected,name)
        raw=load('raw-manifest.json');self.assertEqual(len(raw['Files']),sum(e['NativeWorlds'] for e in self.attempts))
        for name,r in self.bundle.items():self.assertEqual(raw['Files'][name+'.json.gz']['NativeFingerprint'],r['Replay']['Metrics']['Fingerprint'])
        self.assertNotEqual(len(self.bundle),len(raw['Files'])) # full raw episodes are explicitly local, not silently omitted from aggregate metrics
        old=DATA/'../free-racing-learning'
        for name,expected in json.loads((old/'manifest.json').read_bytes()).items():self.assertEqual(sha((old/name).read_bytes()),expected,name)

if __name__=='__main__':unittest.main()
