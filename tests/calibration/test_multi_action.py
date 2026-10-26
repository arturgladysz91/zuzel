import gzip,hashlib,importlib.util,json,unittest
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2]
D=ROOT/'docs/experiments/multi-action-planning'
def read(name):
    p=D/name
    return json.loads(p.read_bytes() if p.exists() else gzip.decompress(Path(str(p)+'.gz').read_bytes()))
class MultiActionEvidenceTests(unittest.TestCase):
    def test_native_collector_only_adds_diagnostic_integration_hook(self):
        original=(ROOT/'tools/shared-world-speedway/RobustLearningExperiment.cs').read_text(encoding='utf-8')
        original=original[original.index('    public static RobustEpisode Execute('):original.index('    private static void Train(')]
        current=(ROOT/'tools/shared-world-speedway/MultiActionNative135.cs').read_text(encoding='utf-8')
        current=current[current.index('    public static RobustEpisode Execute('):current.index('    private static void SaveEpisode(')]
        current=current.replace('TrackShapeVariant? trackShape=null,bool trackAwareness=false,Func<FreeRacingInput,FreeLearningDecision>? chooser=null){','TrackShapeVariant? trackShape=null,bool trackAwareness=false){')
        current=current.replace('if(chooser is not null||interveneAt is not null||horizonSeconds!=.6','if(interveneAt is not null||horizonSeconds!=.6').replace('            if(chooser is not null)return chooser(input);\n','')
        self.assertEqual(original,current)
    def test_frozen_scientific_sources_and_separate_validation(self):
        p=read('protocol.json');self.assertEqual(p['BaseHead'],'999b08aa34ffed09d7ade720ee1f6f2c1422b5f4');self.assertEqual(p['PolicyId'],16015)
        canonical=read('source-line-endings.json')['Sources']
        for name,digest in p['Sources'].items():
            b=(ROOT/'tools/shared-world-speedway'/name).read_bytes();self.assertEqual(canonical[name]['OriginalRawSha256'],digest)
            self.assertEqual(hashlib.sha256(b.replace(b'\r\n',b'\n')).hexdigest().upper(),canonical[name]['LFContentSha256'],name)
        self.assertEqual(len(read('diagnostic-states.json')),16);self.assertEqual(len(read('confirm-states.json')),6)
        a={s['Case']['Name'] for s in read('diagnostic-states.json')};b={s['Case']['Name'] for s in read('confirm-states.json')};self.assertFalse(a&b)
        self.assertEqual(read('diagnostic-complete.json')['SourceHash'],read('confirm-complete.json')['SourceHash'])
    def test_every_world_and_all_failures_are_counted(self):
        ledger=read('attempts.json');self.assertTrue(all(e['Complete'] for e in ledger));self.assertLessEqual(sum(e['NativeWorlds'] for e in ledger),96)
        self.assertLessEqual(sum(e['CpuSeconds'] for e in ledger),read('budget-freeze.json')['CpuLimitSeconds']);self.assertLessEqual(sum(e['Forecasts'] for e in ledger),250000);self.assertLessEqual(sum(e['Evaluations'] for e in ledger),25000000)
        pairs=read('paired-results.json');self.assertEqual(len(pairs),66);self.assertTrue(all(r['ExactPrefix'] and r['RequestsBeforeStopExact'] for r in pairs));self.assertTrue(all(r['MatchedCommandPrefixAccuracy']['Passed'] for r in pairs))
        audit=read('postflight-request-audit.json');self.assertTrue(audit['Passed']);self.assertTrue(any(not r['OriginalAllActivationsRequiredPassed'] for r in audit['Checks']));self.assertTrue(read('confirm-pairs-initial.json'))
        self.assertEqual(hashlib.sha256((ROOT/'tools/multi-action-planning/FinishAudit.cs').read_bytes()).hexdigest().upper(),audit['AuditSourceSha256'])
        for r in pairs:
            if not r['Native']['Native']['Completed']:self.assertIsNone(r['Native']['Native']['GateSeconds']);self.assertIsNone(r['GainTwoSeconds'])
    def test_search_branch_caps_reaction_and_no_oracle_selection(self):
        for category in ['diagnostic','confirm']:
            rows=read(category+'-search.json')
            for r in rows:
                s=r['Search'];self.assertLessEqual(len(s['Two']),12);self.assertLessEqual(len(s['Three']),24)
                best=lambda arr:min(arr,key=lambda q:(q['Evaluation']['Safety'],q['Evaluation']['Score']))
                self.assertEqual(best(s['Two'])['Evaluation']['Score'],min(q['Evaluation']['Score'] for q in s['Two'] if q['Actions']==r['ChosenTwo']))
                self.assertEqual(best(s['Three'])['Evaluation']['Score'],min(q['Evaluation']['Score'] for q in s['Three'] if q['Actions']==r['ChosenThree']))
                self.assertNotIn('Native',s);self.assertEqual(len(r['ChosenTwo']),2);self.assertEqual(len(r['ChosenThree']),3)
        preflight=read('preflight.json');self.assertTrue(preflight['Passed']);self.assertTrue(preflight['Off134Exact']);self.assertTrue(preflight['NativeRepeatExact']);self.assertTrue(preflight['PredictionsPassed'])
        for actual,expected in zip(preflight['Activations'],[.24,.44,.64]):self.assertAlmostEqual(actual,expected,8)
    def test_readonly_geometry_width_and_discrepancy(self):
        for g in read('preflight.json')['Geometry']:
            self.assertEqual(g['StartToBend'],35);self.assertEqual(g['ClosingStraight'],27);self.assertEqual(g['BackStraight'],62)
            if g['Geometry']=='progressive-b':self.assertAlmostEqual(g['MaximumFirstBendWidth'],16.6,4);self.assertAlmostEqual(g['MaximumSecondBendWidth'],16.6,4)
            if g['Geometry']=='progressive-c':self.assertAlmostEqual(g['MaximumFirstBendWidth'],17,4);self.assertAlmostEqual(g['MaximumSecondBendWidth'],16.2,4)
if __name__=='__main__':unittest.main()
