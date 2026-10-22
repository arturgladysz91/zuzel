"""#123: one recorded world, no new simulation; reuse independent contact work/impulse ledgers."""
import argparse,copy,csv,gzip,hashlib,importlib.util,json,math
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2]
DATA=ROOT/'docs/experiments/continuous-corner-planning'
OLD=ROOT/'docs/experiments/feasible-path-speed-planning'
def module(name,file):
    s=importlib.util.spec_from_file_location(name,Path(__file__).with_name(file));m=importlib.util.module_from_spec(s);s.loader.exec_module(m);return m
old=module('prepared','prepared-corner-report.py')
def load(d,n):return json.loads((d/n).read_text(encoding='utf-8-sig'))
def read(d,n):return old.read(d,n)
def save(n,x):(DATA/n).write_text(json.dumps(x,indent=2,allow_nan=False)+'\n',encoding='utf-8',newline='\n')
def weighted(rr,values):return sum(r['dt']*v for r,v in zip(rr,values))/sum(r['dt'] for r in rr)
def flips(values,tolerance=1e-5):
    signs=[math.copysign(1,b-a) for a,b in zip(values,values[1:]) if abs(b-a)>tolerance]
    return sum(a!=b for a,b in zip(signs,signs[1:]))
def analyze():
    run=load(DATA,'continuous.json');rows=read(DATA,'continuous');plans=read(DATA,'continuous-plan');changes=load(DATA,'changes.json');reviews=load(DATA,'reviews.json')
    final=run['Result']['Riders'][0];n=len(rows);assert n==len(plans)==run['Result']['Work']['SolverCalls']==run['Result']['Work']['RearWheelCommits']
    ends=rows[1:]+[dict(t=run['Result']['TimeSeconds'],s=final['TotalReferenceMeters'],x=final['Position']['X'],y=final['Position']['Y'],vx=final['Velocity']['X'],vy=final['Velocity']['Y'],speed=final['Speed'],body_omega=final['AngularVelocity'],heading=final['Heading'])]
    wrap=lambda angle:math.atan2(math.sin(angle),math.cos(angle))
    native=[]
    for r,e in zip(rows,ends):
        chi=math.atan2(r['vy'],r['vx']);after_chi=math.atan2(e['vy'],e['vx']);beta=wrap(r['heading']-chi);after_beta=wrap(e['heading']-after_chi)
        native.append(dict(ChiDot=wrap(after_chi-chi)/r['dt'],BetaDot=wrap(after_beta-beta)/r['dt'],EndBeta=after_beta))
    distances=[0.]
    for r,e in zip(rows,ends):distances.append(distances[-1]+math.hypot(e['x']-r['x'],e['y']-r['y']))
    # Canonical landmark interpolation; distance is actual XY polyline, not the s counter.
    def landmark(s):
        i=next(i for i,(r,e) in enumerate(zip(rows,ends)) if r['s']<=s<=e['s']);r=rows[i];e=ends[i];u=(s-r['s'])/(e['s']-r['s'])
        out={k:r[k]+(e[k]-r[k])*u for k in ['t','speed','x','y']};out['SpeedKph']=out['speed']*3.6;out['DistanceXY']=distances[i]+u*(distances[i+1]-distances[i]);return out
    def energy(a,b):
        sub=copy.deepcopy(run);e=ends[b-1];sub['Result']['Riders'][0]['Velocity']=dict(X=e['vx'],Y=e['vy']);sub['Result']['Riders'][0]['AngularVelocity']=e['body_omega']
        ledger=old.ledger.energy_from_new(rows[a:b],sub);impulse=old.physical.impulse_work(rows[a:b],sub)
        ledger['NativeImpulseErrorWork']=impulse['MeasuredNativeImpulseErrorWorkJoules'];ledger['Unexplained']=ledger['Residual']-ledger['NativeImpulseErrorWork'];assert abs(ledger['Unexplained'])<1e-6
        return ledger
    baseline=load(OLD,'C.json');prior=read(OLD,'C');prefix=load(DATA,'prefix.json');assert prefix['Equal']
    assert rows[:len(prior)]==prior and run['Initial']==baseline['Initial']
    commands=[]
    for c in run['Result']['Decisions']:
        assert abs(c['RequestedActivation']-c['DecisionAt']-.24)<1e-8
        assert c['CancelledAt'] is None
        if c['ActualActivation'] is not None:assert abs(c['ActualActivation']-c['RequestedActivation'])<1e-8
        if c['DecisionAt']<=prefix['Time']:
            d=c.copy()
            if d['ActualActivation'] is not None and d['ActualActivation']>prefix['Time']:d['ActualActivation']=None
            commands.append(d)
    assert commands==baseline['Result']['Decisions']
    for r,p in zip(rows,plans):
        assert abs(r['front_lateral'])<=p['front_capacity']+1e-3
        assert math.hypot(r['rear_longitudinal'],r['rear_lateral'])<=p['rear_capacity']+1e-3
        assert abs(r['steer_after']-r['steer_before'])<=math.pi/2*r['dt']+1e-6
    first_start=old.entry.ENTRY;first_exit=old.entry.EXIT;second_start=changes[0]['BendStart'];second_exit=changes[0]['BendExit']
    regions={};landmarks={}
    for label,start,finish,fulltime in [('First',first_start,first_exit,changes[0]['Time']),('Second',second_start,second_exit,run['Result']['TimeSeconds'])]:
        points={key:landmark(s) for key,s in [('Entry',start),('Middle',(start+finish)/2),('Exit',finish)]};landmarks[label]=points
        a=next(i for i,r in enumerate(rows) if r['s']>=start);b=next((i for i,r in enumerate(rows) if r['t']>=fulltime-1e-8),n)
        rr=rows[a:b];pp=plans[a:b];assert rr
        clearance=lambda key:min(r[key]-(max(r['speed'],e['speed'])+1.05*max(abs(r['body_omega']),abs(e['body_omega'])))*r['dt']/2-.012 for r,e in zip(rr,ends[a:b]))
        cuts=sum(a['drive']>.001 and b['drive']<=.001 for a,b in zip(rr,rr[1:]));excess=[p['required_path_normal']>147*r['speed']*p['max_chi_dot'] for r,p in zip(rr,pp)]
        regions[label]=dict(Landmarks=points,CoMTimeSeconds=points['Exit']['t']-points['Entry']['t'],CoMDistanceXY=points['Exit']['DistanceXY']-points['Entry']['DistanceXY'],
            FullFootprintTime=fulltime,EnergyWindow=dict(Start=rr[0]['t'],End=fulltime,StartS=rr[0]['s'],EndS=ends[b-1]['s']),
            Energy=energy(a,b),MinimumSpeedKph=min(r['speed'] for r in rr)*3.6,MaximumError=max(abs(p['error']) for p in pp),RmsError=math.sqrt(weighted(rr,[p['error']**2 for p in pp])),
            MaximumOffset=max(p['offset'] for p in pp),MinimumOffset=min(p['offset'] for p in pp),ConservativeInner=clearance('inner'),ConservativeOuter=clearance('outer'),
            MaximumBetaDegrees=max(abs(r['beta']) for r in rr)*180/math.pi,EndBetaDegrees=native[b-1]['EndBeta']*180/math.pi,EndBetaDot=pp[-1]['beta_dot'],EndNativeBetaDot=native[b-1]['BetaDot'],EndNativeChiDot=native[b-1]['ChiDot'],
            BetaDotRange=[min(p['beta_dot'] for p in pp),max(p['beta_dot'] for p in pp)],YawRateRange=[min(r['body_omega'] for r in rr),max(r['body_omega'] for r in rr)],
            ChiDotRange=[min(p['chi_dot'] for p in pp),max(p['chi_dot'] for p in pp)],PlannedCurvatureRange=[min(p['path_k'] for p in pp),max(p['path_k'] for p in pp)],
            ActualCurvatureRange=[min(p['chi_dot']/r['speed'] for r,p in zip(rr,pp)),max(p['chi_dot']/r['speed'] for r,p in zip(rr,pp))],
            MaximumRequiredNormal=max(p['required_path_normal'] for p in pp),ActualNormalRange=[min(p['actual_normal'] for p in pp),max(p['actual_normal'] for p in pp)],
            FrontFyRange=[min(p['front_fy'] for p in pp),max(p['front_fy'] for p in pp)],RearFyRange=[min(p['rear_fy'] for p in pp),max(p['rear_fy'] for p in pp)],RearFxRange=[min(p['rear_fx'] for p in pp),max(p['rear_fx'] for p in pp)],
            FrontPeakUse=max(abs(p['front_fy'])/p['front_capacity'] for p in pp),RearPeakUse=max(math.hypot(p['rear_fy'],p['rear_fx'])/p['rear_capacity'] for p in pp),
            ThrottleRange=[min(r['drive'] for r in rr),max(r['drive'] for r in rr)],MeanThrottle=weighted(rr,[r['drive'] for r in rr]),ThrottleCutsToZero=cuts,
            WheelspinRange=[min(r['wheelspin'] for r in rr),max(r['wheelspin'] for r in rr)],SteeringReversals=flips([r['steer_after'] for r in rr]),
            RateLimitedFraction=weighted(rr,[p['rate_limited'] for p in pp]),RequestedOutsideResponseFraction=weighted(rr,[p['unreachable'] for p in pp]),PathOutsideSampledResponseFraction=weighted(rr,[float(x) for x in excess]))
    hashes={}
    for manifest in sorted((ROOT/'docs/experiments').glob('*/manifest.json')):
        if manifest.parent==DATA:continue
        items=load(manifest.parent,'manifest.json')
        if not isinstance(items,dict) or not all(isinstance(v,str) and len(v)==64 for v in items.values()):continue
        for name,digest in items.items():
            f=manifest.parent/name;assert hashlib.sha256(f.read_bytes()).hexdigest().upper()==digest.upper(),str(f);hashes[str(f.relative_to(ROOT)).replace('\\','/')]=digest
    for name,digest in load(DATA,'protocol.json')['Sources'].items():assert hashlib.sha256((ROOT/'tools/shared-world-speedway'/name).read_bytes().replace(b'\r\n',b'\n')).hexdigest().upper()==digest
    assert run['Energy']['MaximumExcessBeyondEngineJoules']==0 and len(changes)==2
    activated=changes[1];pending=changes[0]['Pending'];kept=next(c for c in run['Result']['Decisions'] if c['DecisionAt']==pending['DecisionAt']);assert abs(kept['ActualActivation']-10.04)<1e-9
    actual_pending=next(c for c in run['Result']['Decisions'] if c['DecisionAt']==activated['Pending']['DecisionAt']);assert abs(actual_pending['ActualActivation']-10.34)<1e-9
    p=load(DATA,'protocol.json')['Parameters'].copy();assert p.pop('EnableContinuousCornerPlanning');assert p==load(OLD,'protocol.json')['Parameters']
    ai=min(range(n),key=lambda i:abs(rows[i]['t']-activated['Time']))
    activation=dict(State=activated,InstantWheelspin=rows[ai]['wheelspin'],FiniteContactChiDot=plans[ai]['chi_dot'],FiniteContactBetaDot=plans[ai]['beta_dot'],NativeChiDot=native[ai]['ChiDot'],NativeBetaDot=native[ai]['BetaDot'],DistanceBeforeSecondEntry=second_start-activated['Rider']['TotalReferenceMeters'],SecondsBeforeSecondEntry=landmarks['Second']['Entry']['t']-activated['Time'])
    result=dict(BaseHead=load(DATA,'protocol.json')['BaseHead'],NewNativeWorlds=1,FullLaps=0,Pass=load(DATA,'gate.json')['Pass'],Fingerprint=run['Result']['Fingerprint'],Activation=activation,
        Prefix=dict(**prefix,IdenticalRows=len(prior),IdenticalDecisions=len(commands)),Changes=changes,PerBend=regions,WholeEnergy=energy(0,n),WholeTime=run['Result']['TimeSeconds'],
        WholeDistanceXY=distances[-1],Final=final,HistoricalFiles=len(hashes),HistoricalSha256=hashes,Steps=n,Reviews=len(reviews),PlannerContactQueries=13*len(reviews),LifecycleQueries=sum(c['DiagnosticContactQueries'] for c in changes),
        NativeContactQueries=52*(n+run['Result']['Work']['RearWheelBoundaryEvaluations']),
        Interpretation='Both bends pass in one world, with exact #122 prefix. Second bend has smaller error and higher minimum/middle/exit speed; no progressive speed/tracking collapse observed over two bends. Not a lap/heat stability or realism certificate.')
    save('analysis.json',result)
    print(json.dumps({k:v for k,v in regions.items()},indent=2));return result,rows,plans
def plots(result,rows,plans):
    import matplotlib;matplotlib.use('Agg')
    import matplotlib.pyplot as plt
    plt.rcParams.update({'font.size':10,'axes.spines.top':False,'axes.spines.right':False,'axes.titlesize':12})
    geo=load(ROOT/'docs/experiments/progressive-bend-width','progressive-b-geometry.json');sections=[s for seg in geo['segments'] for s in seg['sections']]
    fig,ax=plt.subplots(figsize=(13,7),layout='constrained')
    for edge in ['inner','outer']:ax.plot([q[edge]['x'] for q in sections],[q[edge]['y'] for q in sections],color='#999',lw=1,label='granice toru' if edge=='inner' else None)
    ax.plot([p['path_x'] for p in plans],[p['path_y'] for p in plans],color='#283b49',ls='--',label='aktywny plan C2',lw=1.7)
    final=result['Final']['Position'];ax.plot([r['x'] for r in rows]+[final['X']],[r['y'] for r in rows]+[final['Y']],color='#07877a',label='jeden ciągły zapis native',lw=2)
    for c in result['Changes']:
        p=c['Rider']['Position'];ax.scatter(p['X'],p['Y'],s=35,color='#bb6b27');ax.annotate(c['Phase']+' '+str(c['Time'])+'s',(p['X'],p['Y']),xytext=(0,15 if c['Phase']=='activated' else -22),textcoords='offset points',fontsize=9)
    ax.set(title='Dwa łuki w jednym świecie: bez resetu motocykla na prostej',aspect='equal',xlabel='X [m]',ylabel='Y [m]');ax.grid(alpha=.15);ax.legend(loc='lower center');fig.savefig(DATA/'trajectory.png',dpi=150);plt.close(fig)
    fig,axs=plt.subplots(4,2,figsize=(14,15),layout='constrained');fig.suptitle('Pierwszy i drugi łuk: rzeczywisty ruch, kontakty i decyzje',fontsize=17,fontweight='bold')
    t=[r['t'] for r in rows]
    axs[0,0].plot(t,[r['speed']*3.6 for r in rows],color='#07877a',label='V native')
    axs[0,1].plot(t,[r['drive'] for r in rows],color='#bb6b27',label='gaz po reakcji')
    axs[1,0].plot(t,[r['beta']*180/math.pi for r in rows],color='#07877a',label='β [°]');ax2=axs[1,0].twinx();ax2.plot(t,[p['beta_dot'] for p in plans],color='#bb6b27',alpha=.6,lw=1,label='βdot');ax2.set_ylabel('βdot [rad/s]',color='#bb6b27')
    axs[1,1].plot(t,[r['body_omega'] for r in rows],label='ψdot');axs[1,1].plot(t,[p['chi_dot'] for p in plans],label='χdot',alpha=.8)
    axs[2,0].plot(t,[p['error'] for p in plans],color='#07877a',label='offset − aktywny plan');axs[2,0].axhline(0,color='#aaa',lw=.7)
    axs[2,1].plot(t,[p['path_k'] for p in plans],color='#283b49',ls='--',label='κ planu');axs[2,1].plot(t,[p['chi_dot']/r['speed'] for r,p in zip(rows,plans)],color='#07877a',label='χdot/V')
    axs[3,0].plot(t,[p['required_path_normal'] for p in plans],color='#283b49',ls='--',label='mV²|κ planu|');axs[3,0].plot(t,[p['actual_normal'] for p in plans],color='#07877a',label='uzyskane normalne');axs[3,0].axhline(1852.33895,color='#999',ls=':',label='absolutne B')
    for key,label in [('front_fy','Fy przód'),('rear_fy','Fy tył'),('rear_fx','Fx tył')]:axs[3,1].plot(t,[p[key] for p in plans],label=label,lw=1.2)
    for ax,title,ylabel in zip(axs.flat,['Prędkość','Gaz (oscylacje zachowane)','Uślizg nadwozia i jego zmiana','Obrót nadwozia i kierunku ruchu','Błąd trajektorii','Krzywizna','Siła planowana i osiągnięta','Składowe w lokalnych osiach opon'],['km/h','Drive','β [°]','rad/s','m','1/m','N','N']):
        ax.set(title=title,ylabel=ylabel,xlabel='Czas [s]');ax.grid(alpha=.2);ax.legend(fontsize=8,loc='best')
        for bend in result['PerBend'].values():ax.axvspan(bend['Landmarks']['Entry']['t'],bend['Landmarks']['Exit']['t'],color='#07877a',alpha=.05)
        ax.axvline(result['Changes'][1]['Time'],color='#555',ls=':',lw=.8)
    fig.savefig(DATA/'dynamics.png',dpi=150);plt.close(fig)
if __name__=='__main__':
    ap=argparse.ArgumentParser();ap.add_argument('--plots',action='store_true');args=ap.parse_args();result,rows,plans=analyze()
    if args.plots:plots(result,rows,plans)
