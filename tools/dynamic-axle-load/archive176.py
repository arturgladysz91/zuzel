"""Read the sealed #175 transfer probes and #169 native comparisons; no rollout."""
import argparse
import csv
import gzip
import json
import math
from pathlib import Path
import statistics
import time
from zipfile import ZipFile

ROOT = Path(__file__).resolve().parents[2]
DOCS = ROOT / 'docs/experiments'


def dot(a, b):
    return a['X']*b['X'] + a['Y']*b['Y']


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('output',type=Path)
    args=parser.parse_args()
    began=time.process_time()
    with ZipFile(DOCS/'track-grip-root-cause/175-dowody.zip') as source:
        points=json.loads(gzip.decompress(source.read('contact-pairs.json.gz')))
    with ZipFile(DOCS/'full-drive-stability/169-dowody.zip') as source:
        prior=json.loads(source.read('measurements.json'))
    phase={'entry':'entry','middle':'middle','exit':'exit','exit-wheelspin':'high-spin',
           'straight':'straight','following':'following','prepare':'prepare'}
    rows=[]
    for point in points:
        state=point['Sample']['Input']['Rider']
        for trial in point['Results']:
            tag={'BASE':'BASE','F-load-35cm':'T35','F-load-55cm':'T55'}.get(trial['Id'])
            if tag is None:
                continue
            f=trial['Force']; w=trial['Power']['Wheel']; loads=f['TyreLoads']
            ur=dot(f['RearVelocity'],f['BodyForward'])
            norm=math.hypot(ur,.5)
            rows.append(dict(source=point['Source'],region=phase[point['Region']],variant=tag,
                             state_sha256=point['StateSha256'],time_s=point['Sample']['Input']['Time'],
                             reference_m=state['TotalReferenceMeters'],speed_kph=state['Speed']*3.6,
                             previous_ax=point['Ax'],Fz_front=loads['FrontNormalNewtons'],Fz_rear=loads['RearNormalNewtons'],
                             capacity_front=loads['FrontCapacityNewtons'],capacity_rear=loads['RearCapacityNewtons'],
                             Fx_rear=f['RearLongitudinal'],Fy_rear=f['RearLateral'],Fy_front=f['FrontLateral'],
                             front_util=abs(f['FrontLateral'])/loads['FrontCapacityNewtons'],
                             rear_util=w['RearUtilization'],kappa_before=w['InstantSlipRatio'],
                             kappa_mid=w['MidSlipVelocityMetersPerSecond']/norm,
                             delta_wheel_rad_s=w['AfterOmega']-w['BeforeOmega'],
                             slip_work_j=w['LongitudinalLossJoules'],beta_deg=math.degrees(state['SlideRadians']),
                             yaw_rate=state['AngularVelocity'],yaw_moment=f['NetTorque'],
                             steering_request=f['SteeringRequest'],steering_used=f['SteeringUsed'],
                             along_accel=trial['Along'],normal_accel=trial['Normal']))
    assert len(rows)==168
    for i in range(0,len(rows),3):
        group=rows[i:i+3]
        assert [r['variant'] for r in group]==['BASE','T35','T55']
        assert len({r['state_sha256'] for r in group})==1
        assert len({r['steering_used'] for r in group})==1
        assert all(abs(r['Fz_front']+r['Fz_rear']-147*9.81)<1e-6 for r in group)
    tables={}
    metrics=('Fz_front','Fx_rear','Fy_front','Fy_rear','rear_util','kappa_mid','slip_work_j','yaw_moment','along_accel')
    for region in phase.values():
        selection=[r for r in rows if r['region']==region]
        originals={r['source']:r for r in selection if r['variant']=='BASE'}
        tables[region]={tag:{k:statistics.median(r[k]-originals[r['source']][k]
                                                for r in selection if r['variant']==tag) for k in metrics}
                        for tag in ('T35','T55')}
    args.output.mkdir(parents=True,exist_ok=True)
    with (args.output/'stany-osi.csv').open('x',encoding='utf-8',newline='') as f:
        writer=csv.DictWriter(f,fieldnames=list(rows[0]))
        writer.writeheader()
        writer.writerows(rows)
    with (args.output/'B-archiwum.json').open('x',encoding='utf-8',newline='\n') as f:
        json.dump(dict(n=len(points),variant_rows=len(rows),median_deltas=tables,
                       prior_native_169=prior['trials'],cpu_seconds=time.process_time()-began),f,ensure_ascii=False,indent=2)
    print(json.dumps(tables))


if __name__=='__main__':
    main()
