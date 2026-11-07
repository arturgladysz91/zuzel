"""Phase A: verify P7 using sealed evidence only; register analysis CPU."""
from pathlib import Path
import gzip,hashlib,io,json,time,zipfile

def main():
    began=time.process_time();root=Path(__file__).resolve().parents[2]
    out=root.parent/'evidence174';out.mkdir(exist_ok=True)
    assert not (out/'archive-proof.json').exists()
    docs=root/'docs/experiments/throttle-decision-audit'
    hashes={name:hashlib.sha256((docs/name).read_bytes()).hexdigest() for name in ['173-diagnostics.zip','173-native.zip']}
    with zipfile.ZipFile(docs/'173-diagnostics.zip') as z:
        selected=json.loads(z.read('selected173.json'))
        (out/'selected173.json').write_bytes(z.read('selected173.json'))
        p7=next(s for s in selected if s['Id']=='P7')
        counter=json.loads(z.read('q1-counterfactual.json'))
        local=[r for r in counter['Rows'] if r['State']=='P7']
    trials={}
    with zipfile.ZipFile(docs/'173-native.zip') as outer:
        for member in outer.namelist():
            with zipfile.ZipFile(io.BytesIO(outer.read(member))) as z:
                trials[member]={'native':json.loads(z.read('native.json')),'branch':json.loads(z.read('branch.json')),'result':json.loads(z.read('result.json'))}
    # Compare physical trajectories only until the intervention becomes active.
    activation=p7['Input']['Time']+.24
    def physical(trial):
        rows=trial['native']['Riders'][0]['Samples']
        return [{k:s[k] for k in ['Input','CrankOmega','Forces','Powertrain']} for s in rows if s['Input']['Time']<activation-1e-8]
    def clean(rows):
        for s in rows:
            s['Input']={k:v for k,v in s['Input'].items() if k not in ['Pending','NextReview']}
        return rows
    prefixes=[clean(physical(t)) for t in trials.values()]
    assert all(p==prefixes[0] for p in prefixes)
    assert all(t['result']['ReachedStop'] for t in trials.values())
    proof={'Phase':'A','NativeWorlds':0,'Inputs':hashes,'P7':p7,'LocalForecasts':local,
           'PhysicalPrefixSamples':len(prefixes[0]),'ActivationAt':activation,
           'Branches':{n:{'Branch':t['branch'],'Result':t['result']} for n,t in trials.items()},
           'Distinction':'0.84-second forecast vs fixed speed/0.65 heuristic vs native full bend; subsequent commands were unchanged causal K4',
           'CpuSeconds':time.process_time()-began}
    (out/'archive-proof.json').write_text(json.dumps(proof,ensure_ascii=False,indent=2),encoding='utf-8')
    print(json.dumps({'phase':'A','prefix':len(prefixes[0]),'worlds':0,'cpu':proof['CpuSeconds']}))

if __name__=='__main__':main()
