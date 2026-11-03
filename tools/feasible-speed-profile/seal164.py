from analyze164 import R,D,BASE,read,save,sha
import subprocess
def main():
 old=read(R/'docs/experiments/instrumentation-cost/evidence-manifest.json');frozen=dict(old['FrozenTrees'])
 for p in ['tools/instrumentation-cost','docs/experiments/instrumentation-cost']:frozen[p]=subprocess.check_output(['git','rev-parse',BASE+':'+p],cwd=R,text=True).strip()
 paths=list(D.rglob('*'))+list((R/'tools/feasible-speed-profile').rglob('*'))+[R/'tests/CoreSim.Tests/FeasibleSpeed164Tests.cs',R/'tests/calibration/test_feasible_speed164.py']
 files={p.relative_to(R).as_posix():sha(p) for p in sorted(paths) if p.is_file() and not any(x in p.parts for x in ['bin','obj','__pycache__']) and p.name not in ['evidence-manifest.json','budget-journal.json.lock']}
 save(D/'evidence-manifest.json',dict(Base=BASE,ControllerReference=read(D/'budget-plan.json')['ControllerReference'],Verdict=read(D/'analysis.json')['verdict'],Files=files,FrozenTrees=frozen))
if __name__=='__main__':main()
