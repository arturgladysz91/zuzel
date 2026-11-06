"""Read-only verification of the sealed audit and its controlled native prefixes."""
import argparse
import gzip
import hashlib
import json
import math
from pathlib import Path
from zipfile import ZipFile
from analyze173 import ROOT, DOCS, OLD, read, load_archives
from finalize173 import native_audit


def geometry_check(path):
    actual=read(path)
    with ZipFile(DOCS/'173-diagnostics.zip') as z:expected=json.loads(z.read('g1-geometry.json'))
    def equal(a,b):
        if isinstance(a,dict):return a.keys()==b.keys() and all(equal(a[k],b[k]) for k in a)
        if isinstance(a,list):return len(a)==len(b) and all(equal(x,y) for x,y in zip(a,b))
        if isinstance(a,(float,int)) and not isinstance(a,bool):return math.isclose(a,b,rel_tol=1e-9,abs_tol=1e-8)
        return a==b
    assert equal(actual,expected)
    print('Six bends / eighteen sections retain original TrackFrame geometry; zero native worlds.')


def audit(evidence):
    for name,digest in read(DOCS/'INPUTS.json')['files'].items():
        assert hashlib.sha256((ROOT/name).read_bytes()).hexdigest()==digest,name
    summary=read(DOCS/'summary173.json')
    classified=[json.loads(x) for x in gzip.open(DOCS/'decisions173.jsonl.gz','rt',encoding='utf-8')]
    assert len(classified)==1438 and sum(r['phase']=='S3' for r in classified)==1413
    assert sum(r['phase']=='S3' and r['straight'] for r in classified)==505
    reconstructed=[]
    for path in sorted(evidence.glob('r1-*S*.jsonl.gz')):
        rows=[json.loads(x) for x in gzip.open(path,'rt',encoding='utf-8')]
        assert all(r['Matched'] and r['Reconstruction'] for r in rows)
        reconstructed+=rows
    assert len(reconstructed)==1438
    for phase in ('r1','q1','n1'):
        receipt=read(evidence/(phase+'.started.json'))
        for name,digest in receipt['Sources'].items():
            source=evidence/'source-snapshots'/phase/(Path(name).name+'.txt')
            assert hashlib.sha256(source.read_bytes()).hexdigest().upper()==digest,(phase,name)
    _,old,_=load_archives()
    results,_,proof=native_audit(evidence,old)
    assert results==summary['native'] and proof==summary['native_proof']
    forecast=read(evidence/'q1-counterfactual.json')
    assert forecast['NativeWorlds']==0 and len(forecast['Rows'])==180
    assert all(r['LocallyFeasible'] for r in forecast['Rows'])
    assert hashlib.sha256((evidence/'selected173.json').read_bytes()).hexdigest().upper()==forecast['SelectedSha256']
    assert len({r['InitialSha256'] for r in forecast['Rows']})==9
    for state in {r['State'] for r in forecast['Rows']}:
        assert len({r['InitialSha256'] for r in forecast['Rows'] if r['State']==state})==1
    receipts=[read(p) for p in evidence.glob('*.done.json')]
    assert sum(r['NativeWorlds'] for r in receipts)==4
    cpu=sum(r['CpuSeconds'] for r in receipts)+sum(read(p)['CpuSeconds'] for p in evidence.glob('*.cpu.json'))+read(evidence/'archive-analysis-cpu.json')['CpuSeconds']
    assert cpu<100 and read(evidence/'native-closed.json')['NativeWorlds']==4
    return dict(matched_decisions=1438,s3_decisions=1413,straight_decisions=505,forecasts=180,native_worlds=4,cpu_seconds=cpu)


if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('evidence',type=Path,nargs='?');parser.add_argument('--geometry',type=Path)
    args=parser.parse_args()
    if args.geometry:geometry_check(args.geometry)
    elif args.evidence:print(json.dumps(audit(args.evidence),indent=2))
    else:parser.error('Explicit evidence directory or --geometry required')
