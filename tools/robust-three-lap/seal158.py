"""Seal original and derived evidence hashes after every native reservation closes."""
from analyze158 import R,D,read,write
import hashlib,sys,time

def sha(path):
 with path.open('rb') as stream:return hashlib.file_digest(stream,'sha256').hexdigest()

def main(tag):
 entered=time.process_time();cost=D/'analysis-costs'/f'{tag}.json'
 if cost.exists():raise FileExistsError('Unique seal label required')
 try:
  for ledger in D.rglob('budget.json'):assert all(r['Complete'] for r in read(ledger)),ledger
  a=read(D/'analysis.json');final=[r for r in a['trials'] if r['phase']=='final-v6' and r['validation']];assert len(final)==20
  paths=set()
  for pattern in ['native/*.json.gz','native/*-result.json','journals/*.json.gz','*.zip','diagnostics/*.json.gz','intervals/*.csv.gz','budget.json','scenarios-frozen.json','controller-freeze.json']:
   paths.update(D.rglob(pattern))
  paths.update(D.glob('*.json.gz'))
  paths.update(D/name for name in ['analysis.json','replay.json','trials.csv','six-bends.csv','straights.csv','source-proof.json','nominal-memo-proof.json','nominal-v6-repro-proof.json','scenarios-v6-independent.json','final-v6/scheduling.json','final-v6/binaries.json'])
  hashes={str(p.relative_to(R)).replace('\\','/'):sha(p) for p in sorted(paths)}
  write(D/'evidence-manifest.json',dict(Base='7c24c31dd8587889ff2459608c328fb865a541ef',ActualNativeWorlds=len(a['trials']),AllReservationsClosed=True,FinalNewTrials=sum(not r['baseline'] for r in final),FinalBaseTrials=sum(r['baseline'] for r in final),Files=hashes,Scope='Lossless actual worlds, complete decision archives, sources and derived metrics. No forecasts promoted. Reports, CI metadata and this manifest are excluded to avoid circular hashes.'))
  print('Sealed evidence files:',len(hashes))
 finally:write(cost,dict(Name=tag,CpuSeconds=time.process_time()-entered+1,NativeWorlds=0,Complete=True))
if __name__=='__main__':main(sys.argv[1] if len(sys.argv)>1 else 'seal-v1')
