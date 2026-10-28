"""Read-only forensic evidence checks. Does not create Box2D worlds."""
import csv,gzip,hashlib,importlib.util,json,math,subprocess,sys,unittest
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2];D=ROOT/'docs/experiments/corner-speed-loss';N=D/'native'
def read(p):return json.loads(gzip.decompress(p.read_bytes()) if str(p).endswith('.gz') else p.read_bytes())
class CornerSpeedLoss145Tests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.analysis=read(D/'saved-analysis.json');cls.results={p.name[:-12]:read(p) for p in N.glob('*-result.json')}
    def test_all_twenty_four_original_corners_and_seventy_two_phases(self):
        rows=[q for q in self.analysis['corners'] if q['run'] in ['A','B','F','K1']]
        self.assertEqual(24,len(rows));self.assertEqual(72,sum(len(q['phases']) for q in rows))
        self.assertEqual({(n,l,b) for n in ['A','B','F','K1'] for l in range(1,4) for b in [1,2]}, {(q['run'],q['lap'],q['bend']) for q in rows})
        k=next(q for q in rows if (q['run'],q['lap'],q['bend'])==('K1',2,2))
        self.assertAlmostEqual(47.7258288,k['entry']['speed_kph']-k['minimum']['speed_kph'],3)
        self.assertEqual(34.67,k['minimum']['time_s']);self.assertEqual(0,k['entry_to_minimum']['negative_drive_torque_s'])
        self.assertLess(abs(k['entry_to_minimum']['energy']['total_energy_residual_J']),.2)
    def test_every_phase_closes_body_work_and_preserves_non_overlapping_energy_channels(self):
        for c in self.analysis['corners']:
            self.assertAlmostEqual(c['duration_s'],sum(p['duration_s'] for p in c['phases'].values()),7)
            for p in c['phases'].values():
                e=p['energy'];self.assertAlmostEqual(e['translation_change_J'],e['front_translation_J']+e['rear_drive_translation_J']+e['rear_lateral_translation_J']+e['resistance_J']+e['body_work_residual_J'],6)
                self.assertLess(abs(e['body_work_residual_J']),1)
                self.assertGreaterEqual(e['clutch_heat_J'],-1e-6);self.assertEqual(0,e['crank_drag_J'])
    def test_world_budget_and_every_first_intervention_prefix_are_verified(self):
        ledger=read(N/'budget.json');self.assertLessEqual(len(ledger),60);self.assertLess(sum(q['CpuSeconds'] for q in ledger),900)
        self.assertTrue(all(q['Complete'] for q in ledger));self.assertEqual(len(ledger),len(self.results))
        for name,r in self.results.items():
            self.assertTrue(r['PrefixExact'],name);self.assertEqual(r['PrefixFingerprint'],r['OriginalPrefixFingerprint'])
            self.assertEqual(r['RawSha256'],hashlib.sha256((N/(name+'.json.gz')).read_bytes()).hexdigest().upper())
            if r['Checkpoints']:
                c=r['Checkpoints'][0];self.assertEqual(c['NativeStateFingerprint'],c['OriginalStateFingerprint'],name)
        r=self.results['replay-K1'];self.assertEqual(r['Fingerprint'],r['FullOriginalFingerprint'])
    def test_legal_gain_and_late_failures_are_not_confused(self):
        self.assertEqual('completed',self.results['joint-grid-29']['Outcome']);self.assertEqual(3,self.results['joint-grid-29']['CompletedLaps'])
        self.assertEqual('confirmed footprint crossing',self.results['gas-partial-29']['Outcome'])
        self.assertEqual(2,self.results['steer-large-31']['CompletedLaps']);self.assertNotEqual('completed',self.results['steer-large-31']['Outcome'])
        self.assertEqual('confirmed footprint crossing',self.results['autonomous-two-command-K1']['Outcome'])
        self.assertEqual(0,self.results['autonomous-two-command-K1']['CompletedLaps'])
    def test_scores_reproduce_saved_candidates_and_bound_is_not_a_speed_limit(self):
        audit=read(N/'score-audit.json');self.assertEqual(0,audit['NativeWorlds']);self.assertEqual(9,len(audit['Audits']))
        self.assertTrue(all(q['maximumRecordedScoreDifference']==0 for q in audit['Audits']))
        q=next(q for q in audit['ForceBounds'] if q['RadiusMeters']==36)
        self.assertGreater(q['Demand90Newtons'],audit['loads']['TotalCapacityNewtons']);self.assertAlmostEqual(76.67527,q['AbsoluteUpperKph'],4)
        action=self.results['joint-grid-29']['Checkpoints'][0]['Override']
        d=next(q for q in audit['Audits'] if q['Run']=='K1' and abs(q['Time']-29)<1e-8)
        self.assertTrue(any(x['Action']==action and x['InOriginalGrid'] for x in d['Leaves']))
    def test_generated_native_loop_is_exactly_frozen_144_with_a_chooser_hook(self):
        subprocess.run([sys.executable,str(ROOT/'tools/corner-speed-loss/generate.py'),'--check'],check=True,capture_output=True)
        old=read(ROOT/'docs/experiments/standing-start-three-laps/native/study.json')
        for path,digest in old['Protocol']['Sources'].items():
            self.assertEqual(digest,hashlib.sha256((ROOT/path).read_text(encoding='utf-8').replace('\r\n','\n').encode()).hexdigest().upper(),path)
    def test_single_gas_change_and_delays_are_physical(self):
        for name in ['gas-partial-29','gas-full-29','exit-partial-338','exit-full-338']:
            c=self.results[name]['Checkpoints'][0]
            self.assertEqual(c['OriginalAction']['SteeringRadians'],c['Override']['SteeringRadians'])
        run=read(N/'joint-grid-29.json.gz');commands=run['Riders'][0]['Activations']
        for c in commands:self.assertAlmostEqual(.24,c['ActivationAt']-c['ObservedAt'],8)
        c=next(c for c in commands if abs(c['ObservedAt']-29)<1e-7)
        self.assertEqual(self.results['joint-grid-29']['Checkpoints'][0]['Override'],c['Action'])
    def test_final_source_manifest_matches_frozen_git_line_conventions(self):
        for path,digest in read(D/'provenance.json')['Sources'].items():
            self.assertEqual(digest,hashlib.sha256((ROOT/path).read_bytes()).hexdigest(),path)
    def test_every_saved_decision_and_candidate_has_an_exact_component_reconstruction(self):
        summary=read(N/'all-scores-summary.json');data=read(N/'all-scores.json.gz');rows=data['Audits']
        self.assertEqual(0,summary['NativeWorlds']);self.assertEqual(0,summary['MaximumScoreDifference'])
        self.assertEqual(summary['RawSha256'],hashlib.sha256((N/'all-scores.json.gz').read_bytes()).hexdigest().upper())
        self.assertEqual(1148,len(rows));self.assertEqual(31017,sum(len(d['Leaves']) for d in rows))
        for name in ['A','B','F','K1']:
            original=read(ROOT/'docs/experiments/standing-start-three-laps/native'/f'{name}.json.gz')['Riders'][0]['Decisions']
            actual=[d for d in rows if d['Run']==name]
            self.assertEqual(len(original),len(actual));self.assertEqual([d['Choice']['Time'] for d in original],[d['Time'] for d in actual])
        for d in rows:
            self.assertTrue(d['ChosenMatches']);self.assertEqual(1,sum(c['Selected'] for c in d['Leaves']))
            for c in d['Leaves']:
                self.assertEqual(0,c['ScoreDifference'])
                self.assertAlmostEqual(c['Score'],c['Terms']['Sum']-c['RemovedThrottle']-c['RemovedGeometry'],7)
        cost=read(D/'provenance.json');self.assertLess(cost['NativeReservationCpuSeconds']+cost['OfflineScoreAuditCpuSeconds']+summary['CpuSeconds'],900)

if __name__=='__main__':unittest.main()
