"""Read accepted #113/#115 evidence; no worlds, dynamics fitting or parameter search."""
import argparse
import bisect
import gzip
import hashlib
import importlib.util
import json
import math
from pathlib import Path

ROOT=Path(__file__).resolve().parents[2]
DATA=ROOT/'docs/experiments/adaptive-rider-decisions'
SOURCE=ROOT/'docs/experiments/physical-steering-control'
spec=importlib.util.spec_from_file_location('physical_evidence',Path(__file__).with_name('physical-steering-report.py'))
previous=importlib.util.module_from_spec(spec);spec.loader.exec_module(previous)
load=previous.load

def read(path):
    return previous.old.read(path if path.exists() else path.with_suffix(path.suffix+'.gz'))

def decisions(name):
    p=DATA/(name+'-decisions.json')
    return load(p) if p.exists() else json.loads(gzip.decompress(p.with_suffix('.json.gz').read_bytes()))

def save(name,value):
    (DATA/name).write_text(json.dumps(value,indent=2,allow_nan=False)+'\n',encoding='utf-8',newline='\n')

def analyse():
    geometry=load(ROOT/'docs/experiments/progressive-bend-width/progressive-b-geometry.json')
    sections=[q for s in geometry['segments'] for q in s['sections']]
    refs=[q['referenceMeters'] for q in sections]
    def offset(r):
        s=r['s']%geometry['lapMeters'];i=max(0,min(len(sections)-2,bisect.bisect_right(refs,s)-1));a,b=sections[i:i+2]
        f=(s-refs[i])/max(refs[i+1]-refs[i],1e-12)
        values={k:{axis:a[k][axis]+f*(b[k][axis]-a[k][axis]) for axis in ('x','y')} for k in ('reference','outward')}
        return sum((r[axis]-values['reference'][axis])*values['outward'][axis] for axis in ('x','y'))
    cache={};trials={}
    for name,path in [('inner-first-bend',DATA),('adaptive-first-bend',DATA),('inner-eight-laps',SOURCE),('adaptive-eight-laps',DATA)]:
        filename='eight-laps' if name=='inner-eight-laps' else name
        run=load(path/(filename+'.json'));rows=read(path/(filename+'.csv'));cache[name]=(run,rows)
        energy=previous.old.energy_from_new(rows,run);impulse=previous.impulse_work(rows,run)
        unexplained=energy['Residual']-impulse['MeasuredNativeImpulseErrorWorkJoules']
        assert abs(unexplained)<1e-6 and abs(energy['Residual']-run['Energy']['BalanceResidualJoules'])<.005
        assert run['Energy']['MaximumExcessBeyondEngineJoules']==0 and run['LegacyYawCalls']==0
        assert len(rows)==run['Result']['Work']['RearWheelCommits']==run['Result']['Work']['SolverCalls']
        duration=sum(r['dt'] for r in rows);mean=lambda k:sum(r[k]*r['dt'] for r in rows)/duration
        offsets=[offset(r) for r in rows]
        times=[r['t'] for r in rows]+[run['Result']['TimeSeconds']]
        references=[r['s'] for r in rows]+[run['Result']['Riders'][0]['TotalReferenceMeters']]
        laps=[]
        for lap in range(1,9):
            ref=32+lap*geometry['lapMeters'];i=bisect.bisect_left(references,ref)
            if i==len(times):break
            laps.append(times[i-1]+(times[i]-times[i-1])*(ref-references[i-1])/(references[i]-references[i-1]))
        switches=[rows[i]['t'] for i in range(1,len(rows)) if rows[i]['drive']!=rows[i-1]['drive']]
        dwell=[b-a for a,b in zip([0]+switches,switches+[times[-1]])]
        trials[name]=dict(TimeSeconds=run['Result']['TimeSeconds'],LapEndTimes= laps,
            LapDurations=[b-a for a,b in zip([0]+laps,laps)],DistanceMeters=run['Result']['Riders'][0]['DistanceMeters'],
            MeanSpeedKph=mean('speed')*3.6,MinimumSpeedKph=min(r['speed'] for r in rows)*3.6,MaximumSpeedKph=max(r['speed'] for r in rows)*3.6,
            OffsetMinimum=min(offsets),OffsetMaximum=max(offsets),OffsetSpan=max(offsets)-min(offsets),
            MeanAbsBetaDegrees=sum(abs(math.degrees(r['beta']))*r['dt'] for r in rows)/duration,MaximumAbsBetaDegrees=run['MaximumSlideDegrees'],
            MeanWheelspin=mean('wheelspin'),MaximumWheelspin=run['MaximumWheelspin'],DriveChanges=len(switches),MinimumThrottleDwellSeconds=min(dwell),
            PartialDriveSeconds=sum(r['dt'] for r in rows if r['drive']<1),NativeSteps=len(rows),
            MinimumInner=run['MinimumInner'],MinimumOuter=run['MinimumOuter'],ConservativeInner=run['ConservativeInner'],ConservativeOuter=run['ConservativeOuter'],
            Energy=energy,ImpulseAccounting=impulse,UnexplainedEnergyJoules=unexplained)
    baseline=cache['inner-first-bend'][0];adaptive=cache['adaptive-first-bend'][0]
    assert baseline['Result']['Fingerprint']==load(SOURCE/'first-bend.json')['Result']['Fingerprint']
    assert baseline['Initial']==adaptive['Initial']==cache['adaptive-eight-laps'][0]['Initial']
    short=cache['adaptive-first-bend'][1];long=cache['adaptive-eight-laps'][1]
    assert short==long[:len(short)],'Actual first bend must be reused identically in longer execution'
    dd=decisions('adaptive-eight-laps');changes=[d for d in dd if d['Changed']]
    emergency=[d for d in dd if d['Emergency']]
    for d in dd:
        if not d['Emergency']:assert any(c['Action']==d['Chosen'] and c['PredictedSafe'] for c in d['Candidates'])
    assert all(b['TimeSeconds']-a['TimeSeconds']>=.19999 for a,b in zip(dd,dd[1:]))
    first_open=next(d for d in changes if d['Chosen']['Name']=='open-full')
    first_partial=next(d for d in changes if d['Chosen']['Drive']<1)
    outer=min(long,key=lambda r:r['outer'])
    certified=min(long,key=lambda r:r['outer']-(r['speed']+1.05*abs(r['body_omega']))*r['dt']/2-.012)
    hist=previous.historical_hashes()
    for folder in ('physical-steering-control','dynamic-axle-load-light'):
        manifest=load(ROOT/'docs/experiments'/folder/'manifest.json')
        for name,digest in manifest.items():assert hashlib.sha256((ROOT/'docs/experiments'/folder/name).read_bytes()).hexdigest().upper()==digest.upper(),name
        hist[folder]=len(manifest)
    work=cache['adaptive-eight-laps'][0]['Result']['Work'];shortwork=adaptive['Result']['Work']
    controls=read(SOURCE/'eight-laps-control.csv');base_queries=sum(r['queries'] for r in controls)
    metrics=load(DATA/'adaptive-eight-laps-metrics.json')
    result=dict(BaseHead=metrics['BaseHead'],InitialMatches=True,ShortNativePrefixMatches=True,OffFingerprint=baseline['Result']['Fingerprint'],
        HistoricalHashes=hist,Trials=trials,FullSafetyGate=load(DATA/'adaptive-eight-laps-gate.json'),
        FirstWidening=first_open,FirstPartialThrottle=first_partial,EmergencyReviews=len(emergency),FirstEmergency=emergency[0],
        MinimumOuterAt=outer['t'],MinimumConservativeOuterNear=certified['t'],
        FailedConditions=[f"ConservativeOuter={cache['adaptive-eight-laps'][0]['ConservativeOuter']}m < required 0.5m"]
            if cache['adaptive-eight-laps'][0]['ConservativeOuter']<.5 else [],
        SourceHashesCanonicalLf={str(p.relative_to(ROOT)).replace('\\','/'):hashlib.sha256(p.read_bytes().replace(b'\r\n',b'\n')).hexdigest().upper()
            for p in (ROOT/'tools/shared-world-speedway'/name for name in ('AdaptiveRiderControl.cs','AdaptiveRiderAudit.cs','WorldRunner.cs','WorldModel.cs','TwoContactBikeDynamics.cs','PhysicalSteeringControl.cs'))},
        Budget=dict(BaselineExecutedSteeringQueries=base_queries,AdaptiveExecutedSteeringQueries=metrics['SteeringQueries'],
            PredictionContactQueries=work['AdaptiveContactQueries'],PredictionForces=work['AdaptivePredictionForces'],
            PredictionGeometryQueries=work['AdaptivePredictionForces']+sum(len(d['Candidates']) for d in dd),
            ExtraNativeWorldsInPrediction=0,BoundaryQueriesNotIncluded=True,BoundaryForceEvaluations=work['RearWheelBoundaryEvaluations'],
            QueryRatio=(metrics['SteeringQueries']+work['AdaptiveContactQueries'])/base_queries,
            OriginalContactEvaluationsRatio=(work['SolverCalls']+metrics['SteeringQueries']+work['AdaptivePredictionForces']+work['AdaptiveContactQueries'])/(cache['inner-eight-laps'][0]['Result']['Work']['SolverCalls']+base_queries),
            PlanningMilliseconds=work['AdaptiveElapsedMilliseconds'],PlanningMillisecondsPerReview=work['AdaptiveElapsedMilliseconds']/len(dd),
            NativeStepsRatio=work['SolverCalls']/cache['inner-eight-laps'][0]['Result']['Work']['SolverCalls'],
            FirstBend=dict(InnerQueries=load(DATA/'inner-first-bend-metrics.json')['SteeringQueries'],AdaptiveQueries=load(DATA/'adaptive-first-bend-metrics.json')['SteeringQueries'],
                PredictionQueries=shortwork['AdaptiveContactQueries'],PredictionForces=shortwork['AdaptivePredictionForces'],NativeSteps=shortwork['SolverCalls'])),
        Rule='No crossing observed; positive conservative crossing bounds. Full gate FAIL: conservative outer margin <0.5m. No promotion or further variant sweep. INNER 8-lap evidence reused, not rerun. Offset from <=0.2m frozen geometry samples; native exact extrema are in metrics JSON. Timings are diagnostic, not a matched benchmark.')
    save('analysis.json',result)
    print(json.dumps(dict(Completed=cache['adaptive-eight-laps'][0]['Result']['Completed'],FullGate=result['FullSafetyGate']['Passed'],
        PreservedHashes=sum(hist.values()),QueryRatio=result['Budget']['QueryRatio'],FirstOpen=first_open['TimeSeconds'],FirstPartial=first_partial['TimeSeconds'],
        MinimumThrottleDwell=trials['adaptive-eight-laps']['MinimumThrottleDwellSeconds'])))
    return result,cache

def plots(cache):
    import matplotlib
    matplotlib.use('Agg')
    import matplotlib.pyplot as plt
    fig,axes=plt.subplots(2,2,figsize=(12,8),layout='constrained')
    geometry=load(ROOT/'docs/experiments/progressive-bend-width/progressive-b-geometry.json')
    for edge in ('inner','outer'):
        pts=[s for seg in geometry['segments'] for s in seg['sections']]
        axes[0,0].plot([p[edge]['x'] for p in pts],[p[edge]['y'] for p in pts],color='#333333',lw=1)
    for name,label,color in [('inner-eight-laps','INNER #113','#df8a30'),('adaptive-eight-laps','Adaptacyjny','#227bac')]:
        run,rows=cache[name];first=[r for r in rows if r['s']<32+geometry['lapMeters']]
        axes[0,0].plot([r['x'] for r in first],[r['y'] for r in first],label=label,color=color)
        sample=rows[::10];t=[r['t'] for r in sample]
        axes[0,1].plot(t,[r['speed']*3.6 for r in sample],label=label,color=color,lw=1)
    rows=cache['adaptive-eight-laps'][1];sample=rows[::10];t=[r['t'] for r in sample]
    axes[1,0].step(t,[r['drive'] for r in sample],where='post',label='Drive',color='#227bac')
    beta=axes[1,0].twinx();beta.plot(t,[math.degrees(r['beta']) for r in sample],color='#9b5d85',alpha=.6,label='beta');beta.set_ylabel('beta [stopnie]')
    axes[1,1].plot(t,[r['inner'] for r in sample],label='wewnętrzna');axes[1,1].plot(t,[r['outer'] for r in sample],label='zewnętrzna')
    axes[1,1].axhline(.5,color='k',ls='--',lw=.8,label='wymagany zapas zewn. 0,5 m')
    axes[0,0].set(aspect='equal',xlabel='x [m]',ylabel='y [m]',title='Pierwsze okrążenie: rzeczywisty ruch')
    axes[0,1].set(xlabel='t [s]',ylabel='Prędkość [km/h]',title='8 okrążeń: brak wymogu Drive=1')
    axes[1,0].set(xlabel='t [s]',ylabel='Gaz [0..1]',ylim=(.45,1.05),title='Decyzje gazu i fizyczny uślizg')
    axes[1,1].set(xlabel='t [s]',ylabel='Prześwit całego motocykla [m]',title='Bez przekroczenia; pełna bramka zapasu FAIL')
    for ax in axes.flat:ax.grid(alpha=.2);ax.legend(fontsize=8)
    fig.savefig(DATA/'comparison.png',dpi=160);plt.close(fig)

if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('--data',type=Path);parser.add_argument('--plots',action='store_true');parser.add_argument('--compact',action='store_true')
    args=parser.parse_args()
    if args.data:DATA=args.data.resolve()
    result,cache=analyse()
    if args.plots:plots(cache)
    if args.compact:
        for p in list(DATA.glob('*.csv'))+list(DATA.glob('*-decisions.json')):
            p.with_suffix(p.suffix+'.gz').write_bytes(gzip.compress(p.read_bytes(),mtime=0));p.unlink()
    save('manifest.json',{p.name:hashlib.sha256(p.read_bytes()).hexdigest().upper() for p in sorted(DATA.iterdir()) if p.is_file() and p.name!='manifest.json'})
