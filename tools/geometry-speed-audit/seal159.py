"""Seal immutable159 evidence and original inputs. Does not invent test results."""
from pathlib import Path
import hashlib,json,subprocess
from analyze159 import ROOT,DOC,read,save

def main():
    files=[p for p in DOC.rglob('*') if p.is_file() and p.name!='evidence-manifest.json']
    files += [p for p in (ROOT/'tools/geometry-speed-audit').iterdir() if p.is_file()]
    files += [ROOT/'tools/replay-viewer/study159-model.mjs',ROOT/'tools/replay-viewer/study159.test.mjs',ROOT/'tests/CoreSim.Tests/GeometryAudit159Tests.cs',ROOT/'tests/calibration/test_geometry_speed159.py']
    for row in read(DOC/'analysis.json')['native']:files.append(ROOT/row['source'])
    entries={str(p.relative_to(ROOT)).replace('\\','/'):hashlib.sha256(p.read_bytes()).hexdigest() for p in sorted(set(files))}
    base='9c94a21e37877fd82565fd309fd2850aeb23f341'
    frozen=read(ROOT/'docs/experiments/robust-three-lap/source-proof.json')['FrozenTrees']
    for path,tree in frozen.items():
        assert subprocess.check_output(['git','rev-parse',base+':'+path],cwd=ROOT,text=True).strip()==tree
    save(DOC/'evidence-manifest.json',dict(Base=base,Files=entries,FrozenTrees=frozen,
        Study158SourceTree=subprocess.check_output(['git','rev-parse',base+':tools/robust-three-lap'],cwd=ROOT,text=True).strip(),
        NewNativeWorlds=0,ScientificVerdict='PARTIAL'))
    print('Sealed',len(entries),'files and',len(frozen),'frozen trees')

if __name__=='__main__':main()
