"""#106 reads native Box2D records. No dynamics/tyre law/speed integrator here."""
import bisect
import csv
import gzip
import hashlib
import importlib.util
import json
import math
from pathlib import Path
import shutil
import sys

ROOT=Path(__file__).resolve().parents[2]
DATA=ROOT/'docs/experiments/speed-grip-line-crossover'
LAP=318.77874994277954;HALF=LAP/2;ENTRY=35;EXIT=132.38937497138977
load=lambda p:json.loads(p.read_text(encoding='utf-8'))
spec=importlib.util.spec_from_file_location('root_metrics',ROOT/'tools/shared-world-speedway/racing-line-root-cause-report.py')
old=importlib.util.module_from_spec(spec);spec.loader.exec_module(old)

def read(path):
    with (gzip.open(path,'rt',encoding='utf-8') if path.suffix=='.gz' else path.open(encoding='utf-8')) as f:
        return [{k:float(v) for k,v in r.items()} for r in csv.DictReader(f)]

def save(name,value):
    (DATA/name).write_text(json.dumps(value,indent=2,allow_nan=False)+'\n',encoding='utf-8',newline='\n')

def interpolate(rows,s,key,axis='s'):
    values=[r[axis] for r in rows];i=max(0,min(len(rows)-2,bisect.bisect_right(values,s)-1));a,b=rows[i:i+2]
    u=(s-a[axis])/(b[axis]-a[axis]);return a[key]+u*(b[key]-a[key])

def straight_id(s):
    s=s%LAP
    return 1 if EXIT<=s<ENTRY+HALF else 2 if s>=EXIT+HALF or s<ENTRY else 0

def tracking_pass(s):
    return s['RmsLateral']<=.5 and s['MaximumLateral']<=1 and s['RmsVelocityHeadingDegrees']<=3 and s['MaximumVelocityHeadingDegrees']<=10

def rankable(complete,safe,stats,last_laps):
    return complete and safe and tracking_pass(stats) and len(last_laps)==3 and max(last_laps)/min(last_laps)<=1.005

def decompose(length,time,base_length,base_time):
    u=base_length/base_time;v=length/time
    return (length-base_length)/u,length*(1/v-1/u)

def analyse():
    cases=[];phases=[];cache={};rows_by_name={};metadata={}
    for file in ['control-runs.json','engine-runs.json','surface-runs.json','extreme-runs.json']:
        for config in load(DATA/file):
            name=config['Name'];run=config['Summary'];metadata[name]=config
            rows=read(DATA/(name+'-tracking.csv.gz'));forces=read(DATA/(name+'-forces.csv.gz'))
            assert len(rows)==len(forces) and all(r['t']==f['t'] for r,f in zip(rows,forces))
            cross=run['CrossingSeconds'];full=len(cross)==8;start=cross[-3] if full else 0;end=cross[-1] if full else run['Result']['TimeSeconds']
            stats=old.series_stats(rows,start,end);time_at,distance_at=old.time_and_distance_functions(rows,run)
            d=distance_at(end)-distance_at(start);window=[(r,f) for r,f in zip(rows,forces) if start<=r['t']<end]
            weights=sum(r['dt'] for r,f in window)
            mean=lambda fn:sum(fn(r,f)*r['dt'] for r,f in window)/weights
            contained=lambda r:r['inner']>=.05 and r['outer']>=.5
            physical=[r for r in rows if contained(r)]
            straight=[r for r in physical if straight_id(r['s'])]
            s1=[r for r,f in window if straight_id(r['s'])==1 and contained(r)];s2=[r for r,f in window if straight_id(r['s'])==2 and contained(r)]
            force_check=[]
            events={}
            for i,(r,f) in enumerate(zip(rows,forces)):
                after_speed=math.hypot(r['vx']+f['accel_x']*r['dt'],r['vy']+f['accel_y']*r['dt']);vm=(r['speed']+after_speed)/2
                required=f['mass']*vm*vm*abs(r['actual_curvature']);available=abs(f['normal_force'])
                force_check.append(abs(required-available))
                def event(key,condition,after=False):
                    if condition and key not in events:
                        events[key]={'Time':r['t']+(r['dt'] if after else 0),'BracketStart':r['t'],'BracketEnd':r['t']+r['dt'],
                                     'SpeedKph':3.6*(after_speed if after else r['speed']),'Reference':r['s'],'Offset':r['offset'],
                                     'OuterGapAfter':r['outer'],'PathCurvature':r['path_curvature'],'MotionCurvature':r['actual_curvature'],
                                     'RequiredPathAcceleration':r['speed']**2*abs(r['path_curvature']),'ActualNormalAcceleration':available/f['mass'],
                                     'BudgetAcceleration':r['budget']/f['mass'],'Fx':r['fx'],'Fy':r['fy'],'Utilization':r['utilization'],'WheelSlip':r['wheelspin'],'Yaw':r['yaw_energy']}
                event('RequiredPathAboveBudget',r['required_path_accel']>r['budget']/f['mass'])
                event('TrackingErrorAbove1m',abs(r['lateral_error'])>1)
                event('OuterClearanceBelowHalfMetre',r['outer']<.5,True)
                event('OuterFootprintCrossed',r['outer']<0,True)
                event('InnerFootprintCrossed',r['inner']<0,True)
            stats.update({'Name':name,'Group':file.removesuffix('-runs.json'),'Wide':config['Wide'],'Grip':config['Grip'],
                          'Engine':config['Engine'],'Drive':config['Drive'],'InitialSpeed':config['InitialSpeed'],'Step':config['Step'],
                          'Complete':full,'BoundarySafe':run['BoundarySafe'],'Rankable':rankable(full,run['BoundarySafe'],stats,run['LapSeconds'][-3:]),
                          'LastLapSeconds':run['LapSeconds'][-3:],'TimingSeconds':(end-start)/2 if full else None,'DistancePerLap':d/2 if full else None,
                          'MeanSpeed':d/(end-start),'MaximumContainedStraightKph':max((r['speed']*3.6 for r in straight),default=None),
                          'FlyingStraight1MaxKph':max((r['speed']*3.6 for r in s1),default=None),'FlyingStraight2MaxKph':max((r['speed']*3.6 for r in s2),default=None),
                          'MaximumAnySpeedKph':max(r['speed']*3.6 for r in rows),
                          'MaximumTyreUtilization':max(r['utilization'] for r in rows),
                          'MaximumSlideDegrees':max(abs(math.atan2(math.sin(r['body_heading']-r['velocity_heading']),math.cos(r['body_heading']-r['velocity_heading'])))*180/math.pi for r in rows),
                          'MaximumAbsoluteFxN':max(abs(r['fx']) for r in rows),'MaximumAbsoluteFyN':max(abs(r['fy']) for r in rows),
                          'MaximumActualForceResidualN':max(f['body_force_residual'] for f in forces),'MaximumYawResidualNm':max(abs(f['yaw_residual']) for f in forces),
                          'MaximumLinearImpulseResidualNs':max(f['body_force_residual']*f['dt'] for f in forces),
                          'MaximumVelocityIncrementResidual':max(f['body_force_residual']*f['dt']/f['mass'] for f in forces),
                          'MaximumAngularImpulseResidualNms':max(abs(f['yaw_residual'])*f['dt'] for f in forces),
                          'MinimumAcceptedStep':min(f['dt'] for f in forces),
                          'MaximumCurvatureForceDifferenceN':max(force_check),'MeanCurvatureForceDifferenceN':sum(force_check)/len(force_check),
                          'MeanLateralLossWatts':mean(lambda r,f:f['lateral_loss']/r['dt']),
                          'FlyingLateralLossJoulesPerMeter':sum(f['lateral_loss'] for r,f in window)/d,
                          'FlyingLongitudinalLossJoulesPerMeter':sum(f['longitudinal_loss'] for r,f in window)/d,
                          'MaximumNetAcceleration':max((f['force_x']*r['vx']+f['force_y']*r['vy'])/(f['mass']*max(.01,r['speed'])) for r,f in zip(rows,forces)),
                          'MaximumDeliveredPowerKw':max(f['delivered_engine_torque']*(f['omega_before']+f['omega_after'])/2000 for f in forces),
                          'MinimumOuter':run['MinimumOuter'],'ConservativeOuter':run['ConservativeOuter'],'MinimumInner':run['MinimumInner'],'ConservativeInner':run['ConservativeInner'],
                          'Energy':run['Energy'],'Events':events})
            stats['Rejections']=[label for label,okay in [('incomplete',full),('footprint/between-step',run['BoundarySafe']),('tracking',tracking_pass(stats)),('not periodic within0.5%',full and max(run['LapSeconds'][-3:])/min(run['LapSeconds'][-3:])<=1.005)] if not okay]
            if full:
                for cycle in range(12,16):
                    origin=cycle*HALF;entry=ENTRY+origin;exit=EXIT+origin;corner=[r for r in rows if entry<=r['s']<=exit]
                    if not corner:continue
                    next_entry=entry+HALF;complete_straight=next_entry<=rows[-1]['s'];straight_rows=[r for r in rows if exit<=r['s']<=next_entry]
                    phase={'Name':name,'Cycle':cycle,'Corner':1 if cycle%2==0 else 2,
                           'EntryKph':interpolate(rows,entry,'speed')*3.6,'P25Kph':interpolate(rows,entry+(EXIT-ENTRY)*.25,'speed')*3.6,
                           'P50Kph':interpolate(rows,entry+(EXIT-ENTRY)*.5,'speed')*3.6,'P75Kph':interpolate(rows,entry+(EXIT-ENTRY)*.75,'speed')*3.6,
                           'MinimumCornerKph':min(r['speed']*3.6 for r in corner),'ExitKph':interpolate(rows,exit,'speed')*3.6,
                           'StraightComplete':complete_straight,'StraightMaxKph':max(r['speed']*3.6 for r in straight_rows) if complete_straight else None,
                           'CornerContained':all(contained(r) for r in corner),
                           'WindowSeconds':time_at(32+origin+HALF)-time_at(32+origin),
                           'WindowMeters':distance_at(time_at(32+origin+HALF))-distance_at(time_at(32+origin))}
                    phases.append(phase)
                own=[p for p in phases if p['Name']==name]
                for key in ['EntryKph','P25Kph','P50Kph','P75Kph','MinimumCornerKph','ExitKph']:
                    stats[key]=sum(p[key] for p in own)/len(own)
            cases.append(stats)
            sample=[i for i,r in enumerate(rows) if start<=r['t']<=end]
            take=sample[::5] if full else sample
            cache[name]=([rows[i] for i in take],[forces[i] for i in take]);rows_by_name[name]=None
    pairs=[]
    names={r['Name']:r for r in cases}
    for prefix in ['control','baseline','power2','power3','power4','power6','grip85','grip70','grip55']:
        a,b=names[prefix+'-inner'],names[prefix+'-wide'];qualified=a['Rankable'] and b['Rankable'];delta=b['TimingSeconds']-a['TimingSeconds']
        distance,speed=decompose(b['DistancePerLap'],b['TimingSeconds'],a['DistancePerLap'],a['TimingSeconds'])
        pairs.append({'Configuration':prefix,'Grip':a['Grip'],'Engine':a['Engine'],'Drive':a['Drive'],
                      'MaximumContainedStraightKph':max(x for x in [a['MaximumContainedStraightKph'],b['MaximumContainedStraightKph']] if x is not None),
                      'FlyingMaximumContainedStraightKph':max((x for c in [a,b] for x in [c['FlyingStraight1MaxKph'],c['FlyingStraight2MaxKph']] if x is not None),default=None),
                      'InnerSeconds':a['TimingSeconds'],'WideSeconds':b['TimingSeconds'],'Difference':delta,
                      'Qualified':qualified,'Winner':('INNER' if delta>0 else 'WIDE STRAIGHT') if qualified else None,
                      'DistanceCostSeconds':distance,'SpeedEffectSeconds':speed,
                      'InnerLateralLossJPerMeter':a['FlyingLateralLossJoulesPerMeter'],'WideLateralLossJPerMeter':b['FlyingLateralLossJoulesPerMeter'],
                      'Reason':'; '.join(sorted(set(a['Rejections']+b['Rejections'])))})
    convergence=[]
    for speed in [160,200]:
        reference=cache[f'stress{speed}-h5'][0]
        for h in [20,10,5]:
            name=f'stress{speed}-h{h}';rows=cache[name][0];case=names[name]
            at=.5;xy=math.hypot(interpolate(rows,at,'x','t')-interpolate(reference,at,'x','t'),interpolate(rows,at,'y','t')-interpolate(reference,at,'y','t'))
            convergence.append({'Name':name,'PositionDifferenceAtHalfSecondVersus5ms':xy,'SpeedAtHalfSecondKph':interpolate(rows,at,'speed','t')*3.6,
                                'FirstOuterFootprint':case['Events'].get('OuterFootprintCrossed'),'MaximumForceResidualN':case['MaximumActualForceResidualN']})
    history={}
    for name in ['autonomous-racing-line','slip-coupled-dynamics','slip-control-validation','physics-driven-racing-line','rear-wheel-dynamics','track-geometry-variants','local-offset-limit','progressive-bend-width','racing-line-root-cause','inverse-feasibility-matched-apex']:
        folder=ROOT/'docs/experiments'/name;manifest=load(folder/'manifest.json')
        for file,digest in manifest.items():assert hashlib.sha256((folder/file).read_bytes()).hexdigest().lower()==digest.lower(),(name,file)
        history[name]=len(manifest)
    a={'Cases':cases,'Pairs':pairs,'Convergence':convergence,'HistoricalHashes':history,'Crossover':'INCONCLUSIVE',
       'Finding':'No qualified crossover in full-drive engine/grip matrix; reject apparent wins outside track. Slower validation pair is qualified and INNER wins.',
       'Grade':'B','EngineSurfaceSelection':load(DATA/'surface-freeze.json')}
    save('analysis.json',a)
    with (DATA/'phases.csv').open('w',newline='',encoding='utf-8') as f:
        w=csv.DictWriter(f,fieldnames=list(phases[0]));w.writeheader();w.writerows(phases)
    save('viewer-paths.json',[{'name':name,'points':[{'x':r['x'],'y':r['y']} for r in read(DATA/(name+'-geometry.csv'))[::10]]} for name in ['crossover-inner','crossover-wide-straight']])
    for alias,name in [('viewer-inner','control-inner'),('viewer-wide','control-wide'),('viewer-stress','stress200-h5')]:
        for suffix in ['.json','-accepted.csv']:shutil.copyfile(DATA/(name+suffix),DATA/(alias+suffix))
    return a,cache

def plots(a,cache):
    import matplotlib
    matplotlib.use('Agg')
    import matplotlib.pyplot as plt
    plt.rcParams.update({'font.size':9,'axes.grid':True,'grid.alpha':.2,'figure.dpi':140,'svg.hashsalt':'speed-grip106'})
    def finish(fig,name):
        fig.tight_layout();fig.savefig(DATA/(name+'.png'));fig.savefig(DATA/(name+'.svg'),metadata={'Date':None});plt.close(fig)
        p=DATA/(name+'.svg');p.write_text('\n'.join(s.rstrip() for s in p.read_text(encoding='utf-8').splitlines())+'\n',encoding='utf-8',newline='\n')
    fig,axes=plt.subplots(2,1,figsize=(12,8),sharex=True)
    for prefix,color in [('control','#333333'),('baseline','#2984a6'),('power3','#cc6a26'),('grip55','#7c4b95')]:
        for suffix,style in [('inner','-'),('wide','--')]:
            rows=cache[prefix+'-'+suffix][0];cut=[r for r in rows if 32+6*LAP<=r['s']<=32+7*LAP]
            axes[0].plot([r['s']-6*LAP for r in cut],[r['speed']*3.6 for r in cut],ls=style,color=color,label=prefix+' '+suffix)
            axes[1].plot([r['s']-6*LAP for r in cut],[r['lateral_error'] for r in cut],ls=style,color=color)
    axes[0].set(ylabel='actual km/h',title='Lap7 / all curves diagnostic unless both gates pass');axes[0].legend(ncol=4,fontsize=7)
    axes[1].set(xlabel='reference m',ylabel='tracking error m');finish(fig,'speed-and-tracking')
    fig,axes=plt.subplots(2,2,figsize=(12,8))
    for col,name in enumerate(['baseline-inner','stress200-h5']):
        rows,forces=cache[name]
        if col==0:
            cut=[(r,f) for r,f in zip(rows,forces) if 32+6*LAP<=r['s']<=32+7*LAP];rows=[r for r,f in cut];forces=[f for r,f in cut]
        x=[r['s']-6*LAP if col==0 else r['t'] for r in rows]
        for label,values,style in [('required actual m*v²*k',[f['mass']*r['speed']**2*abs(r['actual_curvature']) for r,f in zip(rows,forces)],'-'),('applied normal force',[abs(f['normal_force']) for f in forces],'--'),('tyre budget',[r['budget'] for r in rows],':'),('required intended path',[f['mass']*r['speed']**2*abs(r['path_curvature']) for r,f in zip(rows,forces)],'-.')]:
            axes[0,col].plot(x,values,ls=style,label=label)
        axes[0,col].set_title(name);axes[0,col].set_ylabel('N');axes[0,col].legend(fontsize=6)
        axes[1,col].plot(x,[r['actual_curvature'] for r in rows],label='motion curvature');axes[1,col].plot(x,[r['path_curvature'] for r in rows],label='intended curvature',ls='--')
        axes[1,col].set(xlabel='reference m' if col==0 else 'time s',ylabel='1/m');axes[1,col].legend(fontsize=7)
    finish(fig,'forces-and-curvature')
    fig,axes=plt.subplots(1,2,figsize=(12,6))
    boundary=load(ROOT/'docs/experiments/progressive-bend-width/progressive-b-geometry.json')
    for ax in axes:
        for edge in ['inner','outer']:
            points=[p[edge] for s in boundary['segments'] for p in s['sections']];ax.plot([p['x'] for p in points],[p['y'] for p in points],color='.65',lw=1)
        ax.set_aspect('equal');ax.set(xlabel='x m',ylabel='y m')
    for name,color in [('control-inner','#333333'),('control-wide','#277fa3')]:
        rows=cache[name][0];cut=[r for r in rows if r['s']<=32+7*LAP];axes[0].plot([r['x'] for r in cut],[r['y'] for r in cut],color=color,label=name)
        g=read(DATA/('crossover-'+('inner' if name.endswith('inner') else 'wide-straight')+'-geometry.csv'));axes[0].plot([r['x'] for r in g],[r['y'] for r in g],color=color,ls='--',lw=1)
    for name,color in [('stress160-h5','#c97725'),('stress200-h5','#b74755')]:
        rows=cache[name][0];axes[1].plot([r['x'] for r in rows],[r['y'] for r in rows],label=name,color=color)
    axes[0].set_title('Qualified slower A/B: planned dashed / native solid');axes[1].set_title('Rejected overspeed / no invisible wall')
    for ax in axes:ax.legend(fontsize=7)
    finish(fig,'actual-trajectories')
    fig,axes=plt.subplots(1,2,figsize=(12,5))
    for pair in a['Pairs']:
        ax=axes[1] if pair['Configuration'].startswith('grip') or pair['Configuration']=='baseline' else axes[0]
        x=pair['Grip'] if ax==axes[1] else pair['MaximumContainedStraightKph']
        for key,color in [('InnerSeconds','#333333'),('WideSeconds','#277fa3')]:
            ax.scatter(x,pair[key],c=color,marker='o' if pair['Qualified'] else 'x',label=key if pair['Configuration'] in ['control','baseline'] else None)
            ax.annotate(pair['Configuration'],(x,pair[key]),fontsize=6,xytext=(3,5 if key=='WideSeconds' else -9),textcoords='offset points')
    axes[0].set(xlabel='all-run maximum contained straight km/h',ylabel='seconds/lap',title='Circles qualified / crosses REJECTED timings');axes[1].set(xlabel='Grip (fixed original engine)',ylabel='seconds/lap',title='Grip experiment: tracking gate still applies')
    for ax in axes:ax.legend(fontsize=7)
    finish(fig,'time-speed-grip')
    fig,axes=plt.subplots(3,1,figsize=(12,9),sharex=True)
    by={c['Name']:c for c in a['Cases']}
    for speed,color in [(160,'#c97725'),(200,'#b74755')]:
        name=f'stress{speed}-h5';rows,forces=cache[name];t=[r['t'] for r in rows]
        axes[0].plot(t,[r['speed']*3.6 for r in rows],color=color,label=name)
        axes[1].plot([r['t']+r['dt'] for r in rows],[r['outer'] for r in rows],color=color)
        axes[2].plot(t,[r['offset'] for r in rows],color=color)
        for label,style in [('RequiredPathAboveBudget',':'),('OuterFootprintCrossed','--')]:
            event=by[name]['Events'].get(label)
            if event:
                for ax in axes:ax.axvline(event['Time'],color=color,ls=style,alpha=.7)
    axes[0].set_title('Vertical dotted: first path demand > budget; dashed: first footprint outside')
    axes[0].legend();axes[0].set_ylabel('actual km/h');axes[1].axhline(0,color='black');axes[1].axhline(.5,color='black',ls=':');axes[1].set_ylabel('outer full-footprint gap m');axes[2].set(xlabel='time s (h5ms)',ylabel='actual offset m')
    finish(fig,'extreme-first-violation')

def report(a):
    cases={c['Name']:c for c in a['Cases']};pairs={p['Configuration']:p for p in a['Pairs']}
    geometry=load(DATA/'geometry-summary.json');real=load(DATA/'real-evidence.json');mapping=load(DATA/'grip-force-map.json')
    fmt=lambda x:'—' if x is None else f'{x:.3f}'
    table='\n'.join(f"|{p['Configuration']} / {p['Drive']:.6f}|{fmt(p['FlyingMaximumContainedStraightKph'])}|{p['Grip']:.2f}|{p['InnerSeconds']:.6f}|{p['WideSeconds']:.6f}|{p['Difference']:+.6f}|{p['Winner'] if p['Qualified'] else 'ODRZUCONE: '+p['Reason']}|" for p in a['Pairs'])
    speed_table='\n'.join(f"|{c['Name']}|{fmt(c['FlyingStraight1MaxKph'])}/{fmt(c['FlyingStraight2MaxKph'])}|{c['EntryKph']:.2f}|{c['P25Kph']:.2f}|{c['P50Kph']:.2f}|{c['P75Kph']:.2f}|{c['MinimumCornerKph']:.2f}|{c['ExitKph']:.2f}|{c['MeanSpeed']*3.6:.2f}|{c['DistancePerLap']:.3f}|" for c in a['Cases'] if c['Complete'])
    tracking='\n'.join(f"|{c['Name']}|{c['RmsLateral']:.3f}/{c['MaximumLateral']:.3f}|{c['RmsVelocityHeadingDegrees']:.2f}/{c['MaximumVelocityHeadingDegrees']:.2f}|{c['MinimumInner']:.3f}/{c['ConservativeInner']:.3f}|{c['MinimumOuter']:.3f}/{c['ConservativeOuter']:.3f}|{'PASS' if c['Rankable'] else 'FAIL'}|" for c in a['Cases'] if c['Complete'])
    power='\n'.join(f"|{c['Name']}|{c['MaximumContainedStraightKph']:.2f}|{c['MaximumAnySpeedKph']:.2f}|{c['MaximumNetAcceleration']:.3f}|{c['MaximumDeliveredPowerKw']:.3f}|{c['MaximumWheelspin']:.3f}|{c['MaximumTyreUtilization']:.3f}|{c['YawForceClampedPercent']:.2f}/{c['YawEnergyClampedPercent']:.2f}|" for c in a['Cases'] if c['Group']=='engine')
    grip_table='\n'.join(f"|{r['Grip']:.2f}|{r['EffectiveGrip']:.4f}|{r['GripBudgetNewtons']:.3f}|{r['AccelerationBudget']:.4f}|{r['DriveForceNewtons']:.3f}|{r['AvailableTorqueNewtonMeters']:.3f}|{math.sqrt(r['AccelerationBudget']/geometry[0]['MaximumAbsoluteCurvature'])*3.6:.2f}|" for r in mapping if not r['Turn'])
    decomposition='\n'.join(f"|{p['Configuration']}|{p['DistanceCostSeconds']:+.6f}|{p['SpeedEffectSeconds']:+.6f}|{p['InnerLateralLossJPerMeter']:.3f}/{p['WideLateralLossJPerMeter']:.3f}|{'ranking dopuszczony' if p['Qualified'] else 'diagnostyka, bez rankingu'}|" for p in a['Pairs'])
    stress='\n'.join(f"|{c['Name']}|{c['Events']['RequiredPathAboveBudget']['Time']:.4f}|{c['Events']['TrackingErrorAbove1m']['Time']:.4f}|{c['Events']['OuterFootprintCrossed']['Time']:.4f}|{c['Events']['OuterFootprintCrossed']['SpeedKph']:.2f}|{1/c['Events']['OuterFootprintCrossed']['MotionCurvature']:.2f}|{c['Events']['OuterFootprintCrossed']['RequiredPathAcceleration']:.2f}/{c['Events']['OuterFootprintCrossed']['ActualNormalAcceleration']:.2f}|" for c in a['Cases'] if c['Group']=='extreme')
    convergence='\n'.join(f"|{c['Name']}|{c['PositionDifferenceAtHalfSecondVersus5ms']:.6f}|{c['SpeedAtHalfSecondKph']:.4f}|{c['FirstOuterFootprint']['Time']:.4f}|" for c in a['Convergence'])
    first='\n'.join(f"|{c['Name']}|{fmt(c['Events'].get('RequiredPathAboveBudget',{}).get('Time'))}|{fmt(c['Events'].get('TrackingErrorAbove1m',{}).get('Time'))}|{c['MaximumSlideDegrees']:.2f}|{c['MaximumAbsoluteFxN']:.1f}/{c['MaximumAbsoluteFyN']:.1f}|{c['NearGripPercent']:.1f}/{c['SteeringClampedPercent']:.1f}|" for c in a['Cases'] if c['Group']=='engine')
    control=pairs['control'];n=sum(a['HistoricalHashes'].values());max_energy=max(abs(c['Energy']['BalanceResidualJoules']) for c in a['Cases'])
    text=f'''# Speed, Grip & Racing Line Crossover Experiment (#106)

**Nie wykazano poprawnego punktu przecięcia.** W zamrożonej macierzy pełnego napędu żadna para A/B nie spełnia jednocześnie trackingu, bezpieczeństwa i okresowości. Pozorna wygrana B dla power2 pochodzi z odrzuconych tras poza torem. Jedyna dopuszczona para — wolniejsza kontrola wykonalności — daje INNER przewagę **{control['Difference']:.6f} s/lap**. Nie jest to dowód, że crossover nie istnieje przy innym, fizycznie wykonalnym sterowaniu.

**Nadmierna prędkość jest prawidłowo wynoszona.** Przy starcie 200 km/h pierwszy pełny footprint przecina bandę po około 0,61 s przy 185,48 km/h; promień rzeczywistego ruchu ≈ 202 m, planowany ≈ 30 m. Dostępna siła zakrzywia prędkość zbyt słabo, aby wykonać plan. Nie dodano siły odśrodkowej, hamulca, teleportacji, limitera prędkości ani ściany bandy.

Ocena **B**: powtarzalny audyt zależności prędkość–krzywizna–siła i poprawne odrzucenie niewykonanych linii; brak zatwierdzonej kalibracji lub crossoveru. Baza Draft #105 **3dd410254e7957b1e5d8604c82eff4b229a28d75**, zweryfikowana lokalnie i zdalnie. Jeden Draft z `experiment/speed-grip-line-crossover` do `experiment/inverse-feasibility-matched-apex`, bez merge. Produkcyjne `src/CoreSim` i dane kalibracyjne niezmienione.

## 1. Dane rzeczywiste oraz zakres wniosków

[Oficjalny przewodnik telemetrii](https://ekstraliga.pl/se/news/telemetria-w-pge-ekstralidze-przewodnik) opisuje maksima mierzone w biegu i odnotowane w testach 140 km/h **w Tarnowie**. Wskazuje też, że maksima często przypadają na wejścia w łuki. Nie publikuje numerycznej krzywej wejście/środek/wyjście dla Motoareny;140 km/h nie jest lokalnym targetem łuku tego toru.

[Oficjalny opis Motoareny](https://ekstraliga.pl/se/news/czy-podczas-pge-imme-im-zenona-plecha-padnie-nowy-rekord-toru) podaje długość 318 m i opublikowany rekord czterech okrążeń 56,50 s. Iloraz 1272/56,50 ≈ 22,513 m/s (81,048 km/h) jest średnią po nominalnej linii pomiarowej, **nie prędkością w łuku**, a rzeczywista droga motocykla może być dłuższa. Progressive-b jest wcześniejszym modelem badawczym, bez geodezyjnego potwierdzenia rzeczywistej krzywizny i nachylenia toru.

Wykorzystano istniejący `RealWorldCalibrationDataset.ParseCsv` i niezmieniony snapshot pge-v1. 5328 rekordów CleanPhysics: Vmax P10/P50/P90/P99={real['All']['VmaxP10']:.1f}/{real['All']['VmaxP50']:.1f}/{real['All']['VmaxP90']:.1f}/{real['All']['VmaxP99']:.1f} km/h. Dokładne, osobne selektory źródła:

- Toruń, 2025:289 clean rekordów; Vmax mediana 113,4 i P99 ≈ 119,96 km/h, flying-lap mediana 14,82 s.
- Motoarena im. Mariana Rosego, 2026:407 clean rekordów; mediana 113,7 i P99 ≈ 119,58 km/h, flying-lap mediana 14,87 s.

Są to obwiednie tego snapshotu, nie limity ani pomiary prędkości wewnątrz łuku. `curve_speed` / `speed_2s` w snapshotcie są **rankingami pól**, nie indywidualną prędkością. SHA pliku i pełne grupy zapisano w [real-evidence.json](speed-grip-line-crossover/real-evidence.json). Nie dopisano syntetycznych danych telemetrycznych.

## 2. Zamrożone A/B: pełne C3 XY

Tor: niezmienione progressive-b. A utrzymuje offset ≈ 1 m. B trzyma 1 m przez wejście i apex 50%; od apexu otwiera do 5 m przy exit+10 m, utrzymuje 5 m do exit+25 m, potem wraca do 1 m przy następnym entry−5 m. Początek pierwszego świata reference 32 m, offset 1 m,18 m/s: nie ma początkowego wyjazdu na zewnątrz przed pierwszym łukiem. Takie samo prawo path-preview i wszystkie gains dla A/B.

Rozszerzono istniejący `AuthoredRacingPath`, bez równoległego systemu geometrii. B używa źródłowego septic smoothstep `35u⁴−84u⁵+70u⁶−20u⁷`. **Całe XY**, również nad węzłami C2 reference, jest okresowym interpolantem septic Hermite: wspólne P/P′/P″/P‴ na węzłach co ≤ 1 m, stałe jet samples±0,25/0,5 m. Nie wystarczyłoby wygładzić tylko d(s), co wykazał #105. Nowe funkcje są opt-in, stare cztery rodziny oraz matched-apex #105 zachowują poprzedni rachunek i fingerprinty.

Testy sprawdzają wszystkie węzły, domknięcie, niezależne różnice XY/κ, ciągłość κ′ po długości **pełnego Q**, brak wczesnego otwarcia i pełne kapsuły przy wielu yaw. Geometryczne certyfikaty dowolnego yaw: banda minimum ≈ 4,919 m, kerb ≈ 0,923 m. Faktyczne bezpieczeństwo ocenia osobno rzeczywisty yaw/ślad.

|Plan|Długość/lap m|max abs κ /m|min R m|Geometry|
|---|---:|---:|---:|---|
|A INNER|{geometry[0]['LengthMeters']:.6f}|{geometry[0]['MaximumAbsoluteCurvature']:.8f}|{geometry[0]['MinimumRadiusMeters']:.4f}|PASS|
|B INNER CORNER / WIDE STRAIGHT|{geometry[1]['LengthMeters']:.6f}|{geometry[1]['MaximumAbsoluteCurvature']:.8f}|{geometry[1]['MinimumRadiusMeters']:.4f}|PASS|

B dodaje 6,412 m/lap. Otwarcie zmniejsza fragmenty krzywizny na wyjściu, lecz powrót na krótkiej prostej ma własny koszt krzywizny. To jedna sensowna, umiarkowana linia 5 m, nie maksymalny offset ani zoptymalizowana trasa. Wolniejsze native A/B dowodzą jej praktycznej wykonalności przy osiągniętych prędkościach, bez korekt stanu. C3 nie gwarantuje wykonalności przy dowolnym v.

## 3. Silnik, przełożenie i procedura

[freeze.json](speed-grip-line-crossover/freeze.json) powstał przed pomiarami. Macierz: oryginalny napęd oraz cztery mocniejsze konfiguracje. `TorqueScale`={', '.join(str(e['TorqueScale']) for e in load(DATA/'freeze.json')['Engines'] if e)}; odpowiednie `FinalRatio`=0,9/0,8/0,7/0,6. Koło, inertia, opona, opór `40+0,20v²`, masa 147kg i regulator są zamrożone. Modyfikuje się wyłącznie engine envelope i jego fizyczne przełożenie, w narzędziowym `WorldParameters.ExperimentalDrive`, null/OFF domyślnie.

`F_available_new(v_wheel)=TorqueScale·q·F_original(q·v_wheel)`; stąd moc na wale skaluje się przez TorqueScale, a q zamienia moment na prędkość obrotową. Nadal jeden bieg, oryginalna funkcja fade i oryginalny rear contact. Delivered torque jest ograniczony envelope przy **rω**, nie przy prędkości chassis. Zwiększenie mocy nie zwiększa B ani limitu Fx. Wyjściowa siła na ciało nadal pochodzi wyłącznie z `RearWheelDynamics.Evaluate`; nie dodaje się drugiej siły silnika.

Pełny Drive 1, Grip 1,18 m/s rolling start, 8lap, nominalnie10 ms/4substeps. Te same warunki A/B. Prędkości łuków są wynikiem Box2D, nie profilem zadanym. Osiem okrążeń zamrożono przed wynikami; analizowane końcowe dwa, okresowość końcowych trzech ≤ 0,5%. Historyczne macierze nie są powtarzane.

Osobna para `control-*` używa starej równowagi prostej 18 m/s i stałego maintenance Drive ≈ 0,269187. To wymagane poprawne przykłady i konstruktywna kontrola C3, **nie zamiennik próby pełnego napędu** ani sposób przypisania wolnego wyniku silnikowi o nowej mocy.

|Konfiguracja / linia|max na zawartej fizycznie prostej km/h|max gdziekolwiek km/h|max a netto m/s²|max delivered shaft kW|max wheel slip|max utilization|yaw physical/energy clamp %|
|---|---:|---:|---:|---:|---:|---:|---:|
{power}

Nie osiągnięto kontrolowanego, bezpiecznego zakresu130–140 km/h. Power3 dochodzi do około 110 km/h, power4 do około 119 km/h na jeszcze zawartej fizycznie prostej, ale następne przejazdy nie wykonują zadanej linii. Power6 osiąga134 km/h **poza torem**, a jego poprawnie zawarty start ma tylko ≈ 68 km/h. Nie wolno używać134 jako Vmax poprawnego przejazdu. Źródła ograniczeń: skończona długość 62 m, niższa prędkość wyjściowa, tylny budżet Fx ≤ 0,6B, wheelspin/shaft fade, opór oraz wymaganie następnego łuku. Oryginalny baseline ma max ≈ 82,7 km/h i szczyt delivered power ≈ 8,45 kW; sam nie odtwarza obserwowanego rozkładu Vmax.

Nawet optymistyczna granica przyspieszenia prosto wynosi `0,6B/m−F_res/m`, czyli maksymalnie8,727 m/s² przed oporem przyGrip1; to konieczna górna granica, nie przewidywany profil motocykla. Wyników mocy/accel nie nazywamy pomiarem rzeczywistego silnika. Ten eksperyment rolling nie kalibruje reakcji lub0–70 km/h ze startu.

## 4. Główna tabela crossover i bramki

Pierwsze przekroczenie koniecznego `v²·abs(κ_path) > B/m` występuje przed błędem bocznym ponad 1 m w każdej parze pełnego napędu. W baseline A jest to 8,70 s wobec 8,85 s, w power3 już 0,39 s wobec 1,76 s. W tym stanie zadana linia wymaga niedostępnej siły nawet przed uwzględnieniem napędu. Yaw physical/energy clamp pozostaje 0%; nie ma dowodu, że zwiększenie momentu yaw usunie ten brak siły. To potwierdzone ograniczenie przy tym profilu prędkości; nie dowód, że każdy mniejszy błąd preview pochodzi wyłącznie z overspeed.

|Próba|pierwsze a_path>B/m s|pierwszy błąd>1 m s|max slide °|max abs Fx/Fy N|util ≥95% / demand ≥95%B, % końcowych 2 lap|
|---|---:|---:|---:|---:|---:|
{first}

Near-grip oznacza utilization ≥0,95, a demand to surowe żądanie sterowania ponad 0,95B, nie dodatkowa siła. Oryginalna konstytutywna opona oddaje mniej niż cały skalarny B także przy dużym slide; utilization poniżej 1 nie gwarantuje wykonalności planu. Pełne Fx/Fy, yaw requested/authority/energy i wheelspin są zachowane dla każdego native kroku.

Warunki rankingu: pełne 8 lap, cały przejazd bezpieczny (accepted kerb ≥ 0,05 m / banda ≥ 0,5 m i konserwatywny between-step bound ≥ 0 / ≥ 0,5 m), końcowe trzy czasy max/min ≤ 1,005, ostatnie dwa tracking RMS ≤ 0,5 m/max ≤ 1 m i kierunek v RMS ≤ 3°/max ≤ 10°. Obie strony muszą przejść. To zamrożone progi #104/#105, nie kara dla INNER.

|Konfiguracja / Drive|max prostej lotnej zawartej km/h|Grip|INNER s/lap|WIDE STRAIGHT s/lap|B−A s/lap|Wynik|
|---|---:|---:|---:|---:|---:|---|
{table}

**Wszystkie czasy odrzucone są wyłącznie diagnostyczne.** Maksimum w tej tabeli dotyczy końcowych dwóch lotnych okrążeń; „—” oznacza brak fizycznie zawartych próbek. Wcześniejsza tabela napędu pokazuje osobno maksimum całego świata, włącznie z przejściem początkowym. Po opuszczeniu toru projekcja na reference może nadal zliczać „okrążenia”; nie oznacza to prawidłowego przejechania toru. Whole-run safety/tracking wykluczają takie czasy. Nie użyto ich jako przecięcia. Przy baseline B rzeczywiste accepted minima pozostają dodatnie, ale between-step bound nie certyfikuje kredy — to **brak certyfikatu**, nie stwierdzenie zaobserwowanego przecięcia kredy.

|Próba|RMS/max lateral m|RMS/max kierunku v °|kerb min/bound m|banda min/bound m|łączny ranking|
|---|---:|---:|---:|---:|---|
{tracking}

Nie wykonano dodatkowego wyszukiwania granicy, ponieważ nie ma dwóch dopuszczonych punktów o przeciwnym znaku ΔT. Wniosek **INCONCLUSIVE dla istnienia crossoveru**, nie uniwersalne „INNER zawsze wygrywa”.

![Czasy, moc i Grip: krzyżyki to odrzucone wyniki](speed-grip-line-crossover/time-speed-grip.png)

## 5. Prędkości obu łuków i prostych

Wszystkie wartości są z rzeczywistych prędkości. `phases.csv` podaje **osobno obydwa łuki** Lap7/8, wejście,25/50/75%, minimum, wyjście i każdą kompletną następną prostą. Poniżej średnia czterech łuków i maksima obu prostych. „—” oznacza brak fizycznie zawartych lotnych próbek danej prostej, nie zero ani osiągnięcie targetu. Linie odrzucone zachowują diagnostyczne landmarki projekcji; nie są prędkościami wykonanej strategii.

|Próba|max prosta1/2 km/h|entry|25%|50%|75%|min|exit|średnia km/h|actual m/lap|
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
{speed_table}

Bezpieczny i śledzący kontrolny INNER osiąga wejście ≈ 50,60 / środek ≈ 48,41 / wyjście ≈ 46,96 km/h. Baseline pełnego napędu, choć zawarty, nie przechodzi dokładnego trackingu: ≈ 82,72 /68,07 /67,79 km/h. Nie przedstawiamy żadnej z tych liczb jako oficjalnej telemetrii Motoareny. Konieczna grip-only prędkość w najciaśniejszym miejscu Q_A przyGrip1 wynosi ≈ 69,3 km/h; nie jest pełnym dowodem osiągalności z yaw/wheel/napędem.

![Prędkość i błąd w funkcji pozycji](speed-grip-line-crossover/speed-and-tracking.png)
![Pełne XY: poprawne A/B oraz odrzucone próby ekstremalne](speed-grip-line-crossover/actual-trajectories.png)

## 6. Faktyczna siła boczna i ekstremalne wejścia

Raportujemy **dwa różne wymagania**: `m·v²·abs(κ_actual)` z wykonanego ruchu oraz `m·v²·abs(κ_path)` potrzebne do zadanej linii przy tej prędkości. Pierwsze jest porównane z normalną składową **rzeczywiście przyłożonego globalnego F**, drugie z budżetem opony. Body Fy samo nie jest całym force-normal, ponieważ obrócone Fx także wnosi składową normalną. Kierunek ruchu, heading ciała i slide są osobne.

Native `*-forces.csv.gz` zawiera F_global, Δv/h, Δω/h, torque, prawdziwą pracę silnika i straty. Jest oryginalny scalar wheel, Box2D jest jedynym integratorem. Nie ma osobnej siły odśrodkowej ani niewidzialnego hamulca. Przy nadmiernym v rzeczywista κ maleje (duży promień), a ślad odchodzi od planu i przekracza granicę.

|Próba|t: required path>B|t: błąd>1 m|t: pierwszy footprint poza bandą|v wtedy km/h|R_actual m|a_path/a_actual m/s²|
|---|---:|---:|---:|---:|---:|---:|
{stress}

To initialized stress 160/200, Drive 0, oryginalna fizyka. Prędkość nie jest utrzymywana, ale także nie spada do prędkości bezpiecznej przed bandą. Przy200/h5 w chwili naruszenia a_path ≈ 88,12 m/s², actual ≈ 13,13 m/s², B/m ≈ 14,546 m/s², utilization ≈ 0,943. Opona nie dostarcza88 m/s²; motocykl jedzie po znacznie większym promieniu. Tym samym wszystkie pięć kontroli z pytania o „200 po ciasnym łuku” ma konkretną odpowiedź: prędkość nadal wysoka, rzeczywista krzywizna mała, siły nie przekraczają B, Δv wynika z zapisanych F, a pomiar pokazuje wyjazd poza tor.

Inicjalizację stresu skorygowano **przed pierwszym stress replay** z reference 34 na 33,75 m: C3 interpolation zaczyna wygładzać przed reference join; dotychczasowy guard równowagi odrzucił nienulową pochodną przy 34. [extreme-freeze.json](speed-grip-line-crossover/extreme-freeze.json) zapisuje wybór najbliższego dokładnie płaskiego punktu siatki0,25 m wyłącznie z geometrii. Prędkość i ω są tylko legalnym stanem początkowym; później brak resetu.

![Wymagana/wykonana krzywizna i faktyczne siły](speed-grip-line-crossover/forces-and-curvature.png)
![Moment pierwszego naruszenia, prędkość i offset](speed-grip-line-crossover/extreme-first-violation.png)

### Krok, round-off i energia

|Próba|różnica XY przy 0,5 s względem5 ms m|v przy 0,5 s km/h|pierwszy zewnętrzny footprint t s|
|---|---:|---:|---:|
{convergence}

Zmniejszenie20→10→5 ms daje zbieżność pozycji do milimetrów oraz tego samego naruszenia; podawany czas jest pierwszym accepted endpointem, z bracketingiem pojedynczego interwału w analysis.json. Safety dodatkowo używa konserwatywnego bound całego footprintu między krokami, z prędkością i yaw. Nie traktujemy dodatnich endpointów jako wystarczającego CCD.

Istniejący event solver czasem dzieli nominalny10 ms na kroki mikrosekundowe. Dzielenie float32 Δv przez 6 µs daje lokalnie pozorny residual37,52 N; odpowiada to jedynie0,000225 N·s. Dlatego raportujemy też **residual impulsu**, nie ukrywamy krótkich kroków. Maksimum wszystkich 24 prób: ≤ {max(c['MaximumLinearImpulseResidualNs'] for c in a['Cases']):.6f} N·s, czyli ≤ {max(c['MaximumVelocityIncrementResidual'] for c in a['Cases']):.8f} m/s różnicy w kroku. Jest to skala float32, nie dodatkowa siła. W stress h5 residual siłowy ≤ 0,379 N, a h-convergence potwierdza wynik. Dla yaw osobno zapisano residual impulsu kątowego.

Cały ledger body+wheel: maksymalny abs residual energii={max_energy:.6f} J, guard excess0 i utilization ≤ 1. Brak niewyjaśnionego dopływu energii; nie dowodzi to empirycznej poprawności opony. Obserwacje/phase extraction/wykresy nie wpływają na stany.

## 7. Grip przy zamrożonym napędzie

Po macierzy zgodnie z regułą [surface-freeze](speed-grip-line-crossover/surface-freeze.json) wybrano oryginalny baseline: żaden silnik nie miał obu boundary-certified przejazdów. Nie ma fizycznie uzasadnionego, poprawnego 130–140, które można zamrozić. Grip 1 wykorzystuje istniejące dwa wyniki baseline, nowe są tylko 0,85/0,70/0,55. Napęd, Drive 1 i regulator nie zmieniają się.

Oddzielny fixed-state probe przy 18 m/s mierzy mapowanie wartości parametru na **oryginalną siłę**. Moisture0,5 i Ruts0 sprawiają, że EffectiveGrip nie jest równy deklarowanemu Grip. Kolumna Fx dotyczy tego samego wheel-slip stanu1,1·u, beta0; pełne probe w łuku beta0,15 także w JSON.

|Grip|EffectiveGrip|B N|B/m m/s²|Fx prosta N|available wheel torque Nm|konieczny v przy max κ_A km/h|
|---|---:|---:|---:|---:|---:|---:|
{grip_table}

Spadek Grip 1→0,55 zmniejsza B o około 18,9%, a nie45%; to wynik istniejącego nieliniowego capability, **Grip nie jest współczynnikiem μ**. Dostępny moment napędu też maleje, a rear contact reaguje na oba kierunki. W native baseline A max prostej spada 82,72→77,90 km/h, RMS trackingu rośnie 0,456→0,723 m; B 0,535→1,007 m. Niższa prędkość może polepszyć clearance mimo mniejszej dostępnej siły, dlatego bezpieczeństwo samo nie dowodzi większej przyczepności. Te dane nie kalibrują realnego μ; bez rzeczywistej krzywej łuków nie można uznać absolutnego Grip za błędny ani dobrać go do czasu.

## 8. Droga, prędkość i straty

Tożsamość `ΔT=(L_B−L_A)/vbar_A + L_B(1/vbar_B−1/vbar_A)` rozdziela actual distance od actual mean speed. Straty slip podano na metr, żeby dłuższej trasy nie mylić automatycznie z gorszą oponą. Nie ma arbitralnych bonusów lub kar. W `phases.csv` są okna od3 m przed wejściem przez łuk do 3 m przed następnym wejściem, które partycjonują dwa lotne obiegi; pełne proste i oba łuki mają osobne landmarki.

|Para|koszt drogi s/lap|efekt prędkości s/lap|lateral slip J/m A/B|interpretacja|
|---|---:|---:|---:|---|
{decomposition}

W dopuszczonej wolniejszej parze B odzyskuje tylko około 0,0327 s dzięki średniej prędkości, wobec ≈ 0,4510 s kosztu drogi. Mniejsze straty slide B nie rekompensują dodatkowej drogi. Dla pełnego napędu nie wolno na tej podstawie ogłosić zwycięzcy: trajektorie nie są wykonane. Zamiast kolejnego strojenia linii audyt wskazuje overspeed względem jej siły i ograniczenie dostępnych wejść.

## Odpowiedzi, rekomendacja i jeden następny krok

1. **Realizm prędkości:** baseline ≈ 82,7 km/h nie odtwarza mediany Vmax ≈ 113–114 km/h Motoareny w snapshotcie. Mocniejsze napędy mają osiągi bliższe tym maksimom, ale nie dają poprawnego przejazdu planu. Przyspieszenie rolling jest zmierzone w tabeli, nie podmienione na dane 0–70.
2. **Prosta:** ograniczają ją engine/shaft fade przy rω, dostępny tylny Fx, wheelspin, opór, skończona prosta i prędkość wyjścia. Zwiększenie momentu nie zwiększa przyczepności.
3. **Łuki:** jedyne kwalifikowane ślady mają około 47–51 km/h; pełny baseline wykonuje około 66–83 km/h, lecz przekracza próg trackingu. Grip-only najciaśniejszy punkt to około 62–69 km/h w badanym Grip; pełna fizyczna/praktyczna obwiednia pozostaje niezatwierdzona.
4. **Overspeed:** tak, utrzymuje dużą prędkość, zwiększa promień i wychodzi poza bandę; numeryka i F→Δv to potwierdzają. Nie ma niefizycznego ciasnego łuku przy 200.
5. **Grip kontra regulator/silnik:** mapa Grip→B i moment jest spójna z obecnym modelem; brak dowodu błędu siły lub ukrytego actuatora. Absolutna kalibracja Grip pozostaje nieustalona. Potwierdzony problem eksperymentu pełnego napędu to żądanie κ przy v wymagającym niedostępnego a oraz brak ograniczania napędu do przyszłej wykonalności; pozostały błąd preview trzeba oceniać dopiero przy legalnym profilu.
6. **Crossover:** nie uzyskano dopuszczonego przecięcia; istnienie przy innych legalnych profilach INCONCLUSIVE. Poprawna wolniejsza kontrola wygrywa INNER. Nie fabrykujemy granicy z odrzuconego power2.
7. **Dalsza kalibracja:** zachować Grip 1 jako kontrolę i 0,85/0,70/0,55 jako jawne sensitivity, nie podnosić go w celu utrzymania 110–140 w ciasnej. Docelowa obwiednia Vmax do sprawdzenia najpierw ≈ 109–120 km/h wynika z lokalnych rekordów snapshotu,130–140 traktować jako zewnętrzną obwiednię,160/200 tylko stress. Badane power2/3 (szczyty delivered ≈ 17/26 kW) oraz zmierzone szczyty przyspieszenia netto 3,58/4,97 m/s² są kandydatami do kolejnej diagnozy napędu, nie zatwierdzonym balansem; wymagane rolling acceleration musi mieścić się w zmierzonym rear-traction/opór, bez nowych hamulców. Nie rekomendujemy wdrożenia żadnego nowego mnożnika ani Grip na podstawie odrzuconego czasu.

**Jeden następny PR:** opt-in longitudinal feasibility preview dla `Drive∈[0,1]`, wykorzystujący pełną Q(s), oryginalne wheel/engine i rzeczywiście dostępne wytracanie prędkości po odjęciu gazu. Zapobiega potwierdzonemu przyspieszaniu do niewykonalnego wejścia. Bez speed clamp, nowego hamulca, dodatkowych sił, zmiany opony lub gains. Acceptance: planowany i native profil zgodne w granicach oryginalnego napędu/energii, A/B bezpieczne i śledzące przez 8 lap, a dopiero potem ranking. Jeżeli samo roll-off nie wystarcza na 62 m, raportować brak wykonalnej prędkości zamiast wymuszać target.

## Dostarczenie i walidacja

24 nowe jednoosobowe światy:10 engine,2 kontrolne,6 nowych Grip (Grip 1 reuse),6 stress/convergence. Bez czterech motocykli, kontaktów lub taktycznego AI. Licznik `AiUpdates` oznacza istniejący zegar; decyzje taktyczne, pary, kontakty i manewry AI wynoszą 0. Fizyczne równania opony/koła/yaw nie zmieniają się; nowa gałąź engine opt-in tylko dla pojedynczej C3 trajektorii. Surowe 10 ms CSV są kompresowane bez utraty danych, accepted CSV ≈ 0,1 s służą viewerowi.

**{n}/{n} historycznych SHA-256 zachowanych**,6 fingerprintów #103,1 #104,1 #105 oraz nowa observed/plain parity. `verification.json` i `targeted-tests.json` dokumentują kontrolę. Standardowe końcowe Draft Windows CI jest podane w PR. Testy obejmują wszystkie C3 węzły/κ′, niezależne XY, footprint, mechanikę przełożenia, brak speed resetu, body/wheel force/energy budget, naturalne wyniesienie, tracking i determinizm. Nie powtarzano dawnych wielkich macierzy.

Istniejący replay viewer ma trzy aliasy: `?case=crossover-viewer-inner`, `?case=crossover-viewer-wide` (kwalifikowane wolniejsze kontrole) i `?case=crossover-viewer-stress` (200/h5, odrzucony). Pokazuje oryginalne accepted poses i planowane C3, nie prognozę.

```powershell
dotnet restore
dotnet build -c Release --no-restore --warnaserror
# Nowy pusty katalog, każda faza przed następną:
dotnet tools/shared-world-speedway/bin/Release/net8.0/shared-world-speedway.dll speed-grip-crossover ../crossover-reproduction freeze
# Kolejno: geometry, real-data, controls, engines, surface-freeze, surfaces,
# extreme-freeze, extremes, probes, verify
python tools/shared-world-speedway/speed-grip-crossover-report.py --data ../crossover-reproduction --plots
```

Python nie implementuje fizyki ani nie zmienia ciał. --plots wymaga matplotlib; analiza i testy używają standardowej biblioteki. Nowe surowe eksporty odmawiają nadpisania, stare dowody i dataset pozostają bez zmian. Bez --data analiza czyta dostarczony katalog repozytorium; z --data analizuje świeży katalog reprodukcji i zapisuje raport obok niego.
'''
    text=text.replace('(speed-grip-line-crossover/',f'({DATA.name}/')
    (DATA.parent/(DATA.name+'.md')).write_text(text,encoding='utf-8',newline='\n')

if __name__=='__main__':
    if '--data' in sys.argv:DATA=Path(sys.argv[sys.argv.index('--data')+1]).resolve()
    analysis,cache=analyse()
    if '--plots' in sys.argv:plots(analysis,cache)
    report(analysis)
    save('manifest.json',{p.name:hashlib.sha256(p.read_bytes()).hexdigest() for p in sorted(DATA.iterdir()) if p.is_file() and p.name!='manifest.json'})
    print('Analysed',len(analysis['Cases']),'native worlds;',sum(analysis['HistoricalHashes'].values()),'historical hashes retained.')
