"""Retain and classify the original 12 worlds by independent commit continuity."""
import argparse
import json
from pathlib import Path
import time
from zipfile import ZipFile


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('evidence',type=Path)
    e=parser.parse_args().evidence.resolve()
    began=time.process_time()
    receipts=[]
    for path in sorted(e.glob('*-result.json')):
        result=json.loads(path.read_bytes())
        with ZipFile(e/(result['Id']+'.zip')) as z:
            native=json.loads(z.read('native.json'))
        states=native['Riders'][0]['Samples']
        after=[s for s in states if s['Input']['Time']>result['Case']['BranchSeconds']+1e-8]
        wheel=max(abs(s['Powertrain']['Wheel']['AfterOmega']-s['Input']['Omega']) for s in after)
        steering=max(abs(s['Forces']['SteeringAfter']-s['Input']['Steering']) for s in after)
        assert wheel > .001 and steering > .0001
        receipts.append(dict(Id=result['Id'],MaxUncommittedWheelRadiansPerSecond=wheel,
                             MaxUncommittedSteeringRadians=steering,Status='INVALID_INTEGRATION'))
    assert len(receipts)==12
    payload=dict(InvalidWorlds=len(receipts),WorldsCountAgainstBudget=True,
                 CpuSeconds=time.process_time()-began,
                 Error='Contact169 returned detached wheel/steering results. The first harness committed stale Prepared fields.',
                 Correction='Commit the exact detached shaft wheel result and steering result as in NativeWorld169. Assert shaft, wheel and steer continuity each step.',
                 ReservePlan='Only two reserve worlds. Use the same archived X branch, frozen commands and original common goal for T35 and T55. Five S3 pairs remain sealed.',
                 Receipts=receipts)
    with (e/'invalid-integration.json').open('x',encoding='utf-8',newline='\n') as f:
        json.dump(payload,f,indent=2)
    closed=e/'closed.json'
    closed.rename(e/'v0-draft-closure.json')
    print(json.dumps({'invalid_worlds':len(receipts),'next':'repair two declared reserve worlds'}))


if __name__=='__main__':
    main()
