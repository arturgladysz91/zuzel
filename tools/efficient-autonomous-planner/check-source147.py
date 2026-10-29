"""Check generated predictor equivalence and frozen runtime sources; no simulation."""
import argparse,hashlib,json,subprocess
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2];DOC=ROOT/'docs/experiments/efficient-autonomous-planner';BASE='9828656024eda4177d39f0a29d10d3f2c5287ff6';NATIVE='1607ae7'
def blob(commit,path):return subprocess.check_output(['git','show',commit+':'+path],cwd=ROOT)
def generated(source):
 s=source.decode('utf-8').replace('namespace SharedWorldSpeedway;','namespace Efficient147;').replace('public static class Predictor144','public static class CachedPredictor147').replace('Action<double,double>? observeCrank=null)','Action<double,double>? observeCrank=null,PredictionBoundary147? boundaryCache=null)').replace('new RacingLineGeometry(frame);','boundaryCache?.For(frame)??new RacingLineGeometry(frame);')
 return ('// Generated mechanically from frozen StandingStart144/Predictor144.cs. See source-proof.json; no physical formula changes.\nusing SharedWorldSpeedway;\n'+s).encode('utf-8')
def check(write=False):
 pred='tools/shared-world-speedway/StandingStart144/Predictor144.cs';geom='tools/shared-world-speedway/RacingLineGeometry.cs';cached='tools/efficient-autonomous-planner/CachedPredictor147.cs'
 assert (ROOT/pred).read_bytes()==blob(BASE,pred)
 assert (ROOT/geom).read_bytes()==blob(BASE,geom)
 assert (ROOT/cached).read_bytes()==generated(blob(BASE,pred)),'Generated performance adapter drifted from frozen equations'
 n=blob(NATIVE,pred).replace(b',RacingLineGeometry? boundaryCache=null',b'').replace(b'        if(boundaryCache is not null&&!boundaryCache.IsFor(frame))throw new ArgumentException("Cached boundary must belong to this exact immutable frame.");\n',b'').replace(b'boundaryCache??new RacingLineGeometry(frame)',b'new RacingLineGeometry(frame)')
 assert n==blob(BASE,pred)
 assert blob(NATIVE,geom).replace(b'    public bool IsFor(TrackFrame candidate)=>ReferenceEquals(frame,candidate);\n',b'')==blob(BASE,geom)
 paths=['src','data','docs/experiments/corner-speed-loss','docs/experiments/standing-start-three-laps','docs/experiments/autonomous-corner-planning']
 assert not subprocess.check_output(['git','diff',BASE,'--name-only','--']+paths,cwd=ROOT).strip()
 if write:
  sources=[ROOT/'tools/autonomous-corner-planning'/n for n in ['Planner146.cs','Model146.cs','ExecutionHooks146.cs']]+list((ROOT/'tools/efficient-autonomous-planner').glob('*.cs'))+[ROOT/pred,ROOT/geom]
  nativepaths=[str(p.relative_to(ROOT)).replace('\\','/') for p in sources if p.name not in ['CachedPredictor147.cs','PredictionBoundary147.cs','Finalize147.cs']]
  proof=dict(BaseCommit=BASE,NativeControlSourceCommit=NATIVE,PhysicsDirectoriesUnchanged=True,CheckedHistoricalPaths=paths,ChangedHistoricalFiles=[],FrozenPredictorAndBoundaryByteEqualToBase=True,GeneratedCachedPredictorByteEqualToFrozenEquations=True,MeasuredNativeEquationsMatchCurrentAdapter=True,SourceSha256={p.relative_to(ROOT).as_posix():hashlib.sha256(p.read_bytes()).hexdigest() for p in sources},NativeControlSourceSha256={p:hashlib.sha256(blob(NATIVE,p)).hexdigest() for p in nativepaths},PostTrialChanges='Windows journal read-sharing/finalization. To preserve raw historical source hashes, geometry reuse was moved to a mechanically generated performance adapter and its identity-owner wrapper. Equations, safety and control policy remain equal; exact prediction parity is tested. Native timing/fingerprint belongs to 1607ae7, not a second run.')
  (DOC/'source-proof.json').write_text(json.dumps(proof,indent=2)+'\n',encoding='utf-8',newline='\n')
 return True
if __name__=='__main__':
 ap=argparse.ArgumentParser();ap.add_argument('--write-manifest',action='store_true');args=ap.parse_args();check(args.write_manifest);print('PASS: frozen sources and generated-equation parity')
