"""Lossless packaging of CLOSED native policy journals; no physical stepping."""
from analyze158 import D,read,write
import hashlib,sys,time,zipfile

def main(tag):
 entered=time.process_time();cost=D/'analysis-costs'/f'{tag}.json'
 if cost.exists():raise FileExistsError('Unique pack label required')
 count=0
 try:
  for native in sorted(D.rglob('native/*.json.gz')):
   folder=native.parent.parent;label=native.name.removesuffix('.json.gz');ledger=read(folder/'budget.json');row=next(r for r in ledger if r['Name']==label)
   if not row['Complete']:continue
   directory=folder/'policies'/label;files=sorted(directory.glob('*.json.gz'));archive=directory.parent/(label+'.zip')
   if not files:
    if not archive.exists():raise FileNotFoundError('No full policies for '+label)
    continue
   if not archive.exists():
    with zipfile.ZipFile(archive,'x',compression=zipfile.ZIP_STORED) as z:
     for p in files:z.writestr(zipfile.ZipInfo(p.name,date_time=(1980,1,1,0,0,0)),p.read_bytes())
   with zipfile.ZipFile(archive) as z:
    assert sorted(z.namelist())==[p.name for p in files]
    for p in files:assert hashlib.sha256(z.read(p.name)).digest()==hashlib.sha256(p.read_bytes()).digest()
   count+=1
  print('Verified lossless closed-world policy archives:',count)
 finally:write(cost,dict(Name=tag,CpuSeconds=time.process_time()-entered+1,NativeWorlds=0,Complete=True,ArchivesVerified=count))
if __name__=='__main__':main(sys.argv[1] if len(sys.argv)>1 else 'pack-v1')
