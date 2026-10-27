"""Read-only evidence checks: no forecasts, native worlds or fitting in CI."""
import csv,gzip,hashlib,json,subprocess,sys,unittest
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2]
D=ROOT/'docs/experiments/racing-constraints-speed'
def read(name):
    p=D/name
    return json.loads(gzip.decompress(p.read_bytes()) if name.endswith('.gz') else p.read_bytes())
def sha(path):return hashlib.sha256(path.read_bytes()).hexdigest().upper()
class RacingConstraintsEvidenceTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):cls.r=read('results.json');cls.rows=cls.r['Native'];cls.logs=read('decision-logs.json.gz')
    def test_sources_base_and_historical_evidence_are_frozen(self):
        p=self.r['Protocol'];self.assertEqual('0da21bc92ee541b6611e19f1d4bd2e3866b283ca',p['BaseHead'])
        self.assertFalse(p['DefaultEnabled']);self.assertFalse(p['PhysicsChanged']);self.assertEqual(0,p['CemRuns'])
        for path,digest in p['Sources'].items():
            normalized=(ROOT/path).read_text(encoding='utf-8').replace('\r\n','\n').encode()
            self.assertEqual(digest,hashlib.sha256(normalized).hexdigest().upper(),path)
        for path,digest in self.r['Audit']['HistorySha256'].items():self.assertEqual(digest,sha(ROOT/path),path)
        subprocess.run([sys.executable,str(ROOT/'tools/racing-constraints/generate.py'),'--check'],check=True,capture_output=True)
        self.assertEqual(self.r['CombinationExtension']['ScriptSha256'],sha(ROOT/'tools/racing-constraints/extend-recovery.py'))
        self.assertEqual(self.r['ConfirmationLock']['ScriptSha256'],sha(ROOT/'tools/racing-constraints/lock-held.py'))
        self.assertEqual(self.r['ConfirmationLock']['MandatoryBCommitmentSha256'],sha(D/'held-B-commitment.json'))
    def test_unique_reservations_account_for_every_world_and_budget(self):
        reservations=self.r['Reservations'];cost=self.r['Cost']
        self.assertEqual(len(reservations),len({q['Name'] for q in reservations}));self.assertEqual(146,len(reservations))
        self.assertEqual({q['Name'] for q in reservations},{q['Name'] for q in self.rows})
        self.assertTrue(all(q['Complete'] for q in reservations));self.assertLessEqual(len(reservations),160)
        self.assertAlmostEqual(cost['CpuSeconds'],sum(q['CpuSeconds'] for q in reservations)+self.r['OfflineCpuSeconds'])
        self.assertLessEqual(cost['CpuSeconds'],1800)
        self.assertTrue(all(q['NumericalClockGatesPassed'] and q['DecisionCpu']['NoDecisionCpuCutoff'] for q in self.rows))
        self.assertEqual(0,self.r['Audit']['NewWorlds']);self.assertEqual(0,self.r['Audit']['NewForecasts'])
    def test_full_csv_and_lossless_logs_match_manifest(self):
        manifest=read('telemetry-manifest.json');self.assertEqual(len(self.rows),len(manifest))
        self.assertEqual(self.r['Packaging']['DecisionLogsSha256'],sha(D/'decision-logs.json.gz'))
        for q in manifest:
            p=D/'telemetry'/q['Name'];self.assertEqual(q['Bytes'],p.stat().st_size);self.assertEqual(q['Sha256'],sha(p))
        for q in self.rows:
            self.assertEqual(q['DecisionCpu']['Count'],len(self.logs[q['Name']]))
            self.assertNotIn('Decisions',q)
            self.assertTrue(q['Completed']==(q['FullTimeSeconds'] is not None))
    def test_precommitted_selection_and_held_families_are_distinct(self):
        self.assertTrue(self.r['CombinationLock']['BeforeCombinationWorlds']);self.assertTrue(self.r['ConfirmationLock']['BeforeAnyHeldWorld'])
        self.assertTrue(self.r['CombinationExtension']['BeforeAnyHeldWorld']);self.assertEqual(64,self.r['CombinationExtension']['BeforeWorldCount'])
        self.assertEqual(['F','K2','K1'],[q['Id'] for q in self.r['ConfirmationLock']['OriginalRuleTop3']])
        self.assertEqual(['F','K2','K1','B'],[q['Id'] for q in self.r['ConfirmationLock']['Winners']])
        held=[q for q in self.rows if q['Pool']=='held'];self.assertEqual(60,len(held))
        identities={tuple(q['Case']['Case']['Scenario'][k] for k in ['Speed','Offset','Start','OutwardSpeed','HeadingError','YawRate']) for q in held}
        self.assertEqual(8,len(identities));self.assertEqual(12,len({q['Family'] for q in held}))
        nominal=[q for q in self.rows if q['Name'] in ['D0-A','D1-A','H0-A','H1-A']]
        self.assertEqual(4,len(nominal));self.assertTrue(all(q['Exact142Fingerprint'] for q in nominal))
    def test_paired_results_do_not_promote_failed_or_duplicate_cases(self):
        summary={q['Variant']:q for q in read('held-summary.json')};pairs=read('held-pairs.json')
        self.assertEqual({'B':12,'F':10,'K1':10,'K2':11},{k:v['Complete'] for k,v in summary.items()})
        self.assertTrue(all(q['GainFraction'] is None and q['Seconds'] is None for q in pairs if not q['Complete']))
        self.assertAlmostEqual(.002923929888576496,summary['B']['FamilyMeanTimeGainFraction'])
        self.assertEqual('PARTIAL',read('analysis.json')['Verdict']);self.assertFalse(any(q['OrientationalFivePercentWithNoNewDnf'] for q in summary.values()))
    def test_detectors_predictor_and_repeatability_stay_separate(self):
        diagnostics=read('detector-diagnostics.json');self.assertEqual(8,len(diagnostics));self.assertTrue(all(q['SameNativePhysicalPrefix'] for q in diagnostics))
        self.assertFalse(any(q['Completed'] for q in diagnostics if q['Name'].startswith('beta-')))
        self.assertTrue(next(q for q in diagnostics if q['Name']=='refined-D0-G')['Completed'])
        predictor=read('predictor-check.json');self.assertTrue(predictor['Passed']);self.assertEqual(0,predictor['NewNativeWorlds']);self.assertEqual(18,len(predictor['Checks']))
        self.assertEqual(predictor['SourceSha256'],sha(ROOT/'tools/constraints-analysis143/Program.cs'))
        for v in ['A','F']:
            group=[q for q in self.rows if q['Name'].startswith('warm-') and q['Variant']['Id']==v]
            self.assertEqual(3,len(group));original=next(q for q in self.rows if q['Name']=='D0-'+v)
            self.assertTrue(all(q['NativeMetrics']['Fingerprint']==original['NativeMetrics']['Fingerprint'] for q in group))
    def test_native_csv_clock_queue_and_forces_are_finite(self):
        # Representative original, successful removal and failed removal, with all force intervals retained.
        import math
        for name in ['H0-A','H0-B','H4-b-F']:
            with gzip.open(D/'telemetry'/(name+'.csv.gz'),'rt') as f:rows=list(csv.DictReader(f))
            self.assertGreater(len(rows),100);self.assertEqual(37,len(rows[0]))
            self.assertTrue(all(math.isfinite(float(v)) for q in rows for v in q.values() if v!=''))
            self.assertTrue(all(float(b['time'])>float(a['time']) for a,b in zip(rows,rows[1:])))
            self.assertTrue(any(q['front_Fy_N']!='' and q['rear_Fx_N']!='' for q in rows))
if __name__=='__main__':unittest.main()
