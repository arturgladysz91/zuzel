"""Read-only integrity checks for the single #140 result; never repeat experiments."""
import hashlib
import json
from pathlib import Path
import subprocess
import sys
import unittest

ROOT=Path(__file__).resolve().parents[2]
RESULT=ROOT/'docs/experiments/tyre-law-regularization/results.json'


class TyreSeparationEvidenceTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.r=json.loads(RESULT.read_bytes())

    def test_frozen_sources_defaults_and_unique_complete_budget(self):
        r=self.r;p=r['Protocol']
        self.assertEqual('1a84e527536e0dc4e77eaa1acebd2866c1579593',p['BaseHead'])
        self.assertFalse(p['ProductionChanged']);self.assertFalse(p['DefaultEnabled'])
        self.assertTrue(p['FrozenBeforeRecordedMatricesAndNativeWorlds'])
        for path,digest in p['Sources'].items():
            actual=hashlib.sha256((ROOT/path).read_text(encoding='utf-8-sig').replace('\r\n','\n').encode()).hexdigest().upper()
            self.assertEqual(digest,actual,path)
        a=r['Reservations']
        self.assertEqual(len(a),len({x['Name'] for x in a}));self.assertTrue(all(x['Complete'] for x in a))
        self.assertEqual(12,sum(x['NativeWorlds'] for x in a));self.assertEqual(12,len(r['Native']))
        self.assertLessEqual(sum(x['CpuSeconds'] for x in a),450)
        self.assertAlmostEqual(sum(x['CpuSeconds'] for x in a),r['Cost']['CpuSeconds'])

    def test_material_invariance_and_distinct_finite_step_dynamics(self):
        m=self.r['Mathematics'];self.assertTrue(m['Passed'])
        rows=m['Inertia']['Rows'];self.assertEqual([.45,.9,1.8],[x['IwKgMetersSquared'] for x in rows])
        self.assertEqual(0,m['Inertia']['MaximumInertiaForceDifferenceNewtons'])
        self.assertEqual(1,len({(x['NewPure']['FxNewtons'],x['NewPure']['FyNewtons']) for x in rows}))
        self.assertEqual(3,len({x['NewStep']['AfterOmega'] for x in rows}))
        self.assertGreater(max(x['OldFxNewtons'] for x in rows)-min(x['OldFxNewtons'] for x in rows),400)

    def test_mathematical_energy_passivity_and_same_time_convergence(self):
        m=self.r['Mathematics'];g=m['PhysicsGrid'];f=m['Front']
        self.assertEqual(2592,g['Queries']);self.assertTrue(g['Passed']);self.assertLessEqual(g['MaximumNormalizedEnergyResidual'],1e-10)
        self.assertLessEqual(g['MaximumSolverResidualMetersPerSecond'],2e-5)
        self.assertLessEqual(g['MaximumRearUtilization'],1.000001)
        self.assertGreaterEqual(g['MinimumDissipationJoules'],-1e-6)
        self.assertTrue(f['Passed']);self.assertEqual(0,f['MaximumDtForceDifferenceNewtons'])
        self.assertLessEqual(f['MaximumLocalDriveNewtons'],.001)
        for row in m['Convergence']['Rows']:
            self.assertTrue(row['Converging'],row['Scenario'])
            for model in ['New','Old']:
                self.assertEqual([.02,.01,.005,.0025],[x['DtSeconds'] for x in row[model]])
                for x in row[model]:self.assertAlmostEqual(row['Initial']['Time']+.2,x['End']['Time'])
            errors=row['AdjacentStateErrorsMetersPerSecondEquivalent']
            self.assertTrue(errors[-1]<errors[0] or errors[0]<1e-5)

    def test_cached_old_fingerprints_and_off_prefix_are_retained(self):
        r=self.r
        manifest=json.loads((ROOT/'docs/experiments/motorcycle-physics-validation/native-manifest.json').read_bytes())
        lookup={x['Name']:x['Fingerprint'] for x in manifest}
        self.assertEqual(8,len(r['CachedOld']))
        for x in r['CachedOld']:self.assertEqual(lookup[x['Name']],x['FullCaptureFingerprint'])
        off=next(x for x in r['Native'] if x['Name']=='preflight-off')
        self.assertTrue(off['ExactHistoricalPrefix']);self.assertEqual(off['HistoricalPrefixSha256'],off['NewPrefixSha256'])
        new=next(x for x in r['Native'] if x['Name']=='preflight-new')['PredictionReplayCheck']
        self.assertLessEqual(new['PositionErrorMeters'],.003);self.assertLessEqual(new['OmegaErrorRadPerSecond'],.01)

    def test_native_held_pool_controls_and_every_outcome_are_present(self):
        r=self.r;self.assertTrue(r['ConfirmationLock']['FrozenBeforeConfirmationWorlds'])
        names={x['Name'] for x in r['Native']}
        for case in ['D0','D1','H0','H1']:
            for mode in ['long','short']:self.assertIn(case+'-new-'+mode,names)
        for x in r['Native']:
            self.assertTrue(x['EnergyLedgerPassed'],x['Name'])
            self.assertLessEqual(x['MaximumYawUpdateErrorRadiansPerSecond'],2e-5)
            self.assertLessEqual(x['MaximumReactionErrorSeconds'],1e-8)
            self.assertLessEqual(x['MaximumReviewIntervalErrorSeconds'],1e-8)
            self.assertTrue(x['ExactInitialPhysicalState'] or x['IntentionalInitialPerturbation'])
            if x['Metrics']['Outcome'] not in ['completed','probe']:self.assertEqual(2,len(x['LastTwo']))
        self.assertEqual(2,len([x for x in r['Native'] if x['Trace'] is not None]))

    def test_only_two_report_files_and_exact_isolation_substitutions(self):
        self.assertEqual({'README.md','results.json'},{x.name for x in RESULT.parent.iterdir() if x.is_file()})
        result=subprocess.run([sys.executable,str(ROOT/'tools/tyre-law-regularization/generate.py'),'--check'],capture_output=True,text=True)
        self.assertEqual(0,result.returncode,result.stdout+result.stderr)


if __name__=='__main__':unittest.main()
