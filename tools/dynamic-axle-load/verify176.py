"""Independent archive verification of the two corrected native worlds."""
import hashlib
import io
import json
import math
from pathlib import Path
import struct
import subprocess
import sys
from zipfile import ZipFile

ROOT = Path(__file__).resolve().parents[2]
DOC = ROOT/'docs/experiments/dynamic-axle-load'
TOOL = ROOT/'tools/dynamic-axle-load'
sys.path.insert(0,str(ROOT/'tools/three-lap-global-planning'))
from analyze172 import queue_audit


def sha(data):
    return hashlib.sha256(data).hexdigest()


def f32(x):
    return struct.unpack('<f',struct.pack('<f',x))[0]


def sample_check(a,b,c,height):
    # a -> b is the previous accepted interval. b -> c is the solved interval.
    p=c['Powertrain']; wheel=p['Wheel']; f=c['Forces']; loads=f['TyreLoads']
    a,b,c=(q['Input'] for q in (a,b,c))
    va,vb,vc=(q['Rider']['Velocity'] for q in (a,b,c))
    h=f32(b['Rider']['Heading'])
    dt=f32(b['Time']-a['Time'])
    ax=((f32(vb['X'])-f32(va['X']))*math.cos(h)+(f32(vb['Y'])-f32(va['Y']))*math.sin(h))/dt
    length=f32(.7)+f32(.6)
    rear=147*9.81*f32(.7)/length+147*ax*height/length
    assert abs(rear-loads['RearNormalNewtons']) < 1e-5
    assert abs(loads['FrontNormalNewtons']+loads['RearNormalNewtons']-147*9.81) < 1e-7
    assert loads['FrontNormalNewtons'] > 0 and loads['RearNormalNewtons'] > 0
    assert abs(loads['EffectiveFrictionCoefficient']-1.2845000267028808)<1e-9
    assert math.hypot(f['RearLongitudinal'],f['RearLateral']) <= loads['RearCapacityNewtons']+.001
    assert abs(f['FrontLateral']) <= loads['FrontCapacityNewtons']+.001
    assert abs(wheel['AfterOmega']-c['Omega']) < 1e-8
    assert abs(wheel['BeforeOmega']-b['Omega']) < 1e-8
    assert f['SteeringBefore'] == b['Steering'] and f['SteeringAfter'] == c['Steering']
    assert wheel['LongitudinalLossJoules']>=-1e-6 and p['ClutchHeatJoules']>=-1e-6
    assert abs(p['EnergyResidualJoules'])<1e-6
    # The actual Box2D impulse is one application of the recorded contact
    # forces and yaw moment, within float accumulation error.
    for axis in ('X','Y'):
        assert abs(f32(vc[axis])-f32(vb[axis])-f['NetForce'][axis]*p['Seconds']/147) < 4e-5
    assert abs(c['Rider']['AngularVelocity']-b['Rider']['AngularVelocity']-
               f['NetTorque']*p['Seconds']/52.72097) < 4e-5


def verify():
    for line in (DOC/'SHA256SUMS').read_text(encoding='utf-8').splitlines():
        digest,name=line.split('  ',1)
        assert sha((ROOT/name).read_bytes())==digest,name
    inputs=json.loads((DOC/'INPUTS.json').read_bytes())
    for item in inputs['files']:
        data=(ROOT/item['path']).read_bytes()
        if item['text_lf']:
            data=data.replace(b'\r\n',b'\n')
        assert sha(data)==item['sha256'],item['path']
    subprocess.run([sys.executable,str(TOOL/'generate176.py'),'--check'],check=True,capture_output=True,timeout=30)
    summary=json.loads((DOC/'summary176.json').read_bytes())
    assert summary['verdict']=='PARTIAL' and summary['s3']=='UNKNOWN'
    assert len(summary['held'])==5 and all(q['BASE']==q['NEW']=='NOT_RUN' for q in summary['held'])
    with ZipFile(DOC/'176-protokol.zip') as z:
        closed=json.loads(z.read('closed.json'))
        assert closed['Worlds']==14 and closed['InvalidWorlds']==12 and closed['ValidationWorlds']==0
        assert closed['HeldUnconsumed']
        assert sha(z.read('held.json'))==closed['HeldSha256']
        assert len([n for n in z.namelist() if n.endswith('.reserved.json')])==14
        bad=json.loads(z.read('invalid-integration.json'))
        assert len(bad['Receipts'])==12
        for stage in ('prepare','tape','live','aware','repair'):
            receipt=json.loads(z.read(stage+'.started.json'))
            with ZipFile(io.BytesIO(z.read(stage+'-source.zip'))) as snapshots:
                for name, digest in receipt['Sources'].items():
                    assert sha(snapshots.read(Path(name).name)).upper()==digest
                if stage=='repair':
                    for name in ('NativeWorld176.Generated.cs','AxleContact176.cs','Program.cs'):
                        assert snapshots.read(name)==(TOOL/name).read_bytes()
    invalid=0
    for stage in ('tape','live','aware'):
        with ZipFile(DOC/('176-'+stage+'.zip')) as outer:
            for name in outer.namelist():
                with ZipFile(io.BytesIO(outer.read(name))) as z:
                    run=json.loads(z.read('native.json'))
                    trial=json.loads(z.read('trial.json'))
                rows=run['Riders'][0]['Samples']
                branch=trial['Case']['BranchSeconds']
                error=max(abs(q['Powertrain']['Wheel']['AfterOmega']-q['Input']['Omega'])
                          for q in rows if q['Input']['Time']>branch+1e-8)
                sealed=next(q for q in bad['Receipts'] if q['Id']==trial['Id'])
                assert error==sealed['MaxUncommittedWheelRadiansPerSecond'] and error>.001
                invalid+=1
    assert invalid==12
    with ZipFile(ROOT/'docs/experiments/three-lap-global-planning/172-validation-K4.zip') as old:
        with ZipFile(io.BytesIO(old.read('v1-K4-S2.zip'))) as z:
            original=json.loads(z.read('native.json'))
            original_commands=json.loads(z.read('decisions.json'))
    count=0
    prefix_hashes=set()
    with ZipFile(DOC/'176-repair.zip') as outer:
        assert set(outer.namelist())=={'repair-X-T35.zip','repair-X-T55.zip'}
        for name in outer.namelist():
            with ZipFile(io.BytesIO(outer.read(name))) as z:
                native=json.loads(z.read('native.json'))
                result=json.loads(z.read('trial.json'))
                commands=json.loads(z.read('decisions.json'))
            queue_audit(native,commands)
            assert not result['Outcome']['ReachedStop'] and not result['Outcome']['S3']
            assert result['Outcome']['Reason']=='swept below 0.5 m'
            assert not native['Contacts']
            assert all(a['Action']==b['Action'] for a,b in zip(commands,original_commands))
            rows=native['Riders'][0]['Samples']
            t=result['Case']['BranchSeconds']
            prior=[p for p in rows if p['Input']['Time']<t-1e-8]
            assert prior==original['Riders'][0]['Samples'][:len(prior)]
            prefix_hashes.add(result['BranchHash'])
            last=rows[-1]
            assert last['Certificate']<.5
            assert (last['Inner']<last['Outer']) == (result['Height']==.35)
            assert all(q['Certificate']>=.5 for q in rows[1:-1])
            for j in range(2,len(rows)):
                if rows[j]['Input']['Time']>t+1e-8:
                    sample_check(rows[j-2],rows[j-1],rows[j],result['Height'])
                    count+=1
            info=next(q for q in summary['native'] if q['trial']['Id']==result['Id'])
            assert abs(info['new_ledger']['energy_J']['closure_residual'])<3
    assert len(prefix_hashes)==1 and None not in prefix_hashes
    return dict(worlds=14,invalid_integration=12,corrected=2,load_intervals=count,
                legal_full_bends=0,validation_worlds=0,s3='UNKNOWN',verdict='PARTIAL')


if __name__=='__main__':
    print(json.dumps(verify()))
