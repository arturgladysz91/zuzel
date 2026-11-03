from audit162 import R,D,read,save,sha
import subprocess

def tree(rev,p):return subprocess.check_output(['git','rev-parse',rev+':'+p],cwd=R,text=True).strip()
def main():
 base='85a5ee3295c2dd983fbacb21859649f794cc5ba5';f=read(R/'docs/experiments/trajectory-continuation/evidence-manifest.json')['FrozenTrees'].copy()
 for path in ['tools/trajectory-continuation','docs/experiments/trajectory-continuation']:f[path]=tree(base,path)
 for path,h in f.items():assert tree('HEAD',path)==h,path
 a=read(D/'analysis.json');paths=[p for p in D.rglob('*') if p.is_file() and p.name!='evidence-manifest.json' and not p.name.endswith('.lock')];paths+=[p for p in (R/'tools/s3-performance').iterdir() if p.is_file()];paths += [R/'tests/CoreSim.Tests/S3Performance162Tests.cs',R/'tests/calibration/test_s3_performance162.py']
 save(D/'evidence-manifest.json',dict(Base=base,PerformanceVerdict=a['performance_verdict'],NativeS3Verdict=a['preserved_s3'],Files={p.relative_to(R).as_posix():sha(p) for p in sorted(set(paths))},FrozenTrees=f));print('sealed',len(set(paths)),'files;',len(f),'unchanged trees')
if __name__=='__main__':main()
