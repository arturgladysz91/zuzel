"""Seal completed diagnostics and pin every historical input; never run a world."""
import json
from pathlib import Path
import time
from zipfile import ZipFile, ZIP_STORED, ZIP_DEFLATED
from io import BytesIO
from collections import Counter
from analyze173 import DOCS, OLD, ROOT, read, write, sha, queue_audit


def pack(evidence):
    began=time.process_time()
    old=read(OLD/'summary.json')
    names=set(old['freeze']['Sources'])|set(old['freeze']['PhysicsSha256'])
    names.update('docs/experiments/autonomous-fast-corner/evidence/'+x for x in ('fit.json','freeze.json'))
    names.update('docs/experiments/three-lap-global-planning/'+x for x in ('summary.json','SHA256SUMS',
        '172-validation-K4.zip','172-validation-C0.zip','172-development.zip','172-protocol.zip'))
    names.update(['docs/experiments/two-corner-full-drive/170-dowody.zip',
        'tools/shared-world-speedway/StandingStart144/CrankClutch144.cs',
        'tools/safe-fast-contingency/PredictorA153.Generated.cs'])
    write(DOCS/'INPUTS.json',dict(base='c5f0a4cc4de34fc4c8c47ef912cafabf34055f29',files={p:sha(ROOT/p) for p in sorted(names)}))
    development=[]
    with ZipFile(OLD/'172-development.zip') as outer:
        for name in outer.namelist():
            with ZipFile(BytesIO(outer.read(name))) as z:
                n=json.loads(z.read('native.json'));d=json.loads(z.read('decisions.json'));r=json.loads(z.read('result.json'))
                count=queue_audit(n,d)
                development.append(dict(trial=name,decisions=count,end_time=n['EndSeconds'],crossings=len(n['Crossings']),
                    s3=r['S3'],reserve=r['Reserve'],accepted_bends=r.get('AcceptedBends'),
                    archived_tracker_gates=dict(Counter(x['Tracking']['Gate'] for x in d)),
                    scope='archival chronology and queue audit only; earlier development controllers are not replayed as frozen #172'))
    write(DOCS/'DEVELOPMENT.json',development)
    with ZipFile(DOCS/'173-native.zip','w',ZIP_STORED) as archive:
        for p in sorted(evidence.glob('n1-*.zip')):archive.write(p,p.name)
    with ZipFile(DOCS/'173-diagnostics.zip','w',ZIP_DEFLATED) as archive:
        for p in sorted(evidence.rglob('*')):
            if p.is_file() and p.suffix!='.zip' and p.name!='.lock':archive.write(p,p.relative_to(evidence).as_posix())
    count=len(list(evidence.glob('packing-*.cpu.json')))+1
    p=evidence/f'packing-{count:02}.cpu.json'
    write(p,dict(CpuSeconds=time.process_time()-began,NativeWorlds=0))
    with ZipFile(DOCS/'173-diagnostics.zip','a',ZIP_DEFLATED) as archive:archive.write(p,p.name)
    print('Sealed diagnostics, four native arms and historical inputs.')


if __name__=='__main__':
    import sys
    pack(Path(sys.argv[1]))
