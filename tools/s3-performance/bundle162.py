from pathlib import Path
import hashlib,json,zipfile

def policies(path,verify=True):
 with zipfile.ZipFile(path) as z:
  index=json.loads(z.read('index.json'));assert index['Format']=='S3Evidence162-v1';expected={v['Key']:v for v in index['Policies']};seen=set()
  with z.open('policies.jsonl') as stream:
   for line in stream:
    assert line.endswith(b'}\n');marker=b',"payload":';offset=line.find(marker);assert offset>0
    key=json.loads(line[:offset]+b'}')['key'];payload=line[offset+len(marker):-2]
    if verify:
     assert key in expected and key not in seen;meta=expected[key];assert len(payload)==meta['Bytes'] and hashlib.sha256(payload).hexdigest().upper()==meta['SHA256'],key
    seen.add(key);yield key,json.loads(payload),payload
  if verify:assert seen==set(expected)

def read(path,name):
 with zipfile.ZipFile(path) as z:return json.loads(z.read(name))

def verify(path):
 count=sum(1 for _ in policies(path))
 with zipfile.ZipFile(path) as z:assert z.testzip() is None
 return count

if __name__=='__main__':
 import argparse
 p=argparse.ArgumentParser();p.add_argument('bundle');p.add_argument('--extract');a=p.parse_args();count=0
 for key,q,raw in policies(a.bundle):
  if a.extract:
   out=Path(a.extract).resolve();out.mkdir(parents=True,exist_ok=True);target=(out/(key+'.json')).resolve();assert target.parent==out;target.write_bytes(raw)
  count+=1
 print(json.dumps({'policies':count,'verified':True,'bundle_sha256':hashlib.sha256(Path(a.bundle).read_bytes()).hexdigest()}))
