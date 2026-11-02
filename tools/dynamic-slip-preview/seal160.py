"""Seal study160 evidence and verify prior complete trees without changing them."""
from analyze160 import R,D,read,save,digest
import subprocess
def tree(rev,path):return subprocess.check_output(['git','rev-parse',rev+':'+path],cwd=R,text=True).strip()
def main():
 base='9fb071ba3129aae88a6ec8c401d51a784be2da2b';prior=read(R/'docs/experiments/geometry-speed-audit/evidence-manifest.json');frozen=prior['FrozenTrees'].copy()
 for path in ['src','data','tools/robust-three-lap','tools/geometry-speed-audit','docs/experiments/geometry-speed-audit']:
  frozen[path]=tree(base,path)
 for path,value in frozen.items():assert tree('HEAD',path)==value,(path,value)
 files=[p for p in D.rglob('*') if p.is_file() and p.name!='evidence-manifest.json']
 files += [p for p in (R/'tools/dynamic-slip-preview').iterdir() if p.is_file()]
 files += [R/'tests/CoreSim.Tests/DynamicSlipPreview160Tests.cs',R/'tests/calibration/test_dynamic_slip160.py',R/'tools/replay-viewer/study160-model.mjs',R/'tools/replay-viewer/study160.test.mjs']
 a=read(D/'analysis.json')
 for row in a['native']:files += [R/row['pair']['baseline_source']]
 save(D/'evidence-manifest.json',dict(Base=base,ScientificVerdict=a['verdict'],NewNativeWorlds=a['new_native_worlds'],Files={str(p.relative_to(R)).replace('\\','/'):digest(p) for p in sorted(set(files))},FrozenTrees=frozen))
 print('sealed',len(set(files)),'files;',len(frozen),'unchanged trees')
if __name__=='__main__':main()
