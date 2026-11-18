from hashlib import sha256
import json
import os
from pathlib import Path
import subprocess
import sys
import time

work=Path(__file__).resolve().parent
root=work.parents[1]
repo=root/'work/zuzel'
out=root/'outputs/badanie201'
archive=out/'stadium'
info=work/'server-info.json'
if info.exists():
    info.unlink()
env=os.environ.copy()
env['PATH']='C:/Program Files/dotnet;C:/Windows/System32;C:/Program Files/nodejs'
node='C:/Program Files/nodejs/node.exe'
chrome='C:/Program Files (x86)/Google/Chrome/Application/chrome.exe'
playwright='C:/Users/Administrator/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright'
arm=sys.argv[1] if len(sys.argv)>1 else 'integration'
replay=arm=='replay'
if not replay and len(list(out.glob('*.reserved')))>=5:
    raise RuntimeError('No native integration slot remains')
if not replay:
    with (out/(arm+'.reserved')).open('x',encoding='utf8') as file:
        file.write(json.dumps({'Arm':arm,'Controller':'speedway200',
            'Executable':sha256((repo/'tools/autonomous-traffic191/bin/Release/net8.0/autonomous-traffic191.dll').read_bytes()).hexdigest()}))
with (work/'server.log').open('wb') as log:
    server=subprocess.Popen([str(Path(os.__file__).parents[1]/'python.exe'),'-B','-X','utf8',
        str(repo/'tools/replay-viewer/serve.py'),'--no-browser','--archive',str(archive),'--info-file',str(info)],
        cwd=repo,env=env,stdout=log,stderr=log,creationflags=subprocess.CREATE_NO_WINDOW)
try:
    for _ in range(120):
        if info.is_file():break
        if server.poll() is not None:raise RuntimeError((work/'server.log').read_text(encoding='utf8'))
        time.sleep(.1)
    url=json.loads(info.read_text(encoding='utf8'))['url']
    subprocess.run([node,str(work/'integration201.mjs'),playwright,chrome,url,str(out),arm],check=True,timeout=130,env=env)
finally:
    server.terminate()
    server.wait(timeout=8)
