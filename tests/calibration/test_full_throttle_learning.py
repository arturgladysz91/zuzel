"""Audit #132 native evidence and untouched historical contracts without new worlds."""
import gzip,hashlib,json,math,statistics,unittest
from pathlib import Path
from extended_source_guard import source131, source132
ROOT=Path(__file__).resolve().parents[2]
D=ROOT/'docs/experiments/full-throttle-learning'
load=lambda n:json.loads((D/n).read_bytes())
sha=lambda b:hashlib.sha256(b).hexdigest().upper()
key=lambda s:tuple(s[k] for k in ['Speed','Offset','Start','OutwardSpeed','HeadingError','YawRate'])

class FullThrottleEvidenceTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.p=load('protocol.json');cls.a=load('attempts.json');cls.f=load('final-results.json')
        cls.runs=json.loads(gzip.decompress((D/'selected-replays.json.gz').read_bytes()))['Runs']

    def test_frozen_sources_history_and_manifests(self):
        for name,digest in self.p['Sources'].items():self.assertEqual(sha(source132(name)),digest,name)
        old=json.loads((D.parent/'autonomous-speedway-extended-learning/protocol.json').read_bytes())
        for name,digest in old['Sources'].items():self.assertEqual(sha(source131(name)),digest,name)
        for name,digest in old['Sources'].items():
            if name not in ['Program.cs','FreeLearningController.cs','RobustLearningExperiment.cs']:self.assertEqual(sha((ROOT/'tools/shared-world-speedway'/name).read_bytes()),digest,name)
        for name,digest in load('manifest.json').items():self.assertEqual(sha((D/name).read_bytes()),digest,name)
        self.assertEqual(sha((D/'selected-replays.json.gz').read_bytes()),load('selected-manifest.json')['BundleSha256'])
        packed=(D/'throttle-audit.json.gz').read_bytes();original=gzip.decompress(packed);archive=load('throttle-audit-archive.json')
        self.assertEqual(sha(packed),archive['CompressedSha256']);self.assertEqual(sha(original),archive['OriginalSha256']);self.assertEqual(len(original),archive['UncompressedBytes'])
        self.assertEqual(load('predictor-alignment-protocol.json')['SourceSha256'],sha((ROOT/'tools/full-throttle-audit/Program.cs').read_bytes()))
        self.assertEqual(load('predictor-alignment-protocol.json')['InitialPredictorSha256'],sha((D/'predictor-initial.json').read_bytes()))

    def test_final_is_new_legal_protocol_and_opened_only_after_choice(self):
        p=self.p;final={key(s) for s in p['FinalSingle']+p['FinalTwo']};others={key(s) for s in p['Training']+p['Diagnostic']+p['Selection']}
        self.assertEqual(len(final),36);self.assertEqual(len(p['FinalSingle']),24);self.assertEqual(len(p['FinalTwo']),12);self.assertFalse(final&others)
        for directory in ['autonomous-speedway-extended-learning','robust-free-racing-learning','free-racing-learning']:
            def scenarios(x):
                if isinstance(x,dict):
                    if all(k in x for k in ['Speed','Offset','Start','OutwardSpeed','HeadingError','YawRate']):yield key(x)
                    for v in x.values():yield from scenarios(v)
                elif isinstance(x,list):
                    for v in x:yield from scenarios(v)
            self.assertFalse(final&set(scenarios(json.loads((D.parent/directory/'protocol.json').read_bytes()))))
        lock=load('winner-lock.json');self.assertEqual(lock['FinalWorlds'],0);self.assertEqual(lock['PolicyHash'],sha((D/'winner.json').read_bytes()));first=min(e['Number'] for e in self.a if e['Category']=='final')
        self.assertGreater(first,lock['ReservationCount']);self.assertTrue(all(e['Number']<first for e in self.a if e['Category'] in ['train-ab','continuation','selection']))
        self.assertEqual(load('training-complete.json')['FinalWorlds'],0)

    def test_controlled_equal_ab_and_same_initial_population(self):
        for seed in self.p['Seeds']:
            arms=[load(f'generations-{v}-s{seed}.json') for v in ['A','B']]
            self.assertEqual([c['Policy'] for c in arms[0][0]['Candidates']],[c['Policy'] for c in arms[1][0]['Candidates']])
            self.assertEqual(len(arms[0]),len(arms[1]));self.assertLessEqual(len(arms[0]),5)
            for gs in arms:
                for g in gs:
                    self.assertEqual(len(g['Candidates']),5)
                    for c in g['Candidates']:
                        self.assertEqual(len(c['Results']),4);self.assertEqual(c['Scenarios'],g['Candidates'][0]['Scenarios']);self.assertEqual(sum(n.endswith('-two') for n in c['Scenarios']),3)
                        self.assertEqual(len(c['Policy']['Values']),8)
        self.assertLessEqual(sum(e['Category']=='train-ab' for e in self.a),400)
        for row in load('throttle-by-attempt.json'):
            if row['Label'].startswith('A-s'):self.assertEqual(row['FullRequestsAfterActiveDrop'],0)

    def test_native_accounting_predictor_alignment_and_validity(self):
        a=load('analysis.json');self.assertTrue(all(e['Complete'] for e in self.a));self.assertEqual(len(self.a),len({e['Name'] for e in self.a}))
        for field,ledger,limit in [('NativeWorlds','NativeWorlds',750),('CpuSeconds','CpuSeconds',6000),('ContactEvaluations','Evaluations',100000000),('Forecasts','Forecasts',1200000)]:
            self.assertAlmostEqual(a[field],sum(e[ledger] for e in self.a));self.assertLessEqual(a[field],limit)
        self.assertEqual(sum(e['Category']=='final' for e in self.a),144)
        self.assertTrue(load('preflight.json')['Passed']);aligned=load('predictor-aligned.json');self.assertTrue(aligned['Passed']);self.assertFalse(aligned['ThresholdsChanged'])
        self.assertLess(next(e['Number'] for e in self.a if e['Name']=='predictor-timestamp-alignment'),min(e['Number'] for e in self.a if e['Category']=='train-ab'))
        for c in aligned['Checks']:
            self.assertTrue(c['Passed']);self.assertGreaterEqual(c['Compared'],91)
            for point in c['Points']:
                if point['RequiredGridPoint']:self.assertTrue(point['Compared']);self.assertTrue(point['Passed'])
        for e in self.a:
            n=(e.get('Metrics') or {}).get('Native')
            if not n:continue
            self.assertNotIn('invariant',n['Outcome'])
            if n['Completed']:
                self.assertLessEqual(n['MaximumBetaDegrees'],45);self.assertGreaterEqual(n['MinimumClearanceMeters'],0);self.assertGreaterEqual(n['DurationSeconds'],n['GateSeconds']+1-1e-7)
            else:self.assertIsNone(n['GateSeconds'])
        for q in load('representative-quality.json'):
            self.assertLessEqual(q['MaximumFrontUtilization'],1.00001);self.assertLessEqual(q['MaximumRearUtilization'],1.00001)

    def test_native_replays_throttle_queue_and_comparison_states(self):
        for group in load('index.json')['Groups']:
            states=[self.runs[r['Name']]['Replay']['Samples'][0]['Input']['Rider'] for r in group['Runs']];self.assertTrue(all(s==states[0] for s in states))
            inputs=[self.runs[r['Name']]['Replay']['Samples'][0]['Input'] for r in group['Runs']]
            for field in ['Omega','Steering','Active']:self.assertTrue(all(s[field]==inputs[0][field] for s in inputs))
        for name,r in self.runs.items():
            samples=r['Replay']['Samples'];ds=r['Replay']['Decisions'];self.assertEqual(r['Metrics']['Native']['Fingerprint'],load('selected-manifest.json')['Runs'][name]['NativeFingerprint'])
            self.assertEqual(load('selected-manifest.json')['Runs'][name]['RawSha256'],next(e for e in self.a if e['Name']==name)['RawSha256'])
            for s in samples:
                self.assertLessEqual(abs(s['Input']['Steering']),math.radians(35)+1e-6)
                for c in s['Input']['Pending']:self.assertAlmostEqual(c['ActivationAt']-c['ObservedAt'],.24)
            for x,y in zip(samples,samples[1:]):
                dt=y['Input']['Time']-x['Input']['Time'];self.assertLessEqual(abs(y['Input']['Steering']-x['Input']['Steering']),dt*math.radians(90)+1e-5)
            for c in r['Replay']['Activations']:self.assertAlmostEqual(c['ActivationAt']-c['ObservedAt'],.24)
            for dec in ds:
                if not dec['Candidates']:continue
                self.assertTrue(any(c['Action']==dec['Action'] for c in dec['Candidates']))
                chosen=next(c for c in dec['Candidates'] if c['Action']==dec['Action']);best_safety=min(c['Safety'] for c in dec['Candidates'])
                self.assertEqual(chosen['Safety'],best_safety);self.assertEqual(chosen['Score'],min(c['Score'] for c in dec['Candidates'] if c['Safety']==best_safety))
                expanded='-131-B-' in name or name=='diag-B-D1' or name.startswith('B-s') or name.startswith('final-132-final-') and load('winner.json')['FullThrottle']
                if expanded:
                    angles={c['Action']['SteeringRadians'] for c in dec['Candidates']}
                    for angle in angles:self.assertTrue(any(c['Action']==dict(SteeringRadians=angle,Drive=1) for c in dec['Candidates']))
                if '-131-A-' in name or name=='diag-A-D1':self.assertLessEqual(len({c['Action']['Drive'] for c in dec['Candidates']}),3)

    def test_final_paired_criterion_and_report_are_independent_of_selection(self):
        self.assertEqual(len(self.f),8)
        for suite in ['single','two']:
            groups=[r for r in self.f if r['Variant'].endswith('-'+suite)];self.assertEqual(len(groups),4);self.assertTrue(all(r['Scenarios']==groups[0]['Scenarios'] for r in groups))
        for x in load('paired-times.json'):
            ratios=[]
            for pair in x['Pairs']:
                self.assertAlmostEqual(pair['Ratio'],pair['New']/pair['Old']-1);ratios.append(pair['Ratio'])
            self.assertEqual(x['MedianRatio'],statistics.median(ratios) if ratios else None)
        winner=load('winner.json');self.assertEqual(next(r for r in self.f if r['Variant']=='132-final-two')['Policy'],winner['Policy'])
        a=load('analysis.json');groups={r['Variant']:r for r in self.f};completed=lambda n:sum(x['Native']['Completed'] for x in groups[n]['Results'])
        expected=completed('132-final-single')>=23 and completed('132-final-two')>=11 and a['TwoPairedVs131Median'] is not None and a['TwoPairedVs131Median']<=-.02 and a['SinglePairedVs130Median'] is not None and a['SinglePairedVs130Median']<=.05 and a['SinglePairedVs130Maximum']<=.15
        self.assertEqual(a['Verdict'],'PASS' if expected else 'PARTIAL')

if __name__=='__main__':unittest.main()
