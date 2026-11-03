from pathlib import Path
import gzip,json,hashlib,tarfile,time
R=Path(__file__).resolve().parents[2];S=R/'docs/experiments/trajectory-continuation';D=R/'docs/experiments/s3-performance'

def main():
 # Compression/storage benchmark only: reading original bytes, no physics.
 import io,tempfile
 files=sorted((S/'policies/held-3').glob('*.json.gz'));before=sum(p.stat().st_size for p in files);start=time.process_time();inventory=[]
 with tempfile.TemporaryFile() as f:
  with gzip.GzipFile(fileobj=f,mode='wb',mtime=0,compresslevel=6) as compressed:
   with tarfile.open(fileobj=compressed,mode='w|') as tar:
    for p in files:
     data=gzip.decompress(p.read_bytes());info=tarfile.TarInfo(p.stem);info.size=len(data);info.mtime=0;tar.addfile(info,io.BytesIO(data));inventory.append(dict(name=p.stem,source=p.relative_to(R).as_posix(),gzip_sha256=hashlib.sha256(p.read_bytes()).hexdigest(),payload_sha256=hashlib.sha256(data).hexdigest(),bytes=len(data)))
  after=f.tell();f.seek(0);restored=0
  with gzip.GzipFile(fileobj=f,mode='rb') as compressed:
   with tarfile.open(fileobj=compressed,mode='r|') as tar:
    for member,meta in zip(tar,inventory):
     payload=tar.extractfile(member).read();assert member.name==meta['name'] and hashlib.sha256(payload).hexdigest()==meta['payload_sha256'];restored+=1
  assert restored==len(files)
 result=dict(scope='Read-only consolidation test on existing held-3 policies; no simulation, no deletion or replacement of original files. Exact decompressed UTF8 bytes preserved, original gzip-container hashes retained as provenance.',files_before=len(files),files_after=1,bytes_before=before,bytes_after=after,reduction_percent=100*(before-after)/before,roundtrip_exact_payloads=restored,cpu_s=time.process_time()-start,inventory=inventory)
 (D/'storage161.json').write_text(json.dumps(result,indent=2)+'\n',encoding='utf-8');print(json.dumps({k:v for k,v in result.items() if k!='inventory'}))
if __name__=='__main__':main()
