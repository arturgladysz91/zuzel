"""Seal source and archive bytes without modifying historical studies."""
import json
from verify176 import DOC, ROOT, TOOL, sha


def main():
    names=[
        'docs/experiments/three-lap-global-planning/172-validation-K4.zip',
        'docs/experiments/three-lap-global-planning/172-protocol.zip',
        'docs/experiments/full-drive-stability/169-dowody.zip',
        'docs/experiments/two-corner-full-drive/170-dowody.zip',
        'docs/experiments/track-grip-root-cause/175-dowody.zip',
        'docs/experiments/whole-corner-planning/profile.json',
        'docs/experiments/autonomous-fast-corner/evidence/fit.json',
        'docs/experiments/autonomous-fast-corner/evidence/freeze.json',
        'tools/full-drive-stability/Contact169.cs',
        'tools/full-drive-stability/Predictor169.Generated.cs',
        'tools/full-drive-stability/NativeWorld169.Generated.cs',
        'tools/track-grip-root-cause/analyze175.py',
        'tools/three-lap-global-planning/analyze172.py',
    ]
    names += [p.relative_to(ROOT).as_posix() for p in (ROOT/'tools/three-lap-global-planning').glob('*.cs')]
    names += ['tools/shared-world-speedway/'+n for n in ('DynamicAxleLoad.cs','StandingStart144/Contact144.cs',
                'StandingStart144/CrankClutch144.cs','RearWheelDynamics.cs','LoadBasedTyreGrip.cs','TrackFrame.cs',
                'Engine500.cs','ExperimentalTrackShape.cs','TwoContactBikeDynamics.cs')]
    files=[]
    for name in sorted(set(names)):
        p=ROOT/name
        text=p.suffix in ('.cs','.py','.json')
        data=p.read_bytes()
        files.append(dict(path=name,text_lf=text,sha256=sha(data.replace(b'\r\n',b'\n') if text else data)))
    inputs=dict(base='2a32aec30a50b8640c8dcb34a7b573d12395fecb',reference='c5f0a4cc4de34fc4c8c47ef912cafabf34055f29',files=files)
    (DOC/'INPUTS.json').write_text(json.dumps(inputs,indent=2)+'\n',encoding='utf-8',newline='\n')
    paths=[p for p in DOC.iterdir() if p.is_file() and p.name!='SHA256SUMS']
    paths += [p for p in TOOL.iterdir() if p.is_file()]
    paths += [ROOT/'tests/calibration/test_dynamic_axle_load176.py',ROOT/'.github/workflows/study-176.yml']
    manifest=[sha(p.read_bytes())+'  '+p.relative_to(ROOT).as_posix() for p in sorted(paths)]
    (DOC/'SHA256SUMS').write_text('\n'.join(manifest)+'\n',encoding='utf-8',newline='\n')
    print(f'{len(files)} frozen inputs, {len(paths)} sealed artifacts.')


if __name__=='__main__':
    main()
