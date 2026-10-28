"""Read saved native evidence only. Never creates worlds or changes #144/#145 data."""
import argparse,bisect,csv,gzip,hashlib,json,math,statistics
from collections import Counter
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2]
DOC=ROOT/'docs/experiments/autonomous-corner-planning'

def read(p):
    return json.loads(gzip.decompress(p.read_bytes()) if p.name.endswith('.gz') else p.read_bytes())
def save(p,v):p.write_text(json.dumps(v,ensure_ascii=False,indent=2,allow_nan=False)+'\n',encoding='utf-8')
def ref(s):return s['Input']['Rider']['TotalReferenceMeters']
def time(s):return s['Input']['Time']
def percentile(values,f):return sorted(values)[min(len(values)-1,int((len(values)-1)*f))] if values else None

def analyze(run,starts):
    ss=run['Riders'][0]['Samples'];refs=[ref(s) for s in ss];L=run['LapMeters']
    def crossing(target):
        k=bisect.bisect_left(refs,target)
        if k>=len(ss) or target<refs[0]:return None
        a,b=ss[max(0,k-1)],ss[k];f=(target-ref(a))/(ref(b)-ref(a)) if ref(b)>ref(a) else 1
        va,vb=a['Input']['Rider'],b['Input']['Rider']
        lerp=lambda x,y:x+(y-x)*f
        return dict(time_s=lerp(time(a),time(b)),speed_kph=lerp(va['Speed'],vb['Speed'])*3.6,
            distance_m=lerp(va['DistanceMeters'],vb['DistanceMeters']),offset_m=lerp(va['Road']['OffsetMeters'],vb['Road']['OffsetMeters']))
    rows=[]
    for lap in range(1,4):
        for bend,(ei,xi) in enumerate([(1,4),(5,8)],1):
            lo,hi=(lap-1)*L+starts[ei],(lap-1)*L+starts[xi];entry,exit=crossing(lo),crossing(hi)
            row=dict(run=run['Name'],lap=lap,bend=bend,complete=bool(entry and exit),seconds=None,entry_kph=entry['speed_kph'] if entry else None,
                minimum_kph=None,exit_kph=None,path_m=None,gas0_s=None,gas_partial_s=None,gas1_s=None,beta_abs_max_deg=None,yaw_abs_max_rads=None,
                offset_min_m=None,offset_max_m=None,inner_min_m=None,outer_min_m=None)
            if entry and exit:
                selected=[s for s in ss if lo<=ref(s)<=hi];riders=[s['Input']['Rider'] for s in selected]
                gas=[0,0,0]
                for a,b in zip(ss,ss[1:]):
                    dt=max(0,min(time(b),exit['time_s'])-max(time(a),entry['time_s']))
                    if dt:gas[0 if b['Input']['Active']['Drive']==0 else 2 if b['Input']['Active']['Drive']==1 else 1]+=dt
                row.update(seconds=exit['time_s']-entry['time_s'],minimum_kph=min([entry['speed_kph'],exit['speed_kph']]+[v['Speed']*3.6 for v in riders]),exit_kph=exit['speed_kph'],
                    path_m=exit['distance_m']-entry['distance_m'],gas0_s=gas[0],gas_partial_s=gas[1],gas1_s=gas[2],
                    beta_abs_max_deg=max(abs(v['SlideRadians'])*180/math.pi for v in riders),yaw_abs_max_rads=max(abs(v['AngularVelocity']) for v in riders),
                    offset_min_m=min(v['Road']['OffsetMeters'] for v in riders),offset_max_m=max(v['Road']['OffsetMeters'] for v in riders),
                    inner_min_m=min(s['Inner'] for s in selected),outer_min_m=min(s['Outer'] for s in selected))
            rows.append(row)
    return rows

def forecast_audit(plans,run):
    decisions=run['Riders'][0]['Decisions'];activations=run['Riders'][0]['Activations'];rows=[]
    for k,p in enumerate(plans):
        selected=p.get('Selected');t=p['Time'];actual=next((d['Choice']['Action'] for d in decisions if abs(d['Choice']['Time']-t)<1e-7),None)
        activation=next((a for a in activations if abs(a['ObservedAt']-t)<1e-7),None)
        next_plan=plans[k+1] if k+1<len(plans) else None
        rows.append(dict(time_s=t,reason=p['Reason'],goal_m=p['GoalMeters'],candidate_count=p['Candidates'],cpu_s=p['CpuSeconds'],forecasts=p['Forecasts'],contacts=p['Contacts'],
            selected=selected['Actions'][:3] if selected else None,planned_goal_time_s=selected['GoalTime'] if selected else None,
            planned_exit_time_s=selected['CornerExitTime'] if selected else None,planned_exit_kph=selected['CornerExitSpeed']*3.6 if selected and selected['CornerExitSpeed'] is not None else None,
            forecast_legal=selected['Safety']['Legal'] if selected else None,actual_action=actual,actual_activation=activation,
            submitted_matches=not selected or actual==selected['Actions'][0],activation_matches=not activation or selected is not None and activation['Action']==selected['Actions'][0] and abs(activation['ActivationAt']-t-.24)<1e-7,
            next_followed=next_plan['FollowedExpected'] if next_plan else None,next_change_reason=next_plan['Reason'] if next_plan and not next_plan['FollowedExpected'] else None,
            prediction_error=p['PredictionError']))
    errors=[p['PredictionError'] for p in plans if p['PredictionError']]
    costs=[p['CpuSeconds'] for p in plans]
    return dict(reviews=len(plans),reason_counts=dict(Counter(p['Reason'] for p in plans)),selected_reviews=sum(p['Selected'] is not None for p in plans),
        followed_expected=sum(p['FollowedExpected'] for p in plans if p['ExpectedAction'] is not None),expected_reviews=sum(p['ExpectedAction'] is not None for p in plans),
        submitted_match=all(p['submitted_matches'] for p in rows),activation_match=all(p['activation_matches'] for p in rows),
        maximum_error={key:max(p[key] for p in errors) for key in ['PositionMeters','SpeedMps','BetaRadians','YawRate','WheelOmega','CrankOmega']} if errors else {},
        queues_match=all(p['QueueMatches'] and p['ActiveMatches'] for p in errors),cpu_p50_s=percentile(costs,.5),cpu_p95_s=percentile(costs,.95),cpu_max_s=max(costs,default=None),
        cpu_sum_s=sum(costs),forecasts=sum(p['Forecasts'] for p in plans),contacts=sum(p['Contacts'] for p in plans),rows=rows)

def main():
    ap=argparse.ArgumentParser();ap.add_argument('--output',type=Path,default=DOC);a=ap.parse_args();out=a.output;out.mkdir(parents=True,exist_ok=True)
    starts=[s['startReferenceMeters'] for s in read(ROOT/'docs/experiments/causal-yaw-throttle/progressive-b-viewer-geometry.json')['segments']]
    sources=[];runs=[];corners=[];audits={}
    paths=[ROOT/f'docs/experiments/standing-start-three-laps/native/{n}.json.gz' for n in ['K1','F']]
    paths += [ROOT/'docs/experiments/corner-speed-loss/native/joint-grid-29.json.gz']
    paths += sorted(p for p in (DOC/'native').glob('*.json.gz') if not p.name.endswith('-plans.json.gz'))
    for p in paths:
        run=read(p);corners+=analyze(run,starts);r=run['Riders'][0];sources.append(dict(path=p.relative_to(ROOT).as_posix(),sha256=hashlib.sha256(p.read_bytes()).hexdigest()))
        runs.append(dict(name=run['Name'],outcome=run['Outcome'],laps=r['CompletedLaps'],finish_s=r['FinishSeconds'],end_s=run['EndSeconds'],fingerprint=run['Fingerprint'],
            native_cpu_s=run['TotalCpuSeconds'],controller_cpu_s=run['ControllerCpuSeconds'],forecasts=run['Forecasts'],contacts=run['ForecastContactQueries'],
            inner_min_m=min(s['Inner'] for s in r['Samples']),outer_min_m=min(s['Outer'] for s in r['Samples']),
            sweep_min_m=min((s['Certificate'] for s in r['Samples'] if s['Certificate'] is not None),default=None),
            beta_max_deg=max(abs(s['Input']['Rider']['SlideRadians'])*180/math.pi for s in r['Samples'])))
        planpath=p.with_name(p.name.removesuffix('.json.gz')+'-plans.json.gz')
        if planpath.exists():audits[run['Name']]=forecast_audit(read(planpath),run)
    baseline={(c['lap'],c['bend']):c for c in corners if c['run']=='K1'}
    for c in corners:c['delta_K1_s']=c['seconds']-baseline[(c['lap'],c['bend'])]['seconds'] if c['complete'] else None
    budget=read(DOC/'stage-A/budget.json')+read(DOC/'native/budget.json')
    save(out/'analysis.json',dict(native_worlds_created_by_analysis=0,sources=sources,runs=runs,corners=corners,plan_audits=audits,
        native_budget=dict(worlds=len(budget),cpu_s=sum(b['CpuSeconds'] for b in budget),attempts=budget),method=__doc__))
    with (out/'corners.csv').open('w',encoding='utf-8',newline='') as f:
        writer=csv.DictWriter(f,fieldnames=list(corners[0]));writer.writeheader();writer.writerows(corners)
    for run in runs:print(run['name'],run['outcome'],run['finish_s'])
    print('BUDGET',len(budget),sum(b['CpuSeconds'] for b in budget))
if __name__=='__main__':main()
