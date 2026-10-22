"""Read-only native analysis. No simulation or parameter fitting; plotting is optional."""
import argparse,csv,gzip,hashlib,importlib.util,json,math,subprocess
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2]
DATA=ROOT/'docs/experiments/feasible-path-speed-planning'
OLD=ROOT/'docs/experiments/feasible-500cc-corner-entry'
BASE='46129d74cf725c33d67040663262062ac9067281'
def module(name,file):
    spec=importlib.util.spec_from_file_location(name,Path(__file__).with_name(file));m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m);return m
ledger=module('contact','two-contact-bike-report.py')
physical=module('physical','physical-steering-report.py')
entry=module('entry','corner-entry-report.py')
def load(d,n):return json.loads((d/n).read_text(encoding='utf-8-sig'))
def read(d,n):
    p=d/(n+'.csv');f=p.open(newline='') if p.exists() else gzip.open(str(p)+'.gz','rt',newline='')
    with f:return [{k:float(v) for k,v in row.items()} for row in csv.DictReader(f)]
def save(n,x):(DATA/n).write_text(json.dumps(x,indent=2,allow_nan=False)+'\n',encoding='utf-8',newline='\n')
def weighted(rows,values):return sum(r['dt']*v for r,v in zip(rows,values))/sum(r['dt'] for r in rows)
def metrics(folder,name):
    run=load(folder,name+'.json');rows=read(folder,name);control=read(folder,name+'-control');final=run['Result']['Riders'][0]
    if folder==OLD:
        offsets=[g['offset'] for g in read(folder,name+'-geometry')];errors=[d-5 for d in offsets]
        plans=[dict(path_offset=5,path_curvature=c['path_curvature'],actual_curvature=c['chi_dot']/r['speed'],beta_dot=c['beta_dot']) for r,c in zip(rows,control)]
        observations=[dict(offset=d,outward_speed=0,required_path_normal=c['required_normal']) for d,c in zip(offsets,control)]
    else:
        observations=read(folder,name+'-observed');offsets=[q['offset'] for q in observations];errors=[q['plan_offset_error'] for q in observations];plans=read(folder,name+'-plan')
    assert len(rows)==len(control)==len(plans)==len(observations)==run['Result']['Work']['RearWheelCommits']==run['Result']['Work']['SolverCalls']
    energy=ledger.energy_from_new(rows,run);impulse=physical.impulse_work(rows,run)
    unexplained=energy['Residual']-impulse['MeasuredNativeImpulseErrorWorkJoules'];assert abs(unexplained)<1e-6
    assert run['Energy']['MaximumExcessBeyondEngineJoules']==0
    for r,c in zip(rows,control):
        assert abs(r['front_lateral'])<=c['front_capacity']+1e-3
        assert math.hypot(r['rear_longitudinal'],r['rear_lateral'])<=c['rear_capacity']+1e-3
        assert abs(r['steer_after']-r['steer_before'])<=math.pi/2*r['dt']+1e-6
        assert c['queries']==51
    for cmd in run['Result']['Decisions']:
        assert abs(cmd['RequestedActivation']-cmd['DecisionAt']-.24)<1e-9
        if cmd['ActualActivation'] is not None:assert abs(cmd['ActualActivation']-cmd['RequestedActivation'])<1e-9
    landmarks={}
    for label,s in [('Entry',entry.ENTRY),('Middle',entry.MIDDLE),('Exit',entry.EXIT)]:
        landmarks[label]=None if final['TotalReferenceMeters']<s else {k:entry.old.crossing(rows,'s',s,k) for k in ['t','speed','beta','wheelspin','drive']}
        if landmarks[label]:landmarks[label]['SpeedKph']=landmarks[label]['speed']*3.6
    gate=load(folder,name+'-gate.json');corner=[i for i,r in enumerate(rows) if entry.ENTRY<=r['s']<=entry.EXIT]
    final_target=5
    if folder!=OLD and name!='B':
        geometry=load(DATA,'geometry.json');u=max(0,min(1,(final['TotalReferenceMeters']-geometry['TransitionStartMeters'])/(geometry['TransitionEndMeters']-geometry['TransitionStartMeters'])))
        final_target=1+(geometry['SelectedOffsetMeters']-1)*u**3*(10-15*u+6*u*u)
    end_error=final['Road']['OffsetMeters']-final_target
    peak=max(range(len(rows)),key=lambda i:abs(errors[i]))
    result=dict(Pass=gate['Pass'],Stop=run['StopReason'],Landmarks=landmarks,Time=run['Result']['TimeSeconds'],Distance=final['DistanceMeters'],
        MinimumSpeedKph=min([r['speed'] for r in rows]+[final['Speed']])*3.6,MaximumOffset=max(offsets+[final['Road']['OffsetMeters']]),
        MaximumErrorToPlan=max(max(map(abs,errors)),abs(end_error)),RmsErrorToPlan=math.sqrt(weighted(rows,[e*e for e in errors])),
        CornerRmsErrorToPlan=math.sqrt(sum(rows[i]['dt']*errors[i]**2 for i in corner)/sum(rows[i]['dt'] for i in corner)),
        MaximumErrorToFixed5=max(abs(d-5) for d in offsets+[final['Road']['OffsetMeters']]),
        FinalPlanError=end_error,PeakPlanErrorTime=rows[peak]['t'],PeakPlanErrorS=rows[peak]['s'],
        MaximumBetaDegrees=max(run['MaximumSlideDegrees'],abs(gate['FinalBetaDegrees'])),FinalBetaDegrees=gate['FinalBetaDegrees'],FinalBetaDot=gate['FinalBetaDot'],
        Inner=gate['ConservativeInner'],Outer=gate['ConservativeOuter'],Energy=energy,UnexplainedEnergy=unexplained,
        Steps=len(rows),PlannerReviews=run['Result']['Work'].get('PreparedReviews',0),PlannerQueries=run['Result']['Work'].get('PreparedContactQueries',0),
        PlannerMilliseconds=run['Result']['Work'].get('PreparedMilliseconds',0),ControllerQueries=51*len(rows),AppliedContactQueries=len(rows),
        BoundaryEvaluations=run['Result']['Work']['RearWheelBoundaryEvaluations'],Fingerprint=run['Result']['Fingerprint'],
        PeakWheelspin=max(r['wheelspin'] for r in rows),FrontPeakUse=max(abs(r['front_lateral'])/c['front_capacity'] for r,c in zip(rows,control)),
        RearPeakUse=max(math.hypot(r['rear_lateral'],r['rear_longitudinal'])/c['rear_capacity'] for r,c in zip(rows,control)),
        RequiredPathAboveAbsoluteFraction=weighted(rows,[float(o['required_path_normal']>c['front_capacity']+c['rear_capacity']) for o,c in zip(observations,control)]),
        ForceUnreachableFraction=weighted(rows,[c['unreachable'] for c in control]))
    return result,(run,rows,control,plans,observations)
def analyze():
    hashes={}
    for manifest in sorted((ROOT/'docs/experiments').glob('*/manifest.json')):
        if manifest.parent==DATA:continue
        entries=json.loads(manifest.read_text(encoding='utf-8-sig'))
        if not isinstance(entries,dict) or not all(isinstance(v,str) and len(v)==64 for v in entries.values()):continue
        for name,digest in entries.items():
            f=manifest.parent/name;assert hashlib.sha256(f.read_bytes()).hexdigest().upper()==digest.upper(),str(f)
            hashes[str(f.relative_to(ROOT)).replace('\\','/')]=digest
    for name in ['Engine500.cs','LoadBasedTyreGrip.cs','RearWheelDynamics.cs','TwoContactBikeDynamics.cs','TravelDirectionSteering.cs','ReachableTravelDirectionSteering.cs','SlipCoupledDynamics.cs','ExperimentalTrackShape.cs']:
        subprocess.run(['git','diff','--exit-code',BASE,'--','tools/shared-world-speedway/'+name],cwd=ROOT,check=True)
    results={};cache={}
    for label,folder,name in [('#119',OLD,'combined')]+[(n,DATA,n) for n in ['A','B','C']]:results[label],cache[label]=metrics(folder,name)
    assert all(c[0]['Initial']==cache['#119'][0]['Initial'] for c in cache.values())
    assert len(list(DATA.glob('*-attempt.json')))==3
    replay=load(DATA,'replay.json');assert next(x for x in replay if x['Name']=='C')['RealExit']
    for x in replay:assert x['MaximumForceOrSteerDifference']==0 and x['ChangedReviews']==0 and x['MaximumCoastUpperBoundRounding']<1e-12
    protocol=load(DATA,'protocol.json')
    for name,digest in load(DATA,'source-canonical.json').items():
        if name not in {'Engine500.cs','LoadBasedTyreGrip.cs','RearWheelDynamics.cs','TwoContactBikeDynamics.cs','TravelDirectionSteering.cs'}:continue
        source=(ROOT/'tools/shared-world-speedway'/name).read_bytes().replace(b'\r\n',b'\n')
        assert hashlib.sha256(source).hexdigest().upper()==digest
    geom=read(DATA,'planned-geometry');g=load(DATA,'geometry.json');c=results['C'];b=results['#119']
    result=dict(BaseHead=BASE,NativeTrials=3,NewLaps=0,HistoricalFiles=len(hashes),HistoricalSha256=hashes,Results=results,Replay=replay,
        Geometry=dict(Start=g['TransitionStartMeters'],End=g['TransitionEndMeters'],SelectedOffset=g['SelectedOffsetMeters'],
            MaximumCurvature=max(q['curvature'] for q in geom),MinimumCurvature=min(q['curvature'] for q in geom),
            MinimumInner=min(q['inner'] for q in geom),MinimumOuter=min(q['outer'] for q in geom),InitialReview=g['InitialReview']),
        ExitGainKph=c['Landmarks']['Exit']['SpeedKph']-b['Landmarks']['Exit']['SpeedKph'],
        MaxPlanErrorReductionPercent=100*(1-c['MaximumErrorToPlan']/b['MaximumErrorToPlan']),
        LateralLossReductionPercent=100*(1-(c['Energy']['FrontLoss']+c['Energy']['RearLateralLoss'])/(b['Energy']['FrontLoss']+b['Energy']['RearLateralLoss'])),
        Grade='B',Interpretation='C is safe and faster but misses the requested line/60kmh exit targets; response/coast envelope is provisional, not a certified feasible profile. No further tuning or trials.')
    save('analysis.json',result)
    print(json.dumps({k:{key:v[key] for key in ['Pass','Stop','Landmarks','MinimumSpeedKph','MaximumOffset','MaximumErrorToPlan','RmsErrorToPlan','MaximumErrorToFixed5','MaximumBetaDegrees','Energy']} for k,v in results.items()},indent=2))
    return result,cache
def plots(result,cache):
    import matplotlib;matplotlib.use('Agg')
    import matplotlib.pyplot as plt
    plt.rcParams.update({'font.size':10,'axes.spines.top':False,'axes.spines.right':False,'axes.titlesize':12})
    colors={'#119':'#526776','A':'#c77728','B':'#b65252','C':'#158477'}
    geometry=read(DATA,'planned-geometry')
    fig,axs=plt.subplots(1,2,figsize=(14,7),layout='constrained');fig.suptitle('Pierwszy łuk: ścieżka planowana i rzeczywisty ruch',fontsize=17,fontweight='bold')
    for ax in axs:
        for edge in ['inner','outer']:ax.plot([q['edge_'+edge+'_x'] for q in geometry],[q['edge_'+edge+'_y'] for q in geometry],color='#9a9a9a',lw=1,label='granice toru' if edge=='inner' else None)
        ax.plot([q['x'] for q in geometry],[q['y'] for q in geometry],color='#202b35',ls='--',lw=1.5,label='plan A/C: 1→8,5 m')
        line5=[]
        for q in geometry:
            dx=q['edge_outer_x']-q['edge_inner_x'];dy=q['edge_outer_y']-q['edge_inner_y'];width=math.hypot(dx,dy)
            line5.append((q['edge_inner_x']+6*dx/width,q['edge_inner_y']+6*dy/width))
        ax.plot([p[0] for p in line5],[p[1] for p in line5],color='#526776',ls=':',lw=1,label='odniesienie stałe5m')
        ax.set(aspect='equal',xlabel='X [m]',ylabel='Y [m]');ax.grid(alpha=.15)
    for name,(run,rows,_,_,_) in cache.items():
        endpoint=run['Result']['Riders'][0]['Position']
        axs[0].plot([r['x'] for r in rows]+[endpoint['X']],[r['y'] for r in rows]+[endpoint['Y']],color=colors[name],label=name+(' FAIL' if not result['Results'][name]['Pass'] else ' PASS'),lw=1.8)
    run,rows,_,plan,obs=cache['C'];endpoint=run['Result']['Riders'][0]['Position']
    axs[1].plot([r['x'] for r in rows]+[endpoint['X']],[r['y'] for r in rows]+[endpoint['Y']],color=colors['C'],lw=2,label='C: zapis native')
    for landmark,label in [('Entry','wejście'),('Middle','środek'),('Exit','wyjście')]:
        t=result['Results']['C']['Landmarks'][landmark]['t'];i=min(range(len(rows)),key=lambda i:abs(rows[i]['t']-t));axs[1].scatter(rows[i]['x'],rows[i]['y'],s=30,color=colors['C']);axs[1].annotate(label,(rows[i]['x'],rows[i]['y']),xytext=(6,5),textcoords='offset points',fontsize=9)
    axs[0].set(title='A/B zatrzymane przy pierwszym przekroczeniu');axs[1].set(title='C: plan jest legalny, błąd wykonania pozostaje')
    for ax in axs:ax.legend(fontsize=9)
    fig.savefig(DATA/'trajectory.png',dpi=150);plt.close(fig)
    fig,axs=plt.subplots(3,2,figsize=(14,12),layout='constrained');fig.suptitle('Native: prędkość, sterowanie, krzywizna i siły',fontsize=17,fontweight='bold')
    for name,(_,rr,cc,pp,oo) in cache.items():
        s=[r['s'] for r in rr];kw=dict(color=colors[name],label=name,lw=1.5)
        axs[0,0].plot(s,[r['speed']*3.6 for r in rr],**kw);axs[0,1].plot(s,[r['drive'] for r in rr],**kw)
        axs[1,0].plot(s,[r['beta']*180/math.pi for r in rr],**kw);axs[1,1].plot(s,[o['offset']-p['path_offset'] for o,p in zip(oo,pp)],**kw)
    run,rr,cc,pp,oo=cache['C'];s=[r['s'] for r in rr]
    axs[2,0].plot(s,[p['path_curvature'] for p in pp],ls='--',label='plan C',color='#202b35');axs[2,0].plot(s,[p['actual_curvature'] for p in pp],label='rzeczywiste χdot/V',color=colors['C'])
    axs[2,1].plot(s,[o['required_path_normal'] for o in oo],label='mV²|κ planu|',color='#202b35',ls='--')
    axs[2,1].plot(s,[c['actual_normal'] for c in cc],label='uzyskane normalne',color=colors['C'])
    reviews=load(DATA,'C-reviews.json');sr=[min(rr,key=lambda r:abs(r['t']-q['Time']))['s'] for q in reviews]
    axs[2,1].plot(sr,[q['ContactCeiling'] for q in reviews],label='górna granica po Fx/β',color='#c77728')
    axs[2,1].plot(sr,[q['HeldSteeringNormal'] for q in reviews],label='maks. statyczne ±35°',color='#b65252',alpha=.8)
    axs[2,1].axhline(1852.33895,color='#777',lw=1,ls=':',label='absolutne B (nie natychmiastowe)')
    for ax,title,y in zip(axs.flat,['Prędkość','Gaz po reakcji 0,24 s','β całego motocykla','Błąd względem własnego planu','Krzywizna: C','Siły: C'],['km/h','Drive','°','m','1/m','N']):
        ax.set(title=title,ylabel=y,xlabel='Współrzędna referencyjna s [m]');ax.grid(alpha=.2);ax.legend(fontsize=8)
        for station in [entry.ENTRY,entry.MIDDLE,entry.EXIT]:ax.axvline(station,color='#aaa',ls=':',lw=.6)
    fig.savefig(DATA/'speed-forces.png',dpi=150);plt.close(fig)
if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('--plots',action='store_true');args=parser.parse_args()
    result,cache=analyze()
    if args.plots:plots(result,cache)
