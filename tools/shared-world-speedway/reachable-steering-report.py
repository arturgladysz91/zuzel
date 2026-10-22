"""#120: analyse saved #119 plus the sole #120 trial; never run a simulation."""
import csv,gzip,hashlib,importlib.util,json,math,subprocess,argparse,copy
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2]
DATA=ROOT/'docs/experiments/reachable-steering-line-tracking'
OLD=ROOT/'docs/experiments/feasible-500cc-corner-entry'
BASE='8a218fc8cb8be3460c8e42116789ece8ff576498'
spec=importlib.util.spec_from_file_location('entry',Path(__file__).with_name('corner-entry-report.py'))
old=importlib.util.module_from_spec(spec);spec.loader.exec_module(old)
def load(folder,name):return json.loads((folder/name).read_text(encoding='utf-8-sig'))
def read(folder,name):
    p=folder/(name+'.csv');f=open(p,newline='') if p.exists() else gzip.open(str(p)+'.gz','rt',newline='')
    with f:return [{k:float(v) for k,v in r.items()} for r in csv.DictReader(f)]
def save(name,value):(DATA/name).write_text(json.dumps(value,indent=2,allow_nan=False)+'\n',encoding='utf-8',newline='\n')
def windows(rows,predicate):
    result=[];start=None;last=None
    for r in rows:
        if predicate(r):
            if start is None:start=r['t']
            last=r['t']+r['dt']
        elif start is not None:result.append([start,last]);start=None
    if start is not None:result.append([start,last])
    return result
def weighted(rows,key,stop=None):
    rr=[r for r in rows if stop is None or r['t']<stop];return sum(r[key]*r['dt'] for r in rr)/sum(r['dt'] for r in rr)
def metrics(folder,name):
    run=load(folder,name+'.json');rows=read(folder,name);control=read(folder,name+'-control');geo=read(folder,name+'-geometry')
    final=run['Result']['Riders'][0];duration=run['Result']['TimeSeconds']
    assert len(rows)==len(control)==len(geo)==run['Result']['Work']['SolverCalls']==run['Result']['Work']['RearWheelCommits']
    assert run['Energy']['MaximumExcessBeyondEngineJoules']==0
    e=old.old.old.old.energy_from_new(rows,run);impulse=old.old.old.impulse_work(rows,run)
    assert abs(e['Residual']-impulse['MeasuredNativeImpulseErrorWorkJoules'])<1e-6
    for r,c in zip(rows,control):
        assert abs(r['front_lateral'])<=c['front_capacity']*(1+4*2**-23)
        assert math.hypot(r['rear_longitudinal'],r['rear_lateral'])<=c['rear_capacity']*(1+4*2**-23)
        assert abs(r['steer_after']-r['steer_before'])<=math.pi/2*r['dt']+1e-6
        assert c['queries']==51
        assert r['drive']==(1 if r['t']<.24-1e-8 else float.fromhex('0x1.333334p-3'))
    points={}
    for label,s in [('Entry',old.ENTRY),('Middle',old.MIDDLE),('Exit',old.EXIT)]:
        points[label]=None if final['TotalReferenceMeters']<s else {k:old.old.crossing(rows,'s',s,k) for k in ['t','speed','beta','wheelspin']}
        if points[label]:points[label]['SpeedKph']=points[label]['speed']*3.6
    errors=[g['offset']-5 for g in geo]+[final['Road']['OffsetMeters']-5]
    rms=math.sqrt(sum((g['offset']-5)**2*r['dt'] for g,r in zip(geo,rows))/duration)
    region=[(r,g) for r,g in zip(rows,geo) if old.ENTRY<=r['s']<=old.EXIT]
    return dict(StopReason=run['StopReason'],Gate=load(folder,name+'-gate.json'),Landmarks=points,
        MaximumAbsoluteErrorMeters=max(map(abs,errors)),RmsErrorTo5Meters=rms,
        CornerRmsErrorTo5Meters=math.sqrt(sum((g['offset']-5)**2*r['dt'] for r,g in region)/sum(r['dt'] for r,g in region)),
        MaximumOffsetMeters=max(g['offset'] for g in geo),MinimumSpeedKph=min([r['speed'] for r in rows]+[final['Speed']])*3.6,
        PeakBetaDegrees=run['MaximumSlideDegrees'],FinalBetaDot=load(folder,name+'-gate.json')['FinalBetaDot'],
        Energy=e,UnexplainedEnergyJoules=e['Residual']-impulse['MeasuredNativeImpulseErrorWorkJoules'],
        Steps=len(rows),ControllerContactQueries=sum(c['queries'] for c in control),ContactEvaluationsIncludingApplied=52*len(rows),
        BoundaryEvaluations=run['Result']['Work']['RearWheelBoundaryEvaluations'],SolverMilliseconds=run['Result']['Work']['SolverElapsedMilliseconds'],
        Final=final,TimeSeconds=duration,DistanceMeters=final['DistanceMeters'],Fingerprint=run['Result']['Fingerprint'],
        FrontMaximumUtilization=max(abs(r['front_lateral'])/c['front_capacity'] for r,c in zip(rows,control)),
        RearMaximumUtilization=max(math.hypot(r['rear_longitudinal'],r['rear_lateral'])/c['rear_capacity'] for r,c in zip(rows,control)),
        FrontMeanUtilization=sum(abs(r['front_lateral'])/c['front_capacity']*r['dt'] for r,c in zip(rows,control))/duration,
        RearMeanUtilization=sum(math.hypot(r['rear_longitudinal'],r['rear_lateral'])/c['rear_capacity']*r['dt'] for r,c in zip(rows,control))/duration), (run,rows,control,geo)
def analyze():
    hashes={}
    for manifest in sorted((ROOT/'docs/experiments').glob('*/manifest.json')):
        if manifest.parent==DATA:continue
        entries=json.loads(manifest.read_text(encoding='utf-8-sig'))
        if not isinstance(entries,dict) or not all(isinstance(x,str) and len(x)==64 for x in entries.values()):continue
        for name,digest in entries.items():
            f=manifest.parent/name;assert hashlib.sha256(f.read_bytes()).hexdigest().upper()==digest.upper(),str(f)
            hashes[str(f.relative_to(ROOT)).replace('\\','/')]=digest
    for name in ['Engine500.cs','LoadBasedTyreGrip.cs','RearWheelDynamics.cs','TravelDirectionSteering.cs','SlipCoupledDynamics.cs','ExperimentalTrackShape.cs']:
        subprocess.run(['git','diff','--exit-code',BASE,'--','tools/shared-world-speedway/'+name],cwd=ROOT,check=True)
    a=read(DATA,'saved-state-comparison');baseline,bc=metrics(OLD,'combined');trial,tc=metrics(DATA,'reachable')
    assert bc[0]['Initial']==tc[0]['Initial'] and bc[0]['Result']['Decisions']==tc[0]['Result']['Decisions']
    assert len(a)==len(bc[1])
    derivatives=[]
    final=bc[0]['Result']['Riders'][0]
    ends=bc[1][1:]+[dict(vx=final['Velocity']['X'],vy=final['Velocity']['Y'],heading=final['Heading'],x=final['Position']['X'],y=final['Position']['Y'])]
    for r,c,v,n in zip(a,bc[2],bc[1],ends):
        assert r['chi_dot']==c['chi_dot'] and r['desired_chi_dot']==c['desired_chi_dot']
        assert abs(r['demand_ff']+r['demand_error']+r['demand_damping']-r['desired_chi_dot']*v['speed'])<1e-5
        r['current_fx_normal_ceiling']=c['front_capacity']+math.sqrt(max(0,c['rear_capacity']**2-r['rear_fx']**2))*abs(math.cos(r['beta']))+max(0,r['rear_fx']*math.sin(r['beta']))
        angle=math.atan2(n['vy'],n['vx'])-r['chi'];angle=math.atan2(math.sin(angle),math.cos(angle))
        beta_next=math.atan2(math.sin(n['heading']-math.atan2(n['vy'],n['vx'])),math.cos(n['heading']-math.atan2(n['vy'],n['vx'])))
        delta_beta=math.atan2(math.sin(beta_next-r['beta']),math.cos(beta_next-r['beta']))
        derivatives.append(dict(t=r['t'],force_chi_dot=r['chi_dot'],native_chi_dot=angle/r['dt'],force_beta_dot=r['beta_dot'],native_beta_dot=delta_beta/r['dt'],
            native_curvature=angle/max(math.hypot(n['x']-v['x'],n['y']-v['y']),1e-12),current_fx_normal_ceiling=r['current_fx_normal_ceiling']))
    with (DATA/'native-response.csv').open('w',newline='') as f:
        writer=csv.DictWriter(f,fieldnames=derivatives[0]);writer.writeheader();writer.writerows(derivatives)
    oldcost=sum(r['old_score']*r['dt'] for r in a);newcost=sum(r['reachable_score']*r['dt'] for r in a)
    at=lambda t:min(a,key=lambda r:abs(r['t']-t))
    overshoot=next(r for r in a if r['offset']>=6 and r['error_dot']>0)
    peak=max(a,key=lambda r:r['offset']);common=trial['TimeSeconds']
    common_rms={}
    for label,(_,rows,_,geo) in [('Baseline',bc),('Reachable',tc)]:
        common_rms[label]=math.sqrt(sum((g['offset']-5)**2*r['dt'] for r,g in zip(rows,geo) if r['t']<common)/common)
    prefix_rows=[r for r in bc[1] if r['t']<common-1e-8];endpoint=bc[1][len(prefix_rows)];prefix_run=copy.deepcopy(bc[0])
    prefix_run['Result']['Riders'][0]['Velocity']=dict(X=endpoint['vx'],Y=endpoint['vy'])
    prefix_run['Result']['Riders'][0]['AngularVelocity']=endpoint['body_omega']
    prefix_energy=old.old.old.old.energy_from_new(prefix_rows,prefix_run)
    result=dict(BaseHead=BASE,HistoricalFiles=len(hashes),HistoricalSha256=hashes,NewNativeTrials=1,FullLaps=0,
        Replay=load(DATA,'replay.json'),Baseline=baseline,Reachable=trial,CommonDurationSeconds=common,CommonDurationRmsErrorMeters=common_rms,
        BaselineEnergyAtCommonDuration=prefix_energy,MaximumNativeChiDotDifference=max(abs(r['native_chi_dot']-r['force_chi_dot']) for r in derivatives),
        AbsoluteCapacityExceeded=windows(a,lambda r:r['required_normal']>r['budget']),
        FullSteeringSampledCapacityExceeded=windows(a,lambda r:r['required_normal']>r['full_max_normal']),
        RateLimited=windows(a,lambda r:r['rate_limited']>0),FirstOutwardOvershootBeyondOneMeter=overshoot,PeakOffsetState=peak,
        IntegratedLocalScoreOriginal=oldcost,IntegratedLocalScoreReachable=newcost,LocalScoreReductionPercent=100*(1-newcost/oldcost),
        LargestLocalScoreWorsening=max(r['reachable_score']-r['old_score'] for r in a),
        KeyStates=[at(t) for t in [0,.24,1,1.55,1.73,2,2.42,2.58,3,4.3,4.5,4.8,5.4,5.6,6,7,10]],
        Interpretation='At this speed and accepted state, entry force demand is physically unavailable; steering search domain is not its primary cause. Reachable-only greedy search fails recovery in the sole trial. This does not prove no earlier planning/controller could ever track5m with the same physics.')
    save('analysis.json',result)
    print(json.dumps({k:v for k,v in result.items() if k not in ['HistoricalSha256','KeyStates','FirstOutwardOvershootBeyondOneMeter','PeakOffsetState']},indent=2))
    return result,a,bc,tc
def plots(result,a,bc,tc):
    import matplotlib;matplotlib.use('Agg')
    import matplotlib.pyplot as plt
    plt.rcParams.update({'font.size':10,'axes.spines.top':False,'axes.spines.right':False,'axes.titlesize':12,'axes.titleweight':'bold'})
    fig,axs=plt.subplots(4,2,figsize=(14,14),layout='constrained');fig.suptitle('#119: odchylenie zaczyna się przy niedoborze siły, przed limitem kierownicy',fontsize=16,fontweight='bold')
    t=[r['t'] for r in a];deg=180/math.pi
    def plot(ax,key,label,scale=1):ax.plot(t,[r[key]*scale for r in a],label=label,lw=1.5)
    plot(axs[0,0],'offset','rzeczywisty offset');plot(axs[0,0],'target','cel');axs[0,0].set(ylabel='m',title='Linia i polecenie po reakcji 0,24 s')
    plot(axs[0,1],'error','błąd [m]');plot(axs[0,1],'error_dot','pochodna [m/s]');axs[0,1].set(title='Błąd offset−cel; skok celu pominięty w pochodnej')
    plot(axs[1,0],'chi','χ: kierunek prędkości',deg);plot(axs[1,0],'psi','ψ: nadwozie',deg);plot(axs[1,0],'beta','β = ψ−χ',deg);axs[1,0].set(ylabel='°',title='Kierunek ruchu i uślizg')
    plot(axs[1,1],'beta_dot','rzeczywiste βdot');plot(axs[1,1],'chi_dot','osiągnięte χdot');plot(axs[1,1],'desired_chi_dot','żądane χdot');axs[1,1].set(ylabel='rad/s',title='Żądanie PD przekracza odpowiedź kontaktów')
    plot(axs[2,0],'steer_request','żądanie #117',deg);plot(axs[2,0],'steer_after','wykonany koniec kroku',deg);plot(axs[2,0],'steer_used','użyty środek kroku',deg);axs[2,0].set(ylabel='°',title='Kierownica: limit 90°/s aktywny dopiero od 4,42 s')
    for l,h in result['RateLimited']:axs[2,0].axvspan(l,h,color='#e2a349',alpha=.2)
    plot(axs[2,1],'front_fy','Fy przodu');plot(axs[2,1],'rear_fy','Fy tyłu');plot(axs[2,1],'rear_fx','Fx tyłu');axs[2,1].set(ylabel='N',title='Składowe w lokalnych osiach opon')
    plot(axs[3,0],'required_normal','wymaga linia 5 m');plot(axs[3,0],'actual_normal','uzyskano');plot(axs[3,0],'full_max_normal','maks. próbka całych ±35°');plot(axs[3,0],'budget','absolutny B');axs[3,0].set(ylabel='N',title='Budżet sił i nieosiągalność na wejściu')
    plot(axs[3,1],'required_curvature','krzywizna linii 5 m');plot(axs[3,1],'actual_curvature','faktyczne χdot/V');axs[3,1].set(ylabel='1/m',title='Krzywizna toru ruchu')
    for ax in axs.flat:ax.axvline(2.58,color='#555',ls=':',lw=.8);ax.grid(alpha=.2);ax.legend(fontsize=8);ax.set(xlabel='Czas [s]')
    fig.savefig(DATA/'diagnosis.png',dpi=145);plt.close(fig)
    fig,axs=plt.subplots(3,2,figsize=(14,11),layout='constrained');fig.suptitle('Wybór osiągalny poprawia lokalny koszt, lecz psuje odzyskanie linii',fontsize=16,fontweight='bold')
    for name,(_,rows,c,g),color in [('oryginalny #119',bc,'#227596'),('wariant #120 (FAIL)',tc,'#c16a34')]:
        tt=[r['t'] for r in rows];kw=dict(label=name,color=color)
        axs[0,0].plot(tt,[r['offset'] for r in g],**kw);axs[0,1].plot(tt,[r['speed']*3.6 for r in rows],**kw)
        axs[1,0].plot(tt,[r['steer_after']*deg for r in rows],**kw);axs[1,1].plot(tt,[r['beta']*deg for r in rows],**kw)
        axs[2,0].plot(tt,[r['inner'] for r in rows],**kw)
    axs[0,0].axhline(5,color='#888',ls='--',label='cel 5 m');axs[0,0].set(ylabel='m',title='Offset: szczyt podobny, potem przekroczenie wewnętrznej granicy')
    axs[0,1].set(ylabel='km/h',title='Nowy wariant traci więcej prędkości i nie kończy łuku')
    axs[1,0].set(ylabel='°',title='Lokalny wybór zatrzymuje kierownicę przy +35°')
    axs[1,1].set(ylabel='°',title='β nie zastępuje kontroli trajektorii')
    axs[2,0].axhline(0,color='#888',lw=.8);axs[2,0].set(ylabel='m',title='Prześwit wewnętrzny (FAIL w 6,71 s)')
    axs[2,1].plot(t,[(r['old_score']-r['reachable_score']) for r in a],color='#227596',label='zysk kosztu na zamrożonych stanach #119')
    axs[2,1].set(ylabel='(rad/s)²',title=f"Zintegrowany zysk lokalny: {result['LocalScoreReductionPercent']:.4f}%")
    for ax in axs.flat:ax.grid(alpha=.2);ax.legend(fontsize=8);ax.set(xlabel='Czas [s]')
    fig.savefig(DATA/'comparison.png',dpi=145);plt.close(fig)
if __name__=='__main__':
    ap=argparse.ArgumentParser();ap.add_argument('--plots',action='store_true');ap.add_argument('--compact',action='store_true');args=ap.parse_args()
    result,a,bc,tc=analyze()
    if args.plots:plots(result,a,bc,tc)
    if args.compact:
        for p in DATA.glob('*.csv'):p.with_suffix('.csv.gz').write_bytes(gzip.compress(p.read_bytes(),mtime=0));p.unlink()
    save('manifest.json',{str(p.relative_to(DATA)).replace('\\','/'):hashlib.sha256(p.read_bytes()).hexdigest().upper() for p in sorted(DATA.rglob('*')) if p.is_file() and p.name!='manifest.json' and not {'bin','obj'}.intersection(p.relative_to(DATA).parts)})
