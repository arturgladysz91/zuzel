from forensics161 import R,D,read,save
import hashlib,subprocess
def digest(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def tree(rev,path):return subprocess.check_output(['git','rev-parse',rev+':'+path],cwd=R,text=True).strip()
def main():
 base='e8866e91c4c3818b8ca836260883ca847ccc4a8a';frozen=read(R/'docs/experiments/dynamic-slip-preview/evidence-manifest.json')['FrozenTrees'].copy()
 for path in ['tools/dynamic-slip-preview','docs/experiments/dynamic-slip-preview']:frozen[path]=tree(base,path)
 for path,h in frozen.items():assert tree('HEAD',path)==h,path
 paths=[p for p in D.rglob('*') if p.is_file() and p.name!='evidence-manifest.json'];paths += [p for p in (R/'tools/trajectory-continuation').iterdir() if p.is_file()]
 paths += [R/'tests/CoreSim.Tests/TrajectoryContinuation161Tests.cs',R/'tests/calibration/test_trajectory_continuation161.py',R/'tools/replay-viewer/study161.test.mjs']
 a=read(D/'analysis.json')
 for row in a['native']:paths.append(R/row['pair']['baseline_source'])
 save(D/'evidence-manifest.json',dict(Base=base,ScientificVerdict=a['verdict'],NewNativeWorlds=a['new_native_worlds'],Files={p.relative_to(R).as_posix():digest(p) for p in sorted(set(paths))},FrozenTrees=frozen));print('sealed',len(set(paths)),'files;',len(frozen),'unchanged trees')
if __name__=='__main__':main()
