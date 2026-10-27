"""Read-only frozen full-race evidence checks. Never creates a native world or runs training."""
import csv,gzip,hashlib,json,math,struct,subprocess,sys,unittest
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2];D=ROOT/'docs/experiments/standing-start-three-laps';N=D/'native'
def read(path):return json.loads(gzip.decompress(path.read_bytes()) if str(path).endswith('.gz') else path.read_bytes())
def sha(path):return hashlib.sha256(path.read_bytes()).hexdigest().upper()
class StandingStartEvidenceTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.study=read(N/'study.json');cls.runs={q['Name']:read(N/(q['Name']+'.json.gz')) for q in cls.study['Runs']};cls.shared=read(N/'shared-A.json.gz');cls.metrics=read(D/'lap-metrics.json')
    def test_main_and_superseded_sources_are_frozen_without_overwriting_history(self):
        p=self.study['Protocol'];self.assertEqual('a6b301c6bf8a2854a6963a849b3944b435734f60',p['BaseHead']);self.assertFalse(p['EnabledByDefault'])
        for name,digest in p['Sources'].items():self.assertEqual(digest,hashlib.sha256((ROOT/name).read_text(encoding='utf-8').replace('\r\n','\n').encode()).hexdigest().upper(),name)
        old=read(D/'preliminary/study.json');archive=read(D/'preliminary/source-snapshot.json.gz')
        for name,digest in old['Protocol']['Sources'].items():
            source=archive[name] if name in archive else (ROOT/name).read_text(encoding='utf-8').replace('\r\n','\n')
            self.assertEqual(digest,hashlib.sha256(source.encode()).hexdigest().upper(),name)
        subprocess.run([sys.executable,str(ROOT/'tools/standing-start-three-laps/generate.py'),'--check'],check=True,capture_output=True)
        self.assertEqual(0,p['Launch']['CrankDragNewtonMeterSeconds']);self.assertEqual(20,p['Launch']['ClutchViscosityNewtonMeterSeconds'])
    def test_all_eleven_native_attempts_are_retained_and_hash_verified(self):
        count=0
        for folder in [N,D/'preliminary']:
            study=read(folder/'study.json');self.assertTrue(all(q['Complete'] for q in study['Reservations']))
            self.assertEqual(len(study['Runs']),len(study['Reservations']));self.assertEqual(len(study['Runs']),len({q['Name'] for q in study['Runs']}))
            for q in study['Runs']:self.assertEqual(q['RawSha256'],sha(folder/(q['Name']+'.json.gz')));count+=1
        shared=read(N/'shared-summary.json');self.assertEqual(shared['RawSha256'],sha(N/'shared-A.json.gz'));count+=1
        self.assertEqual(11,count);self.assertEqual(11,read(D/'cost.json')['Worlds'])
        self.assertEqual(500.84375,read(D/'cost.json')['TotalReservationCpuSeconds'])
    def test_zero_speed_zero_wheel_and_no_body_motion_before_tape_reaction(self):
        for run in list(self.runs.values())+[self.shared]:
            for rider in run['Riders']:
                zero=rider['Samples'][0]['Input']['Rider'];self.assertEqual(0,zero['Speed']);self.assertEqual(0,zero['AngularVelocity'])
                for s in rider['Samples']:
                    if s['Input']['Time']>.24+1e-9:break
                    self.assertEqual(0,s['Input']['Rider']['Speed']);self.assertEqual(0,s['Input']['Omega']);self.assertEqual(zero['Position'],s['Input']['Rider']['Position'])
                self.assertTrue(any(s['Input']['Rider']['Speed']>0 for s in rider['Samples'] if s['Input']['Time']>.24))
        for run in self.runs.values():self.assertEqual(3.5,run['Riders'][0]['Spec']['OffsetMeters']);self.assertEqual(1,run['Riders'][0]['Gate'])
        self.assertEqual([.5,3.5,6.5,9.5],[r['Spec']['OffsetMeters'] for r in self.shared['Riders']])
    def test_exactly_three_certified_forward_crossings_no_initial_or_early_finish(self):
        for run in self.runs.values():
            rider=run['Riders'][0];self.assertEqual('completed',run['Outcome']);self.assertEqual(3,rider['CompletedLaps']);self.assertEqual([1,2,3],[c['Lap'] for c in run['Crossings']])
            samples={round(s['Input']['Time'],8):s for s in rider['Samples']}
            for c in run['Crossings']:
                goal=c['Lap']*run['LapMeters'];self.assertLess(c['BeforeReference'],goal);self.assertGreaterEqual(c['AfterReference'],goal)
                self.assertLess(c['BeforeTime'],c['Time']);self.assertLessEqual(c['Time'],c['AfterTime'])
                a=samples[round(c['BeforeTime'],8)]['Input']['Rider'];b=samples[round(c['AfterTime'],8)]['Input']['Rider']
                self.assertLess(math.hypot(b['Position']['X']-a['Position']['X'],b['Position']['Y']-a['Position']['Y']),.4)
                self.assertEqual(c['PendingCount'],len(samples[round(c['AfterTime'],8)]['Input']['Pending']))
            self.assertEqual(run['Crossings'][-1]['Time'],rider['FinishSeconds']);self.assertGreaterEqual(run['EndSeconds'],rider['FinishSeconds'])
            self.assertGreaterEqual(rider['Samples'][-1]['Input']['Rider']['TotalReferenceMeters'],3*run['LapMeters'])
    def test_repeat_covers_the_entire_physical_and_control_history(self):
        a=self.runs['A'];b=self.runs['A-repeat'];self.assertEqual(a['Fingerprint'],b['Fingerprint']);self.assertEqual(a['Riders'][0]['Samples'],b['Riders'][0]['Samples']);self.assertEqual(a['Crossings'],b['Crossings'])
        self.assertEqual(a['Riders'][0]['Activations'],b['Riders'][0]['Activations'])
    def test_crank_clutch_energy_and_causal_queue_are_continuous(self):
        for run in self.runs.values():
            rider=run['Riders'][0]
            for command in rider['Activations']:self.assertAlmostEqual(.24,command['ActivationAt']-command['ObservedAt'],8)
            for a,b in zip(rider['Samples'],rider['Samples'][1:]):
                p=b['Powertrain'];self.assertEqual(a['CrankOmega'],p['BeforeOmega']);self.assertEqual(b['CrankOmega'],p['AfterOmega'])
                self.assertEqual(a['Input']['Omega'],p['Wheel']['BeforeOmega']);self.assertEqual(b['Input']['Omega'],p['Wheel']['AfterOmega'])
                self.assertGreaterEqual(p['ClutchHeatJoules'],-1e-7);self.assertGreaterEqual(p['TransmissionLossJoules'],-1e-7);self.assertEqual(0,p['CrankDragWorkJoules'])
                self.assertLess(abs(p['EnergyResidualJoules']),1e-6);self.assertLessEqual(p['Wheel']['RearUtilization'],1.000001)
                # JSON writes the shortest decimal for a C# float; restore its actual binary32 value before checking a double torque.
                gas=struct.unpack('<f',struct.pack('<f',b['Input']['Active']['Drive']))[0]
                self.assertAlmostEqual(p['CrankTorque'],p['Engine']['CrankTorqueNewtonMeters']*gas,10)
    def test_lap_metrics_are_scalar_native_measurements_and_include_warm_laps(self):
        main={q['Run']:q for q in self.metrics if q['Run'] in ['A','B','F','K1']}
        self.assertEqual(4,len(main))
        for name,m in main.items():
            run=self.runs[name];cross=run['Crossings'];previous=0
            for i,lap in enumerate(m['Laps']):
                self.assertTrue(lap['Completed']);self.assertAlmostEqual(cross[i]['Time']-previous,lap['LapSeconds']);previous=cross[i]['Time']
                self.assertAlmostEqual(1,sum(lap['ActiveGasFractions']));self.assertAlmostEqual(1,sum(lap['RequestedGasFractions']))
                self.assertAlmostEqual(lap['PhysicalDistanceMeters']/lap['LapSeconds']*3.6,lap['MeanPhysicalSpeedKph'])
            self.assertEqual(.25,m['Launch']['FirstMotionSeconds']);self.assertEqual(35,m['Launch']['BeforeFirstBend']['Reference'])
        a=sum(q['LapSeconds'] for q in main['A']['Laps'][1:]);b=sum(q['LapSeconds'] for q in main['B']['Laps'][1:]);self.assertLess(1-b/a,.01)
        self.assertEqual('PASS',read(D/'analysis.json')['StageA']);self.assertEqual('PARTIAL',read(D/'analysis.json')['Verdict'])
    def test_full_csv_keeps_every_accepted_state_and_manifest(self):
        records={}
        for run in list(self.runs.values())+[self.shared]:
            for rider in run['Riders']:records[run['Name']+'-R'+str(rider['Spec']['Profile']['Id'])]=rider
        manifest=read(D/'telemetry-manifest.json');self.assertEqual(9,len(manifest))
        for q in manifest:
            path=D/q['File'];self.assertEqual(q['Sha256'],sha(path));self.assertEqual(q['Bytes'],path.stat().st_size)
            with gzip.open(path,'rt',encoding='utf-8') as f:rows=list(csv.DictReader(f))
            native=records[path.name[:-7]]['Samples'];self.assertEqual(len(native),len(rows));self.assertEqual(42,len(rows[0]));self.assertEqual(native[-1]['Input']['Time'],float(rows[-1]['time_s']))
    def test_shared_contact_and_failed_gate_remain_partial_and_not_a_solo_success(self):
        r=self.shared;self.assertEqual(4,len(r['Riders']));times=[s['Input']['Time'] for s in r['Riders'][0]['Samples']]
        for rider in r['Riders']:self.assertEqual(times,[s['Input']['Time'] for s in rider['Samples']]);self.assertEqual(0,rider['CompletedLaps']);self.assertIsNone(rider['FinishSeconds'])
        self.assertTrue(any(c['Kind']=='begin' and c['ImpulseNewtonSeconds']>0 for c in r['Contacts']))
        self.assertEqual('experimental beta45 envelope',r['Outcome']);self.assertEqual('experimental beta45 envelope',r['Riders'][2]['Outcome'])
        self.assertTrue(all(q['Outcome']=='interrupted by another rider stop' for i,q in enumerate(r['Riders']) if i!=2))
        summary=read(N/'shared-summary.json');self.assertLess(summary['MinimumNativeCapsuleGapMeters'],0);self.assertGreater(summary['NegativeGapSamples'],0)
        self.assertEqual('PARTIAL',read(D/'analysis.json')['StageB'])
    def test_predictor_agrees_with_native_body_wheel_crank_in_start_and_laps_two_three(self):
        p=read(N/'predictor-check.json');self.assertTrue(p['Passed']);self.assertEqual(0,p['NativeWorlds']);self.assertEqual(20,p['Forecasts'])
        self.assertTrue(all(q['Passed'] and q['Points']>=39 for q in p['Checks']));self.assertEqual(p['SourceSha256'],sha(ROOT/'tools/standing144-validation/Program.cs'))
if __name__=='__main__':unittest.main()
