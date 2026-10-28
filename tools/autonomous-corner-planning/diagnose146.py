"""Stage A: inspect the retained #145 failure before implementing a controller."""
from pathlib import Path
import gzip,hashlib,json,math
ROOT=Path(__file__).resolve().parents[2];OLD=ROOT/'docs/experiments/corner-speed-loss/native';D=ROOT/'docs/experiments/autonomous-corner-planning';D.mkdir(exist_ok=True)
read=lambda p:json.loads(gzip.decompress(p.read_bytes()) if str(p).endswith('.gz') else p.read_bytes())
run=read(OLD/'autonomous-two-command-K1.json.gz');plans=read(OLD/'autonomous-two-command-K1-plans.json.gz');samples=run['Riders'][0]['Samples'];rows=[]
for index,plan in enumerate(plans):
    s=min(samples,key=lambda s:abs(s['Input']['Time']-plan['Time']));v=s['Input']['Rider'];chosen=next(q for q in plan['Candidates'] if q['Candidate']['Action']==plan['Chosen'] and q['Second']==plan['ForecastSecond'])
    actual=plans[index+1]['Chosen'] if index+1<len(plans) else None
    rows.append(dict(Time=plan['Time'],Input=s['Input'],CrankOmega=s['CrankOmega'],Inner=s['Inner'],Outer=s['Outer'],Chosen=plan['Chosen'],ForecastSecond=plan['ForecastSecond'],ActualNext=actual,
        FollowedSecond=None if actual is None else actual==plan['ForecastSecond'],Prediction=chosen['Candidate'],Terms=chosen['Terms'],
        ActivationFirst=plan['Time']+.24,ActivationSecond=plan['Time']+.44))
value=dict(BaseHead='3b9180d957bcc596c5e8b2756f280a65fb40670f',NativeWorlds=0,Outcome=run['Outcome'],EndSeconds=run['EndSeconds'],
    HistoricalFingerprint=run['Fingerprint'],Sources={p.name:hashlib.sha256(p.read_bytes()).hexdigest() for p in [OLD/'autonomous-two-command-K1.json.gz',OLD/'autonomous-two-command-K1-plans.json.gz']},
    MatchedSecond=sum(r['FollowedSecond'] is True for r in rows),ComparableSecond=sum(r['FollowedSecond'] is not None for r in rows),
    FirstNonSafe=next(r for r in rows if r['Prediction']['Safety']!=0)['Time'],FirstPredictedCrossing=next(r for r in rows if r['Prediction']['Clearance']<0)['Time'],Rows=rows)
(D/'diagnosis-saved.json').write_text(json.dumps(value,ensure_ascii=False,indent=2)+'\n',encoding='utf-8',newline='\n')
lines=['# #146 — Stage A: saved failure diagnosis (before new planner)', '',
 'The #145 planner retains no previous plan. Each review ranks a newly generated pair by the original terminal score. The forecast second command is neither kept nor compared with the next actual decision.', '',
 '|Observed s|First: steer° / gas|Forecast second|Actual next|Followed?|Predicted safety / minimum gap m|Native inner gap m|',
 '|---:|---|---|---|---|---:|']
fmt=lambda a:'N/D' if a is None else f"{math.degrees(a['SteeringRadians']):.1f}° / {a['Drive']:.3f}"
for r in rows:
    if r['Time']<3.59:continue
    lines.append(f"|{r['Time']:.2f}|{fmt(r['Chosen'])}|{fmt(r['ForecastSecond'])}|{fmt(r['ActualNext'])}|{r['FollowedSecond']}|{r['Prediction']['Safety']} / {r['Prediction']['Clearance']:.4f}|{r['Inner']:.4f}|")
lines+=['', 'At 5.0 s the forecast requires partial throttle at observation 5.2 s (activation 5.44 s); the next review requests full throttle instead. At 5.2 s the new pair requires −15° / gas 0 at observation 5.4 s (activation 5.64 s); the actual review requests −15° / partial throttle. At 5.4 s the same future coast is again postponed. These deviations occur before the forecast loses its Safe class at 5.6 s.',
 '', 'At 5.6 s the selected pair is already Uncertain (future gap −0.1807 m). At 5.8 s it explicitly predicts a footprint crossing, yet the collector accepts the least-bad Unsafe candidate. The actual inner crossing occurs at 6.57 s. No execution veto exists in #145. H09 ends before the complete corner/next straight, so nominally Safe earlier pairs do not establish a recoverable complete maneuver.',
 '', 'Established from saved data: repeated abandonment of forecast second actions, no plan memory, and execution after all ranked alternatives are unsafe. Not yet isolated causally: whether executing an earlier planned second action alone avoids the later crossing; whether sequence prediction itself matches native integration. Stage A native sequence checks must resolve those questions before implementing a new planner.']
(D/'DIAGNOSIS.md').write_text('\n'.join(lines)+'\n',encoding='utf-8',newline='\n')
print({k:v for k,v in value.items() if k not in ['Rows','Sources']})
