"""Read native two-contact/legacy evidence. No dynamics integration or tyre calibration."""
import bisect
import argparse
import csv
import gzip
import hashlib
import json
import math
from pathlib import Path
import struct
import sys

ROOT=Path(__file__).resolve().parents[2]
DATA=ROOT/'docs/experiments/two-contact-bike-dynamics'
OLD=ROOT/'docs/experiments/speed-grip-line-crossover'
SLIDE=ROOT/'docs/experiments/full-throttle-slide-grip-audit'
load=lambda p:json.loads(p.read_text(encoding='utf-8'))
f32=lambda v:struct.unpack('f',struct.pack('f',v))[0]
def read(p):
    with (gzip.open(p,'rt',encoding='utf-8') if p.suffix=='.gz' else p.open(encoding='utf-8')) as f:
        return [{k:float(v) for k,v in r.items()} for r in csv.DictReader(f)]
def save(name,value):
    (DATA/name).write_text(json.dumps(value,indent=2,allow_nan=False)+'\n',encoding='utf-8',newline='\n')
def dot(a,b):return sum(x*y for x,y in zip(a,b))
def point_velocity(v,w,r):return [v[0]-w*r[1],v[1]+w*r[0]]
def interpolate(rows,value,key,axis='s'):
    if not rows or value<rows[0][axis] or value>rows[-1][axis]:return None
    values=[r[axis] for r in rows];i=max(0,min(len(rows)-2,bisect.bisect_right(values,value)-1));a,b=rows[i:i+2]
    return a[key]+(b[key]-a[key])*(value-a[axis])/(b[axis]-a[axis])
def energy_from_new(rows,run):
    """Independent endpoint work at each actual world point, including rotation ONCE."""
    final=run['Result']['Riders'][0]
    def state(r):return [r['vx'],r['vy']],r['body_omega']
    ends=[state(r) for r in rows[1:]]+[(list(final['Velocity'].values()),final['AngularVelocity'])]
    totals=dict(Engine=0,FrontLoss=0,RearLateralLoss=0,RearLongitudinalLoss=0,Bearing=0,ResistanceWork=0,FrontWork=0,RearWork=0)
    for r,(v_after,w_after) in zip(rows,ends):
        h=r['dt'];v=[r['vx'],r['vy']];mid=[(a+b)/2 for a,b in zip(v,v_after)];spin=(r['body_omega']+w_after)/2
        vf=point_velocity(mid,spin,[r['front_arm_x'],r['front_arm_y']]);vr=point_velocity(mid,spin,[r['rear_arm_x'],r['rear_arm_y']])
        fw=dot([r['front_fx'],r['front_fy']],vf)*h;rw=dot([r['rear_fx'],r['rear_fy']],vr)*h
        forward=[f32(math.cos(r['heading'])),f32(math.sin(r['heading']))]
        u=dot(vr,forward);long_work=r['rear_longitudinal']*u*h
        totals['FrontWork']+=fw;totals['RearWork']+=rw;totals['FrontLoss']-=fw
        totals['RearLateralLoss']-=rw-long_work
        totals['RearLongitudinalLoss']+=r['rear_longitudinal']*(.33*(r['omega_before']+r['omega_after'])/2-u)*h
        totals['Engine']+=r['engine_work'];totals['Bearing']+=r['bearing_loss'];totals['ResistanceWork']+=dot([r['resistance_x'],r['resistance_y']],mid)*h
    def k(v,w):return .5*run['Mass']*dot(v,v)+.5*f32(run['Inertia'])*w*w
    start=state(rows[0]);end=ends[-1]
    totals['BodyChange']=k(*end)-k(*start);totals['WheelChange']=.45*(rows[-1]['omega_after']**2-rows[0]['omega_before']**2)
    totals['Residual']=totals['BodyChange']+totals['WheelChange']-totals['Engine']+totals['FrontLoss']+totals['RearLateralLoss']+totals['RearLongitudinalLoss']+totals['Bearing']-totals['ResistanceWork']
    return totals
def historical_hashes():
    result={}
    names=[*load(ROOT/'docs/experiments/natural-racing-line-vs-inner/analysis.json')['HistoricalHashes'],'natural-racing-line-vs-inner']
    for name in sorted(names):
        m=ROOT/'docs/experiments'/name/'manifest.json'
        values=load(m)
        for file,digest in values.items():assert hashlib.sha256((m.parent/file).read_bytes()).hexdigest().upper()==digest.upper(),(m,file)
        result[m.parent.name]=len(values)
    return result
def qualified_short(run):
    return run['StopReason'] is None and run['MinimumInner']>=.05 and run['MinimumOuter']>=.5 and run['ConservativeInner']>=0 and run['ConservativeOuter']>=.5
def isolated_gate():
    runs={c['Name']:load(DATA/(c['Name']+'.json')) for c in load(DATA/'freeze.json')['Cases']}
    checks={name:run['StopReason'] is None and run['Result']['Completed'] and run['LegacyYawCalls']==0
        and run['Energy']['MaximumExcessBeyondEngineJoules']==0 and abs(run['Energy']['BalanceResidualJoules'])<.15 for name,run in runs.items()}
    checks['repeat']=runs['B-left']['Result']['Fingerprint']==runs['E-left-repeat']['Result']['Fingerprint']
    checks['turn signs']=runs['B-left']['Result']['Riders'][0]['AngularVelocity']>0 and runs['B-right']['Result']['Riders'][0]['AngularVelocity']<0
    ref=runs['E-left-h5']['Result']['Riders'][0]['Position']
    def distance(name):
        p=runs[name]['Result']['Riders'][0]['Position'];return math.hypot(p['X']-ref['X'],p['Y']-ref['Y'])
    checks['convergence']=distance('B-left')<distance('E-left-h20')
    result={'Passed':all(checks.values()),'Checks':checks,'Rule':'Isolated completion/passivity/energy0.15J, zero old yaw, sign, exact repeat and native-step convergence. C# tests additionally audit impulses/budgets/low-speed endpoints.'}
    save('isolated-gate.json',result);return result
def analyse():
    isolated=[];cache={}
    for case in load(DATA/'freeze.json')['Cases']:
        name=case['Name'];run=load(DATA/(name+'.json'));rows=read(DATA/(name+'.csv'));cache[name]=rows
        independent=energy_from_new(rows,run)
        assert abs(independent['Residual']-run['Energy']['BalanceResidualJoules'])<.01,(name,independent)
        isolated.append({'Name':name,'FinalTime':run['Result']['TimeSeconds'],'StopReason':run['StopReason'],'Fingerprint':run['Result']['Fingerprint'],
            'MaximumSlideDegrees':run['MaximumSlideDegrees'],'FinalSlideDegrees':run['FinalSlideDegrees'],'MaximumSpin':run['MaximumSpin'],
            'MaximumWheelspin':run['MaximumWheelspin'],'Energy':run['Energy'],'IndependentEnergy':independent,
            'FrontPeakTorque':max(abs(r['front_torque']) for r in rows),'RearPeakTorque':max(abs(r['rear_torque']) for r in rows),
            'LegacyYawCalls':run['LegacyYawCalls']})
    isolated_pass=all(c['StopReason'] is None and c['LegacyYawCalls']==0 and c['Energy']['MaximumExcessBeyondEngineJoules']==0 and abs(c['Energy']['BalanceResidualJoules'])<.15 for c in isolated)
    assert isolated_pass
    short=load(DATA/'track-short.json');rows=read(DATA/'track-short.csv');cache['new']=rows
    short['MaximumSteeringUsedDegrees']=max(abs(math.degrees(r['steer_used'])) for r in rows)
    short['FrontPeakLateralForce']=max(abs(r['front_lateral']) for r in rows)
    short['RearPeakLateralForce']=max(abs(r['rear_lateral']) for r in rows)
    short['FrontPeakTorque']=max(abs(r['front_torque']) for r in rows)
    short['RearPeakTorque']=max(abs(r['rear_torque']) for r in rows)
    short['NetPeakTorque']=max(abs(r['front_torque']+r['rear_torque']) for r in rows)
    end=short['Result']['TimeSeconds'];legacy_all=read(OLD/'baseline-inner-tracking.csv.gz');old=[r for r in legacy_all if r['t']<end-1e-10];cache['old']=old
    old_forces=read(OLD/'baseline-inner-forces.csv.gz')[:len(old)];decomp=read(SLIDE/'baseline-inner-decomposition.csv.gz')[:len(old)]
    assert all(r['t']==f['t']==d['t'] for r,f,d in zip(old,old_forces,decomp))
    old_run=load(OLD/'baseline-inner.json');assert {k:v for k,v in short['Initial'].items() if k!='Steering'}==old_run['Initial']
    old_energy={key:0. for key in ['BodyChange','WheelChange','Engine','FrontLoss','RearLateralLoss','RearLongitudinalLoss','Bearing','ResistanceWork','YawWork']}
    for i,(r,f,d) in enumerate(zip(old,old_forces,decomp)):
        nxt=legacy_all[i+1];mid=[(r['vx']+nxt['vx'])/2,(r['vy']+nxt['vy'])/2];h=f32(f['dt']);forward=[f32(math.cos(r['body_heading'])),f32(math.sin(r['body_heading']))];left=[-forward[1],forward[0]]
        lateral_velocity=dot(mid,left);front_loss=-d['front_fy']*lateral_velocity*h;rear_loss=-d['rear_fy']*lateral_velocity*h
        tyre=[forward[j]*r['fx']+left[j]*r['fy'] for j in [0,1]];res=[f['force_x']-tyre[0],f['force_y']-tyre[1]]
        for key,value in [('BodyChange',d['body_delta']),('WheelChange',d['wheel_delta']),('Engine',f['engine_work']),('FrontLoss',front_loss),('RearLateralLoss',rear_loss),
            ('RearLongitudinalLoss',d['longitudinal_loss_work']),('Bearing',f['bearing_loss']),('ResistanceWork',dot(res,mid)*h),('YawWork',d['yaw_work'])]:old_energy[key]+=value
    old_energy['Residual']=old_energy['BodyChange']+old_energy['WheelChange']-old_energy['Engine']+old_energy['FrontLoss']+old_energy['RearLateralLoss']+old_energy['RearLongitudinalLoss']+old_energy['Bearing']-old_energy['ResistanceWork']-old_energy['YawWork']
    phase={}
    for name,rr in [('Old',legacy_all),('New',rows)]:
        phase[name]={key:(v*3.6 if v is not None else None) for key,value in [('Entry',35),('Middle',83.6946874857),('Exit',132.3893749714)] for v in [interpolate(rr,value,'speed')]}
    planned=read(OLD/'crossover-inner-geometry.csv')
    first_error=next((r for r in rows if math.hypot(r['x']-interpolate(planned,r['s']%318.7787499428,'x'),r['y']-interpolate(planned,r['s']%318.7787499428,'y'))>1),None)
    legacy_summary={'EndTime':end,'EndReference':interpolate(legacy_all,end,'s','t'),'MaximumSlideDegrees':max(abs(math.degrees(math.atan2(math.sin(r['body_heading']-r['velocity_heading']),math.cos(r['body_heading']-r['velocity_heading'])))) for r in old),
        'MaximumSpin':max(abs(r['angular_velocity']) for r in old),'MaximumWheelspin':max(r['wheelspin'] for r in old),
        'MinimumInner':min(r['inner'] for r in old),'MinimumOuter':min(r['outer'] for r in old),'MaximumYawTorque':max(abs(r['yaw_energy']) for r in old),
        'FrontPeakLateralForce':max(abs(d['front_fy']) for d in decomp),'RearPeakLateralForce':max(abs(d['rear_fy']) for d in decomp),'Energy':old_energy}
    convergence=[]
    end5=load(DATA/'E-left-h5.json')['Result']['Riders'][0]
    for name,h in [('E-left-h20',.02),('B-left',.01),('E-left-h5',.005)]:
        final=load(DATA/(name+'.json'))['Result']['Riders'][0]
        convergence.append({'Step':h,'PositionDifferenceVersus5ms':math.hypot(final['Position']['X']-end5['Position']['X'],final['Position']['Y']-end5['Position']['Y']),
            'SpeedKph':final['Speed']*3.6,'Spin':final['AngularVelocity'],'FinalSlideDegrees':final['SlideRadians']*180/math.pi})
    short_gate={'Passed':qualified_short(short),'Reason':short['StopReason'],'LongRunExecuted':False,'Rule':'No long repeat after observed short-run crossing. No certified periodicity inferred.'};save('short-gate.json',short_gate)
    a={'Grade':'B','IsolatedPassed':isolated_pass,'Isolated':isolated,'Old':legacy_summary,'New':short,'Phases':phase,'Convergence':convergence,'IndependentTrackEnergy':energy_from_new(rows,short),
        'InitialBodyWheelExactlyMatched':True,'FirstNewPathErrorAbove1m':{k:first_error[k] for k in ['t','s','speed','steer_used']} if first_error else None,
        'HistoricalHashes':historical_hashes(),'OffFingerprint':load(DATA/'off-verification.json'),'ShortGate':short_gate,
        'SourcesSha256':{str(p.relative_to(ROOT)).replace('\\','/'):hashlib.sha256(p.read_bytes()).hexdigest().upper() for p in [OLD/'baseline-inner.json',OLD/'baseline-inner-tracking.csv.gz',OLD/'baseline-inner-forces.csv.gz',SLIDE/'baseline-inner-decomposition.csv.gz']}}
    save('analysis.json',a);return a,cache
def plots(a,cache):
    import matplotlib
    matplotlib.use('Agg')
    import matplotlib.pyplot as plt
    from matplotlib.patches import Circle,Rectangle
    plt.rcParams.update({'svg.hashsalt':'two-contact-stage1','font.size':10})
    def finish(fig,name):
        fig.savefig(DATA/(name+'.png'),dpi=150);fig.savefig(DATA/(name+'.svg'),metadata={'Date':None});plt.close(fig)
        p=DATA/(name+'.svg');p.write_text('\n'.join(s.rstrip() for s in p.read_text(encoding='utf-8').splitlines())+'\n',encoding='utf-8',newline='\n')
    fig,axes=plt.subplots(2,2,figsize=(12,8),layout='constrained')
    old=cache['old'];new=cache['new']
    axes[0,0].plot([r['t'] for r in old],[math.degrees(math.atan2(math.sin(r['body_heading']-r['velocity_heading']),math.cos(r['body_heading']-r['velocity_heading']))) for r in old],label='OFF — stary yaw',color='#177ca4')
    axes[0,0].plot([r['t'] for r in new],[math.degrees(r['beta']) for r in new],label='ON — dwa kontakty',color='#ba4b26')
    axes[0,0].set(title='Pierwszy łuk; ON zatrzymany na bandzie',xlabel='t [s]',ylabel='β [°]')
    for name in ['D-beta20','D-beta35','D-beta50','D-front-minus','D-front-plus']:
        rr=cache[name];axes[0,1].plot([r['t'] for r in rr],[math.degrees(r['beta']) for r in rr],label=name)
    axes[0,1].set(title='Kontrolowane duże kąty początkowe — bez podtrzymywania',xlabel='t [s]',ylabel='β [°]')
    axes[1,0].plot([r['t'] for r in new],[math.degrees(r['steer_used']) for r in new],label='kąt przedniego koła')
    axes[1,0].plot([r['t'] for r in new],[r['body_omega'] for r in new],label='yaw body [rad/s]')
    axes[1,0].set(xlabel='t [s]',ylabel='δ [°] / ω [rad/s]',title='Fizyczne sterowanie przednim kołem')
    for field,label in [('front_torque','τ przód'),('rear_torque','τ tył')]:axes[1,1].plot([r['t'] for r in new],[r[field] for r in new],label=label)
    axes[1,1].plot([r['t'] for r in new],[r['front_torque']+r['rear_torque'] for r in new],color='k',lw=1,label='suma r×F')
    axes[1,1].set(xlabel='t [s]',ylabel='Moment [Nm]',title='Bez osobnego aktuatora yaw')
    for ax in axes.flat:ax.grid(alpha=.25);ax.legend(fontsize=8)
    finish(fig,'slide-steering-torque')
    fig,axes=plt.subplots(2,2,figsize=(12,8),layout='constrained')
    axes[0,0].plot([r['t'] for r in old],[r['speed']*3.6 for r in old],label='OFF')
    axes[0,0].plot([r['t'] for r in new],[r['speed']*3.6 for r in new],label='ON');axes[0,0].set(xlabel='t [s]',ylabel='km/h',title='Wspólny początek, pełny gaz')
    for field,label in [('front_lateral','Fy przodu, układ koła'),('rear_lateral','Fy tyłu, układ bryły'),('rear_longitudinal','Fx tyłu')]:axes[0,1].plot([r['t'] for r in new],[r[field] for r in new],label=label)
    axes[0,1].set(xlabel='t [s]',ylabel='Siła [N]',title='Oddzielne siły kontaktu ON')
    for name in ['C-spin-ramp','C-coast']:
        rr=cache[name];axes[1,0].plot([r['t'] for r in rr],[r['wheelspin'] for r in rr],label=name)
    axes[1,0].set(xlabel='t [s]',ylabel='Slip ratio koła',title='Istniejące koło, zmiana Drive0→0,5→1')
    boundary=load(ROOT/'docs/experiments/progressive-bend-width/progressive-b-geometry.json')
    for edge,label in [('inner','kreda'),('outer','banda')]:
        points=[p[edge] for segment in boundary['segments'] for p in segment['sections']];axes[1,1].plot([p['x'] for p in points],[p['y'] for p in points],color='.65',lw=1,label=label)
    for rr,label,color in [(old,'OFF native','#177ca4'),(new,'ON native','#ba4b26')]:axes[1,1].plot([r['x'] for r in rr],[r['y'] for r in rr],label=label,color=color)
    g=read(OLD/'crossover-inner-geometry.csv');axes[1,1].plot([r['x'] for r in g],[r['y'] for r in g],ls='--',lw=.8,color='k',label='plan INNER')
    axes[1,1].set(aspect='equal',xlabel='X [m]',ylabel='Y [m]',title='Ta sama geometria i plan; ON przekracza bandę')
    for ax in axes.flat:ax.grid(alpha=.25);ax.legend(fontsize=8)
    finish(fig,'speed-forces-xy')
    fig,ax=plt.subplots(figsize=(10,3),layout='constrained');ax.add_patch(Rectangle((-1.05,-.15),2.1,.3,color='.85'));ax.add_patch(Circle((0,0),.055,color='k'))
    ax.plot([-.76,-.44],[0,0],lw=6,color='#177ca4');ax.plot([.54,.86],[-.03,.03],lw=6,color='#ba4b26')
    for x,label,color,direction in [(-.6,'Tył: r=−0,60 m','#177ca4',-.52),(.7,'Przód: r=+0,70 m','#ba4b26',.52)]:
        ax.annotate('',xy=(x,.55),xytext=(x,0),arrowprops={'arrowstyle':'->','color':color,'lw':2});ax.text(x,-.28,label,ha='center',color=color)
    ax.text(0,-.48,'CoM=(0,0); m=147 kg; I=52,72097 kg·m²',ha='center');ax.text(0,.78,'Schemat geometrii prowizorycznej — siły w punktach, bez dodatkowego torque',ha='center')
    ax.set(xlim=(-1.25,1.25),ylim=(-.65,1),aspect='equal');ax.axis('off');finish(fig,'contacts-schematic')
def manifest():save('manifest.json',{p.name:hashlib.sha256(p.read_bytes()).hexdigest().upper() for p in sorted(DATA.iterdir()) if p.is_file() and p.name!='manifest.json'})
if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('stage',nargs='?',default='analyse',choices=['analyse','isolated-gate'])
    parser.add_argument('--plots',action='store_true');parser.add_argument('--data',type=Path,default=DATA);args=parser.parse_args();DATA=args.data.resolve()
    if args.stage=='isolated-gate':print(json.dumps(isolated_gate(),indent=2));sys.exit(0)
    a,cache=analyse()
    if args.plots:plots(a,cache)
    manifest()
    print(json.dumps({'Grade':a['Grade'],'Phases':a['Phases'],'Old':a['Old'],'NewStop':a['New']['StopReason'],'FirstError':a['FirstNewPathErrorAbove1m'],'Convergence':a['Convergence'],'HistoricalHashes':sum(a['HistoricalHashes'].values())},indent=2))
