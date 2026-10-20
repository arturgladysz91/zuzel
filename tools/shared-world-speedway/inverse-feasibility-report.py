"""#105: audit saved ORIGINAL-equation probes and native Box2D captures.
No force law, speed integrator, braking model or trajectory correction lives here.
"""
import bisect
import csv
import hashlib
import importlib.util
import json
import math
from pathlib import Path
import sys

ROOT=Path(__file__).resolve().parents[2]
DATA=ROOT/'docs/experiments/inverse-feasibility-matched-apex'
load=lambda p:json.loads(p.read_text(encoding='utf-8'))
spec=importlib.util.spec_from_file_location('root_cause_metrics',ROOT/'tools/shared-world-speedway/racing-line-root-cause-report.py')
old=importlib.util.module_from_spec(spec);spec.loader.exec_module(old)
read=old.read_csv

def save(name,value):
    (DATA/name).write_text(json.dumps(value,indent=2,allow_nan=False)+'\n',encoding='utf-8',newline='\n')

def tracking_pass(s):
    return s['RmsLateral']<=.5 and s['MaximumLateral']<=1 and s['RmsVelocityHeadingDegrees']<=3 and s['MaximumVelocityHeadingDegrees']<=10

def strategy_time_gate(inverse,tracking,safe,lap2,lap3):
    return inverse=='PASS' and tracking and safe and abs(lap3-lap2)/lap3<=.005

def distance_decomposition(length,time,base_length,base_time):
    base_speed=base_length/base_time;speed=length/time
    return (length-base_length)/base_speed,length*(1/speed-1/base_speed)

def interpolate(rows,s,key):
    axis=[r['s'] for r in rows];i=max(0,min(len(rows)-2,bisect.bisect_right(axis,s)-1))
    a,b=rows[i:i+2];u=(s-a['s'])/(b['s']-a['s']);return a[key]+u*(b[key]-a[key])

def path_length(rows,start,end):
    # Trapezoid on the exported metric, including exact cut boundaries.
    points=[(start,interpolate(rows,start,'metric'))]+[(r['s'],r['metric']) for r in rows if start<r['s']<end]+[(end,interpolate(rows,end,'metric'))]
    return sum((b[0]-a[0])*(a[1]+b[1])/2 for a,b in zip(points,points[1:]))

def half_response(rows,start,end,target):
    segment=[r for r in rows if start<=r['s']<=end]
    for a,b in zip(segment,segment[1:]):
        if a['offset']>=target>=b['offset'] and a['offset']>b['offset']:
            u=(a['offset']-target)/(a['offset']-b['offset'])
            return a['s']+u*(b['s']-a['s']),a['t']+u*(b['t']-a['t'])
    return None

def inverse_stats(rows):
    def first(fn):
        found=next((r for r in rows if fn(r)),None)
        return found
    inertia=load(DATA/'entry-proof.json')[0]['Inertia'];wp_radius=.33;wp_inertia=.9;wp_bearing=.02
    for r in rows:
        r['engine_fx_ceiling']=(r['available_drive_torque']-wp_inertia*r['wheel_required_rate']-wp_bearing*r['omega'])/wp_radius
        rate=inertia/.01;root=math.sqrt(r['yaw_rate']**2+2*r['energy_credit']/rate)
        r['guard_min']=rate*(-r['yaw_rate']-root);r['guard_max']=rate*(-r['yaw_rate']+root)
    return {'MaximumForceResidualN':max(r['force_residual'] for r in rows),
            'MaximumScalarGripFraction':max(r['speed']**2*abs(r['k'])/(r['budget']/147) for r in rows),
            'MinimumDrive':min(r['drive'] for r in rows),'MaximumDrive':max(r['drive'] for r in rows),
            'MaximumBetaDegrees':max(abs(math.degrees(r['beta'])) for r in rows),
            'MaximumOmega':max(r['omega'] for r in rows),'MaximumWheelSlip':max(r['wheel_slip'] for r in rows),
            'MaximumUtilization':max(r['utilization'] for r in rows),'MaximumRequiredYawTorque':max(abs(r['required_yaw_torque']) for r in rows),
            'FirstEngineFailure':first(lambda r:r['drive']>1.001 or r['drive']<-.001),
            'FirstYawFailure':first(lambda r:abs(r['required_yaw_torque'])>r['available_yaw_torque']+1),
            'FirstGuardFailure':first(lambda r:r['required_yaw_torque']<r['guard_min']-1 or r['required_yaw_torque']>r['guard_max']+1),
            'FirstSteeringFailure':first(lambda r:abs(r['required_target_slip'])>r['max_target_slip']+.001),
            'MinimumRequiredTorque':min(r['required_drive_torque'] for r in rows),
            'MaximumRequiredTorque':max(r['required_drive_torque'] for r in rows),
            'MinimumAvailableTorque':min(r['available_drive_torque'] for r in rows),
            'Status':'INCONCLUSIVE',
            'Meaning':'Local regular forward-slide branch, continuous-time constitutive inverse. Engine/yaw violations reject this candidate; no positive result is a constructive native proof.'}

def analyse():
    geometry=load(DATA/'geometry-summary.json');lap=geometry[0]['ReferenceLapMeters'];entry=35;exit=entry+(lap-124)/2;apex=(entry+exit)/2
    inv={};inv_rows={}
    for file in sorted(DATA.glob('*-inverse-*.csv')):
        rows=read(file);inv_rows[file.stem]=rows;inv[file.stem]=inverse_stats(rows)
    paths={g['Name']:read(DATA/(g['Name']+'-geometry.csv')) for g in geometry}
    for g in geometry:
        rows=paths[g['Name']];g['SegmentLength']=path_length(rows,20,exit+15)
        window=[r for r in rows if 20<=r['s']<=exit+15]
        g['SegmentMaximumCurvature']=max(abs(r['curvature']) for r in window)
        g['KinematicLap18']=g['LengthMeters']/18;g['KinematicSegment18']=g['SegmentLength']/18
        g['GeometryStatus']='PASS' if g['GeometricallySafe'] else 'FAIL'
    runs=load(DATA/'runs.json');cases=[];phases=[];raw={}
    for run in runs:
        rows=read(DATA/(run['Case']+'-tracking.csv'));raw[run['Case']]=rows;cross=run['CrossingSeconds'];t0,t1=cross[0],cross[2]
        stats=old.series_stats(rows,t0,t1);flying=[r for r in rows if t0<=r['t']<=t1]
        time_at,distance_at=old.time_and_distance_functions(rows,run)
        duration=t1-t0;distance=distance_at(t1)-distance_at(t0)
        stats.update({'Case':run['Case'],'Path':run['AuthoredPath'],'Safety':run['BoundarySafe'],
                      'TrackingStatus':'PASS' if tracking_pass(stats) else 'FAIL',
                      'Lap2':run['LapSeconds'][1],'Lap3':run['LapSeconds'][2],'Lap2Plus3':duration,'Distance':distance,
                      'LapRelativeChange':abs(run['LapSeconds'][2]-run['LapSeconds'][1])/run['LapSeconds'][2],
                      'TimeStatus':'INCONCLUSIVE','InverseStatus':'INCONCLUSIVE',
                      'MeanSlideDegrees':sum(abs(math.degrees(math.atan2(math.sin(r['body_heading']-r['velocity_heading']),math.cos(r['body_heading']-r['velocity_heading']))))*r['dt'] for r in flying)/sum(r['dt'] for r in flying),
                      'MinimumFlyingSpeed':min(r['speed'] for r in flying),'MaximumFlyingSpeed':max(r['speed'] for r in flying),
                      'MaximumUtilization':max(r['utilization'] for r in flying),
                      'MaximumAppliedYaw':max(abs(r['yaw_energy']) for r in flying)})
        # Two matched windows per lap; reset is outside the compared window, inside full-lap cost.
        windows=[]
        for cycle in range(6):
            origin=cycle*lap/2;start=20+origin;end=exit+15+origin
            if start<20+lap-1e-6 or end>rows[-1]['s']:continue
            corner=[r for r in rows if entry+origin<=r['s']<=exit+origin]
            if not corner:continue
            minimum=min(corner,key=lambda r:r['offset']);ta=time_at(start);tb=time_at(end)
            anchor=apex+origin
            phase={'Case':run['Case'],'Cycle':cycle,'Seconds':tb-ta,'Distance':distance_at(tb)-distance_at(ta),
                   'EntrySpeed':interpolate(rows,entry+origin,'speed'),'MinimumBendSpeed':min(r['speed'] for r in corner),
                   'ExitSpeed':interpolate(rows,exit+origin,'speed'),'ActualMinimumOffset':minimum['offset'],
                   'ActualMinimumAtPercent':100*(minimum['s']-(entry+origin))/(exit-entry),
                   'AtPlannedApexOffset':interpolate(rows,anchor,'offset'),
                   'AtPlannedApexXYError':math.hypot(interpolate(rows,anchor,'x')-interpolate(rows,anchor,'path_x'),interpolate(rows,anchor,'y')-interpolate(rows,anchor,'path_y'))}
            path=run['AuthoredPath'];early=path.endswith('-early');outside=8.5 if 'wide' in path or path=='outside-inside-outside' else 5 if 'mid' in path else 1
            phase['HalfLagSeconds']=None;phase['HalfLagReferenceMeters']=None
            if outside>1:
                tightening=(20 if early else 35)+origin;planned=(tightening+anchor)/2;apex_offset=2 if path=='outside-inside-outside' else 1
                response=half_response(rows,max(start,tightening-15),anchor,(outside+apex_offset)/2)
                if response:
                    phase['HalfLagSeconds']=response[1]-time_at(planned);phase['HalfLagReferenceMeters']=response[0]-planned
            windows.append(phase);phases.append(phase)
        stats['Windows']=windows
        for key in ['Seconds','Distance','EntrySpeed','MinimumBendSpeed','ExitSpeed','ActualMinimumOffset','ActualMinimumAtPercent','AtPlannedApexOffset','AtPlannedApexXYError']:
            stats['WindowMean'+key]=sum(w[key] for w in windows)/len(windows)
        delays=[w['HalfLagSeconds'] for w in windows if w['HalfLagSeconds'] is not None]
        stats['WindowMeanHalfLagSeconds']=sum(delays)/len(delays) if delays else None
        base=cases[0] if cases else stats
        stats['DistanceCostSeconds'],stats['SpeedEffectSeconds']=distance_decomposition(distance,duration,base['Distance'],base['Lap2Plus3'])
        stats['StrategyTimeGate']=strategy_time_gate(stats['InverseStatus'],tracking_pass(stats),stats['Safety'],stats['Lap2'],stats['Lap3'])
        cases.append(stats)
    history={}
    for name in ['autonomous-racing-line','slip-coupled-dynamics','slip-control-validation','physics-driven-racing-line','rear-wheel-dynamics','track-geometry-variants','local-offset-limit','progressive-bend-width','racing-line-root-cause']:
        folder=ROOT/'docs/experiments'/name;manifest=load(folder/'manifest.json')
        for file,digest in manifest.items():assert hashlib.sha256((folder/file).read_bytes()).hexdigest().lower()==digest.lower(),(name,file)
        history[name]=len(manifest)
    analysis={'Geometry':geometry,'Inverse':inv,'Cases':cases,'HistoricalHashes':history,
              'ConstructiveNativeProof':'INCONCLUSIVE','WheelComparisonExecuted':False,'StrategyWinner':None,
              'ExactRegularOIO18':'FAIL','ExactRegularOIO15':'FAIL',
              'Caution':'Exact continuous-path regular-branch obstruction is separate from practical native tracking within #104 tolerances. No metre-scale impossibility proof.'}
    analysis['StabilityScreen']={'RelativeLapTolerance':.005,'PreRegistered':False,'Purpose':'auxiliary report diagnostic only; inverse gate is independently unresolved'}
    save('analysis.json',analysis)
    with (DATA/'phases.csv').open('w',newline='',encoding='utf-8') as w:
        writer=csv.DictWriter(w,fieldnames=list(phases[0]));writer.writeheader();writer.writerows(phases)
    viewer_paths=[{'name':g['Name'],'points':[{'x':r['x'],'y':r['y']} for r in paths[g['Name']][::10]]} for g in geometry]
    save('viewer-paths.json',viewer_paths)
    return analysis,paths,inv_rows,raw

def plots(a,paths,inv_rows,raw):
    import matplotlib
    matplotlib.use('Agg')
    import matplotlib.pyplot as plt
    plt.rcParams.update({'font.size':9,'axes.grid':True,'grid.alpha':.2,'figure.dpi':140})
    def finish(fig,name):
        fig.tight_layout();fig.savefig(DATA/(name+'.png'));fig.savefig(DATA/(name+'.svg'));plt.close(fig)
        svg=DATA/(name+'.svg');svg.write_text('\n'.join(line.rstrip() for line in svg.read_text(encoding='utf-8').splitlines())+'\n',encoding='utf-8',newline='\n')
    fig,axes=plt.subplots(2,2,figsize=(12,8))
    colors=['#222222','#277da8','#cc661f','#31a296','#a64a99']
    boundary=load(ROOT/'docs/experiments/progressive-bend-width/progressive-b-geometry.json')
    for edge in ['inner','outer']:
        xy=[p[edge] for seg in boundary['segments'] for p in seg['sections']];axes[0,0].plot([p['x'] for p in xy],[p['y'] for p in xy],color='.7',lw=1)
    for (name,rows),color in zip(paths.items(),colors):
        cut=[r for r in rows if 20<=r['s']<=147.4]
        axes[0,0].plot([r['x'] for r in cut],[r['y'] for r in cut],label=name.replace('matched-',''),color=color)
        for ax,key in [(axes[0,1],'curvature'),(axes[1,0],'ay_at18')]:ax.plot([r['s'] for r in rows],[r[key] for r in rows],label=name.replace('matched-',''),color=color)
        g=next(g for g in a['Geometry'] if g['Name']==name)
        axes[1,1].scatter(g['SegmentLength'],g['SegmentMaximumCurvature'],color=color,label=name.replace('matched-',''))
    axes[0,0].set_aspect('equal');axes[0,0].set_title('Same apex XY and shared exit; window only');axes[0,0].legend(fontsize=7)
    axes[0,1].set(xlabel='reference m (full lap includes reset)',ylabel='path curvature 1/m')
    axes[1,0].axhline(14.546245,color='red',ls='--',label='necessary scalar grip budget');axes[1,0].set(xlabel='reference m',ylabel='required ay at18 m/s²');axes[1,0].legend(fontsize=7)
    axes[1,1].set(xlabel='planned window length m',ylabel='window max |curvature| 1/m',title='Distance cost / worst curvature');finish(fig,'matched-geometry')
    fig,axes=plt.subplots(3,2,figsize=(12,10),sharex=True)
    for column,name in enumerate(['inner','outside-inside-outside']):
        rows=inv_rows[name+'-inverse-18'];s=[r['s'] for r in rows]
        axes[0,column].plot(s,[r['fx'] for r in rows],label='required body Fx')
        axes[0,column].plot(s,[r['engine_fx_ceiling'] for r in rows],label='engine ceiling at required omega_dot')
        axes[1,column].plot(s,[r['fy'] for r in rows],label='required body Fy')
        axes[1,column].plot(s,[math.sqrt(max(0,r['budget']**2-r['fx']**2)) for r in rows],ls='--',label='scalar circle upper bound ONLY')
        axes[2,column].plot(s,[r['required_yaw_torque'] for r in rows],label='required yaw (knot spikes)')
        axes[2,column].plot(s,[r['available_yaw_torque'] for r in rows],color='red',label='physical torque bound')
        axes[2,column].plot(s,[-r['available_yaw_torque'] for r in rows],color='red')
        axes[2,column].plot(s,[r['controller_energy_torque'] for r in rows],label='unchanged controller at candidate')
        axes[0,column].set_title(name+' / local regular branch at18')
        for row in range(3):axes[row,column].legend(fontsize=6);axes[row,column].set_ylabel('N' if row<2 else 'Nm')
        axes[2,column].set_xlabel('reference m')
    finish(fig,'required-and-available')
    fig,axes=plt.subplots(2,2,figsize=(12,8))
    for case,color in zip(a['Cases'][:5],colors):
        name=case['Case'];run=load(DATA/(name+'.json'));rows=raw[name];lap=a['Geometry'][0]['ReferenceLapMeters'];cut=[r for r in rows if 20+lap<=r['s']<=147.4+lap]
        axes[0,0].plot([r['path_x'] for r in cut],[r['path_y'] for r in cut],color=color,ls='--',lw=1)
        axes[0,0].plot([r['x'] for r in cut],[r['y'] for r in cut],color=color,label=case['Path'].replace('matched-',''))
        axes[0,1].plot([r['s']-lap for r in cut],[r['speed'] for r in cut],color=color)
        axes[1,0].plot([r['s']-lap for r in cut],[r['lateral_error'] for r in cut],color=color)
        axes[1,1].scatter(case['Distance'],case['Lap2Plus3'],color=color)
    axes[0,0].set_aspect('equal');axes[0,0].set_title('Lap2 first window: planned dashed / native solid');axes[0,0].legend(fontsize=7)
    axes[0,1].set(xlabel='reference m',ylabel='native speed m/s',title='Same legal constant maintenance throttle')
    axes[1,0].set(xlabel='reference m',ylabel='lateral error m')
    axes[1,1].set(xlabel='actual Lap2+3 distance m',ylabel='diagnostic Lap2+3 time s',title='No strategy winner: inverse gate unresolved')
    finish(fig,'native-tracking')
    fig,axes=plt.subplots(3,1,figsize=(12,8),sharex=True)
    for name,color in [('inner-steady-15','#222222'),('outside-inside-outside-steady-15','#277da8')]:
        rows=raw[name];lap=a['Geometry'][0]['ReferenceLapMeters'];cut=[r for r in rows if 20+lap<=r['s']<=20+2*lap]
        s=[r['s']-lap for r in cut]
        axes[0].plot(s,[r['steering_demand'] for r in cut],color=color,label=name+' command')
        preview=read(DATA/(name+'-preview.csv'));preview=[r for r in preview if 20+lap<=r['s']<=20+2*lap]
        axes[0].plot([r['s']-lap for r in preview],[r['feed_forward_acceleration'] for r in preview],color=color,ls='--',label='full-path preview feed-forward')
        axes[0].plot(s,[r['speed']**2*r['actual_curvature'] for r in cut],color=color,ls=':',label='actual v²*k_motion')
        axes[1].plot(s,[r['lateral_error'] for r in cut],color=color,label='lateral error m')
        axes[1].plot(s,[math.degrees(r['velocity_heading_error']) for r in cut],color=color,ls=':',label='velocity-heading error deg')
        axes[2].plot(s,[r['yaw_energy'] for r in cut],color=color,label='applied yaw Nm')
    for ax in axes:ax.legend(fontsize=7)
    axes[2].set_xlabel('reference m / Lap2');axes[0].set_ylabel('m/s²');axes[2].set_ylabel('Nm');finish(fig,'lower-speed-response')

def report(a):
    g=a['Geometry'];cases=a['Cases'];inv=a['Inverse'];proof=load(DATA/'entry-proof.json');knots=load(DATA/'knot-probes.json')
    exit=35+(g[0]['ReferenceLapMeters']-124)/2;apex=(35+exit)/2
    gt='\n'.join(f"|{r['Name'].replace('matched-','')}|{r['LengthMeters']:.3f}|{r['SegmentLength']:.3f}|{r['MaximumAbsoluteCurvature']:.6f}|{r['SegmentMaximumCurvature']:.6f}|{r['MinimumRadiusMeters']:.3f}|{r['MaximumAyAt18']:.3f}|{r['KinematicLap18']:.3f}/{r['KinematicSegment18']:.3f}|" for r in g)
    it='\n'.join(f"|{name}|{r['MaximumScalarGripFraction']:.3f}|{r['MinimumDrive']:.3f}/{r['MaximumDrive']:.3f}|{r['MaximumBetaDegrees']:.3f}|{r['MaximumOmega']:.3f}|{r['MaximumWheelSlip']:.3f}|{r['MaximumUtilization']:.3f}|{r['MaximumForceResidualN']:.4f}|" for name,r in inv.items())
    lag=lambda r:'—' if r['WindowMeanHalfLagSeconds'] is None else f"{r['WindowMeanHalfLagSeconds']:.4f}"
    ct='\n'.join(f"|{r['Case']}|{r['RmsLateral']:.3f}/{r['MaximumLateral']:.3f}|{r['RmsVelocityHeadingDegrees']:.3f}|{r['WindowMeanEntrySpeed']:.3f}/{r['WindowMeanMinimumBendSpeed']:.3f}/{r['WindowMeanExitSpeed']:.3f}|{r['WindowMeanAtPlannedApexOffset']:.3f}|{lag(r)}|{r['TrackingStatus']}|PASS|" for r in cases)
    tt='\n'.join(f"|{r['Case']}|{r['Lap2Plus3']:.6f}|{r['Distance']:.3f}|{r['WindowMeanSeconds']:.3f}/{r['WindowMeanDistance']:.3f}|{100*r['LapRelativeChange']:.3f}%|{r['DistanceCostSeconds']:.3f}|{r['SpeedEffectSeconds']:.3f}|INCONCLUSIVE|" for r in cases)
    pt='\n'.join(f"|{r['Case']}|{r['MeanFx']:.1f}/{r['MeanFy']:.1f}|{r['MeanSlideDegrees']:.2f}|{r['MaximumWheelspin']:.4f}|{r['MaximumUtilization']:.3f}|{r['YawForceClampedPercent']:.2f}/{r['YawEnergyClampedPercent']:.2f}|{r['MaximumAppliedYaw']:.2f}|" for r in cases)
    runs=load(DATA/'runs.json')
    safety='\n'.join(f"|{r['Case']}|{r['MinimumInner']:.3f}/{r['ConservativeInner']:.3f}|{r['MinimumOuter']:.3f}/{r['ConservativeOuter']:.3f}|{r['Energy']['BalanceResidualJoules']:.6f}|{r['Energy']['MaximumYawGuardExcessWatts']}|" for r in runs)
    oio=inv['outside-inside-outside-inverse-18'];eng=oio['FirstEngineFailure'];inner=inv['inner-inverse-18'];ieng=inner['FirstEngineFailure']
    data=f'''# Inverse Feasibility & Matched-Apex Validation (#105)

**Potwierdzono niezależne ograniczenie dynamiczne regularnej realizacji dokładnej OIO przy18m/s.** C2 w XY nie wystarcza temu modelowi: na wejściu i apexie skacze wymagany yaw rate ciała. Ponadto stan realizujący lokalny bilans sił potrzebuje miejscami niedostępnego momentu koła. Samo `v²κ≤a_grip` przechodzi dla OIO, ale nie stanowi dowodu wykonalności.

**Wykonalność w tolerancjach trackingu #104 pozostaje INCONCLUSIVE.** Nie utożsamiamy matematycznej przeszkody dokładnej Q(t) ze źródłem całego błędu rzędu metrów. Nie skonstruowano legalnego rozwiązania native dla zadanego stałego profilu; nie wolno jeszcze izolować pozostałego błędu regulatora na podstawie założenia, że rozwiązanie istnieje. Brak dowodu wadliwej opony lub silnika. **Ocena B:** audyt odrzuca nieuprawnione założenia, nie dostarcza zatwierdzonej linii wyścigowej.

Baza Draft #104, zweryfikowany lokalnie i przez GitHub HEAD `10f22806140189086860ed104009cd3da03a75e5`. Gałąź `experiment/inverse-feasibility-matched-apex`, jeden Draft do `experiment/racing-line-root-cause`, bez merge. Zamrożone progressive-b, fizyka #100, parametry #104, mass147kg / rzeczywista inertia{proof[0]['Inertia']:.6f}kgm² z Box2D. Produkcyjne src/CoreSim, opona, wheel/engine/yaw equations, AI i gains niezmienione. Nowe dane inicjalizacji tylko opt-in/null domyślnie.

## Metoda i granice dowodu

W [InverseFeasibilityAudit.cs](../../tools/shared-world-speedway/InverseFeasibilityAudit.cs) solver dwóch niewiadomych β i ω wywołuje **oryginalne** `SlipCoupledDynamics.Forces`, które wywołuje `RearWheelDynamics.Evaluate`. Nie ma drugiej implementacji tyre law ani integratora ciała. Dla Q(s), v=const: `a=v²κ_path n_in`, `F_net=m a`; do wymaganej siły opony dochodzi oryginalny opór. Globalny wektor jest rzutowany na body forward/side. Rozwiązujemy lokalny bilans obu składowych; residual podano poniżej. Siatka reference≤0,05m, pochodne centralne. To regularna gałąź jazdy do przodu, |β|<1,5rad; solver lokalny nie certyfikuje wszystkich możliwych gałęzi ani pełnego układu.

Następnie `ω_dot=(v/|Q′|)dω/ds`, `T_drive=I_w ω_dot+r Fx+cω`; porównanie z **oryginalną** charakterystyką napędu próbkowaną przy rω, nie przy v. `r_body=vκ+(v/|Q′|)β′`, `τ_required=I_body dr_body/dt`. Sprawdzono fizyczny clamp B·0,5·TechniqueAuthority, zakres target slip wynikający z clamp0,95 oraz istniejący energy guard. Kolumny `required_target_slip` odwracają ten sam wzór yaw, bez wysyłania dodatkowego momentu do świata.

`Forces(dt=0)` służy wyłącznie lokalnemu ciągłemu prawu konstytutywnemu. Osobne wywołania **dt=10ms** przy legalnie obciętym Drive wyliczają rzeczywiste Fx/Fy, zmianę ω, komendę i wszystkie guardy. Obcięte wejście nie naprawia rozwiązania — zapis ujawnia rozbieżność. Lokalne stany nie są odtwarzane przez korektę XY/yaw/ω. Pełny dowód konstruktywny wymaga wspólnego rozwiązania kolejnych zaakceptowanych kroków z ciągłością wheel/body, następnie forward replay w Box2D. Tego dowodu nie uzyskano; jego brak nie jest zastąpiony modelem point-mass.

## 1. Warunki konieczne i pierwsze ograniczenia

|Krzywa / profil|max v²κ/a_grip|min/max Drive z inverse|max abs β °|max ω rad/s|max slip koła|max utilization|max residual N|
|---|---:|---:|---:|---:|---:|---:|---:|
{it}

Budżet przyczepności14,546245m/s² / B≈2138,298N. Konieczny scalar grip **PASS** w obu krzywych przy18 oraz15m/s. Nie jest to pełny PASS inverse.

Każdy matched przy18 ma również naruszony konieczny warunek napędu na regularnej gałęzi. WIDE-ENTRY na resecie wymaga nawet ujemnego Drive (hamowania), którego nie dostarczono. Pierwsze miejsca wszystkich naruszeń są osobnymi wierszami `FirstEngineFailure/FirstYawFailure/FirstGuardFailure/FirstSteeringFailure` w analysis.json. Nie ma niezależnego hard limitu ω ani slip ratio koła: zakres dostępności wynika z oryginalnego rear-contact, energii, legalnego Drive i momentu silnika przy rω. Ścisły zakres target slide jest odczytywany z **oryginalnego Forces przy nasyconym żądaniu**, a nie przybliżony ze skalarnego grip.

|Constraint|Dowód / pierwsze miejsce|Przekroczenie / wynik|
|---|---|---|
|Ciągłość yaw rate, dokładna OIO18|s=35m, start zacieśniania, κ i β są ciągłe|Δr={proof[0]['YawRateJump']:.6f}rad/s; wymagany impuls {proof[0]['RequiredAngularImpulse']:.6f}Nms, ograniczony moment≤{proof[0]['MaximumTorque']:.3f}Nm nie może zrealizować skoku w zerowym czasie|
|Ta sama przeszkoda OIO15|s=35m|Δr={proof[1]['YawRateJump']:.6f}; impuls{proof[1]['RequiredAngularImpulse']:.6f}Nms; niższa dodatnia prędkość nie usuwa skoku|
|Napęd OIO18, regularna gałąź|pierwsza próbka s={eng['s']:.6f}m|T_required={eng['required_drive_torque']:.3f}Nm > available{eng['available_drive_torque']:.3f}Nm, +{eng['required_drive_torque']-eng['available_drive_torque']:.3f}Nm; maksimum Drive{oio['MaximumDrive']:.3f}|
|Napęd inner18|pierwsza próbka s={ieng['s']:.6f}m|T_required={ieng['required_drive_torque']:.3f} > available{ieng['available_drive_torque']:.3f}Nm; maksimum Drive{inner['MaximumDrive']:.3f}|
|Yaw torque / target slide|pełne CSV i wykres poniżej|lokalne przekroczenia; pochodna na węźle jest mesh-dependent, dlatego skok oceniono jednostronnie, nie przez pojedynczy pik Nm|
|Energy guard|oryginalny dt10ms + replays|guard zawsze zachowany w wysyłanej komendzie; brak dowodu, że wymagany inverse torque jest osiągalny, patrz `FirstGuardFailure` w analysis.json|
|Niższy profil|17/16 nie przechodzą zamrożonego zapasu20% napędu, wybrano15m/s|maksimum Drive≈0,61 na screenie; pełna siatka i yaw osobno. Bez idealnego hamowania: profil startuje przy15, nie obiecuje zjazdu z18|

### Dlaczego skok jest rzeczywisty

Quintic d(s) ma d′=d″=0 na wejściu, ale `d‴(35+)=-60·6,5/(48,694687...)³`, po lewej0. Stąd skok κ′={proof[0]['CurvaturePrimeJump']:.9f}/m². Na prostej równowaga sił daje β=0; na regularnej gałęzi pochodna normalnej siły względem β z oryginalnej funkcji wynosi{proof[0]['ForceNormalBetaDerivative']:.3f}N/rad. Implicit differentiation daje `Δβ′=m v² Δκ′/(∂F_normal/∂β)` i `Δr=v Δβ′`. To lokalne ograniczenie samego ruchu i momentu, niezależne od wyboru preview.

[entry-proof.json](inverse-feasibility-matched-apex/entry-proof.json) łączy analityczną trzecią pochodną quintic z numerycznym Jacobianem **oryginalnego** prawa. [knot-probes.json](inverse-feasibility-matched-apex/knot-probes.json) sprawdza h0,2→0,005m: OIO18 wejście Δr0,162102 przyh0,1 →0,165159 przyh0,01 →0,165191 przyh0,005, zgodne z0,165190. Inner przy tym samym wejściu dąży do0. Na apexie OIO18 Δr≈−0,518rad/s. β jest ciągła, ale jej pochodna nie; układ o ograniczonym momencie wymaga ciągłego r_body. Nie zaobserwowano skoku headingu, XY ani κ.

**Zakres tego FAIL jest ścisły:** dokładna ciągła Q(t), stałe18/15 i regularna gałąź rozwiązania sił. Nie jest to dowód niemożliwości przejazdu z błędem≤0,5/1m. Już na wejściu minimalny czas wygładzenia przy maksymalnym momencie to tylko{1000*proof[0]['MinimumTransitionSeconds']:.2f}ms; nie przypisujemy temu automatycznie błędu metrowego. Native i tak stosuje siły odcinkami. Z kolei duży niedobór napędu w środku łuku jest niezależnym argumentem przeciw stałym18 na tym lokalnym rozwiązaniu. Dla inner15 oraz praktycznie wygładzonej OIO15 pełna konstruktywna wykonalność pozostaje **INCONCLUSIVE**.

![Wymagane Fx/Fy i yaw versus dostępne ograniczenia](inverse-feasibility-matched-apex/required-and-available.png)

Na wykresie Fx ceiling oznacza wynik bilansu momentu koła przy wymaganym ω_dot; górna granica Fy z koła przyczepności jest tylko konieczna, bez udawania legalnego rozwiązania tylnego kontaktu. Actual dt10ms i target slip/yaw/energy guard są w tych samych CSV. Piki wymaganej τ na węzłach nie są zbieżnym skończonym momentem — granicą jest impuls wykazany powyżej.

## 2. Matched-apex: niezależna geometria

Wszystkie pięć krzywych zamrożono przed porównaniami: trzy pozycje1/5/8,5m, dwa początki zacieśniania **na wejściu** albo **15m przed wejściem** (inner jest identyczny, zapisany raz). Minimum1m osiągają przy wspólnym anchorze s={apex:.9f}m, identycznym XY/stycznej/κ. Od tego miejsca do **15m za wyjściem** krzywe są identycznym d=1. Jest to wspólny apex-anchor z plateau, nie twierdzenie o unikalnym minimum offsetu. Drugi łuk jest lustrzany. Na pozostałych32m prostej C2 reset do pozycji wejścia; następne15m przed łukiem trzyma ją albo rozpoczyna early tightening. Koszt resetu wliczony w pełne okrążenie, wyłączony ze wspólnego okna20m→{exit+15:.6f}m. Szeroka pozycja na prostej nie oznacza szerokiego wyjścia.

Pełny footprint mieści się w okręgu1,05m; konserwatywne certyfikaty dowolnego yaw: banda minimum1,415m, kerb minimum0,915m. Między próbkami odjęto1-Lipschitz bound ruchu Q. Niezależne testy XY/κ/C2 oraz pełnych kapsuł sprawdzają zgodność. **Geometria PASS**, osobno od dynamicznej regularności yaw.

|Plan|L lap m|L okno m|max κ lap|max κ okno|min R lap m|max ay18|L/18 lap/okno s|
|---|---:|---:|---:|---:|---:|---:|---:|
{gt}

**WIDE nie poprawia najgorszej krzywizny** tej zamrożonej rodziny. Ma lokalnie mniejszą κ we wcześniejszych fragmentach, lecz wspólny późny fragment inner zachowuje jego najgorszy promień, a dodatkowe przejścia mogą go pogorszyć. Początek15m wcześniej zmniejsza koszt względem wejścia na początku łuku, ale nie daje przewagi maxκ nad inner. To wynik dla tych jawnych krzywych i progressive-b, nie uniwersalny zakaz innych trajektorii.

Kolumna L/18 to **warunkowy czas kinematyczny**, bez silnika, a nie wykonalny czas przy tych samych ograniczeniach fizycznych. Żądany idealizowany czas pod **pełnymi** constraints pozostaje INCONCLUSIVE; podanie liczby po samym grip z idealnym hamowaniem byłoby niedozwolonym zastąpieniem testu. Nie uruchomiono ponownie point-mass #104 ani wheelspin ON/OFF.

![Planowane trzy pozycje i dwa początki zacieśniania](inverse-feasibility-matched-apex/matched-geometry.png)

## 3. Ograniczone forward replay i sterownik

Analiza i [replay-freeze](inverse-feasibility-matched-apex/replay-freeze.json) poprzedzają wszystkie siedem nowych światów. Użyto istniejącego path-preview #104, bez zmiany gains, oryginalnych sił, guardów,10ms/4substeps. Różne początkowe XY są wymagane. Przy reference20m, v18, body yaw wzdłuż prostej, yaw rate0, wheelω={load(DATA/'replay-freeze.json')['Initial18']['RearWheelOmega']:.6f}rad/s i stałe Drive={load(DATA/'replay-freeze.json')['Initial18']['Drive']:.8f} dają równoważną **ustaloną równowagę na prostej**, sprawdzoną także rzeczywistym krokiem. Nie użyto wspólnego XY dla różnych offsetów. W łuku ta sama legalna przepustnica pozwala modelowi fizycznie zwalniać; nie utrzymuje18 sztuczną siłą. Dwa niższe kontrole #104 startują15m/s ze swoim oryginalnym straight maintenance Drive. Żaden zaakceptowany stan nie jest przestawiany. To próba konstruktywna z legalnym wejściem, która **nie realizuje zadanej Q(t) o stałej prędkości**; nie jest dowodem jej istnienia.

|Próba|RMS/max boczny m|RMS kierunku v °|entry/min/exit m/s|offset przy planned apex m|lag połowy s|tracking|safety|
|---|---:|---:|---:|---:|---:|---|---|
{ct}

`phases.csv` podaje osobno oba łuki Lap2/3, rzeczywiste minimum offsetu i jego położenie, błąd XY w planowanym apexie oraz entry/min/exit. Minimum offsetu na wspólnym plateau nie musi być unikalnym apexem; nie podmieniamy nim shared XY anchor. Lateral/longitudinal/velocity-heading/body-heading i actual κ to te same niezależne obserwacje co #104, przy wspólnej zaakceptowanej reference. Różnica headingu ciała i kierunku prędkości jest slide.

Lag to czas przejścia rzeczywistego offsetu przez połowę zacieśniania minus czas zaplanowanego punktu połowy, interpolowany z10ms danych. Ujemny oznacza wyprzedzenie, inner nie ma takiego zdarzenia. `*-preview.csv` zapisuje pełne feed-forward κ oraz osobno jego a, pozycję, damping i rzeczywistą odpowiedź. Odtworzono je offline z zaakceptowanych wektorów, wywołując istniejące `Guidance`; zgodność sumy z pierwotnym żądaniem Forces≤0,0001m/s² jest sprawdzona w `preview-telemetry-verification.json`.

**Wszystkie siedem przejazdów zalicza progi trackingu #104 przy rzeczywiście osiągniętych niższych prędkościach:** matched około13,0–14,45m/s, para startująca15 około11,1–12,37m/s na lotnych próbkach. OIO ma w tej niższej próbie RMS0,100m / max0,360m i rzeczywiste minimum przy50,1% łuku. Jest to konstruktywny dowód legalnej **przybliżonej geometrii przy tych rzeczywistych prędkościach**, nie dowód narzuconego profilu15 ani18. Nie uzasadnia nowego tuningu gains: warunkowy test „legalna stała Q(t) + błąd kontrolera” nadal nie został spełniony.

|Próba|mean Fx/Fy N|mean abs slide °|max wheel slip|max utilization|yaw physical/energy clamp %|max applied yaw Nm|
|---|---:|---:|---:|---:|---:|---:|
{pt}

![Plan i wykonanie, prędkość, tracking i rzeczywista droga](inverse-feasibility-matched-apex/native-tracking.png)
![Niższa próba: żądanie, rzeczywista odpowiedź, kierunek i yaw](inverse-feasibility-matched-apex/lower-speed-response.png)

### Czas i bramka interpretacji

|Próba|Lap2+3 s|actual droga m|okno mean s/m|różnica Lap2/3|koszt drogi s|efekt prędkości s|czas strategii|
|---|---:|---:|---:|---:|---:|---:|---|
{tt}

To liczby **diagnostyczne**. Nie przeszła bramka inverse dla stałego profilu, a Lap2/3 nadal różnią się o podany procent; nie wykazano stanu okresowego. Ekran stabilności≤0,5% jest pomocniczą konwencją raportu **ustaloną po pomiarach**, nie zamrożonym kryterium akceptacji. Wynik inverse pozostaje nierozstrzygnięty niezależnie od tego ekranu. Nie przedłużano biegu ani nie wybierano liczby okrążeń pod korzystny wynik. W pełnym `analysis.json` tracking może PASS mimo INCONCLUSIVE czasu i inverse. Wynik lepszego trackingu po zwolnieniu nie dowodzi możliwości stałych15 na dokładnej Q(t).

Rozkład ΔT jest tożsamością z #104: `(L−L_inner)/vbar_inner + L(1/vbar−1/vbar_inner)`. Porównuje pierwsze pięć prób przy tej samej przepustnicy i starcie18. Dwie kontrole15 mają inne warunki; ich rozkład względem inner18 jest wyłącznie rachunkiem, bez wniosku o strategii. Nie wykazano, że ewentualna korzyść prędkości szerokiego ustawienia pokrywa dodatkową drogę. **Winner=None**.

## 4. Co rozstrzygnięto i co pozostaje

|Pytanie / bramka|Wynik|
|---|---|
|Geometria C2 i footprint|PASS dla wszystkich matched; nie oznacza dynamicznej regularności|
|Dokładna regularna OIO18|FAIL: najpierw skok wymaganego yaw rate przy s35, dalej niedostępny napęd|
|Dokładna regularna OIO15|FAIL skoku yaw; niższa prędkość usuwa niedobór napędu na screenie, nie całą przeszkodę|
|Pełna native inverse-feasibility w tolerancjach #104|INCONCLUSIVE: nie znaleziono wspólnego legalnego rozwiązania stanów/wejść dla Q(t); nie wykonano certyfikowanego globalnego shooting/optimal-control solve|
|Tracking|PASS/FAIL oddzielnie w tabeli, tylko rzeczywista wykonalna prędkość replay; nie walidacja stałych18/15|
|Czas strategii / prędkość kontra droga|INCONCLUSIVE, brak zaliczonej bramki inverse i stabilnego porównania|
|Czy WIDE ma korzystniejszą geometrię przy tym samym apexie?|Lokalnie mniejsze κ, ale maxκ nie lepsze i droga dłuższa w obu zamrożonych wariantach|
|Regulator czy fizyka?|#104 potwierdził błąd odniesienia regulatora. Tutaj potwierdzono niezależną niespójność dynamiczną dokładnej C2 OIO i niedobór napędu18 na regularnej gałęzi. Nie rozdzielono jeszcze przyczyn całego błędu tolerancji po preview|
|Czy opona / prędkość są wadliwe?|Brak takiego dowodu. Stałe18 nie są neutralnym fizycznie założeniem; bilanse, kontakty i ograniczenia przechodzą. Niedostępna żądana trajektoria nie dowodzi wadliwości modelu|

**Jeden następny PR:** opt-in generator **dynamicznie regularnej pełnej Q(s) z ciągłym κ′ (co najmniej C3 XY)**, w tych samych anchorach i na niezmienionym torze progressive-b, z bramką oryginalnego inverse yaw/engine. Wygładzać Q, także przy węzłach reference; samo zastąpienie offset quintic bez sprawdzenia pełnego XY nie wystarcza. Usuwa potwierdzony pierwszy skok wymaganego yaw rate, bez zmiany opony/yaw/gains. Nie obiecywać18 ani optymalizacji czasu. Acceptance: jednostronne r_body zgodne w tolerancji numerycznej na wszystkich węzłach; pełny10ms coupled solve z ciągłością ω/body i Drive0..1 oraz istniejącymi clamp/guardami; następnie forward bez korekt, z progami #104 i powtórzeniem bez obserwatorów. Jeżeli nie znajdzie się rozwiązania, pozostawić INCONCLUSIVE; dopiero **legalne rozwiązanie + błąd regulatora** uprawnia do wskazania kolejnego składnika sterowania. W tym PR nie ma podstaw do arbitralnej zmiany gains czy opony.

## Walidacja, bezpieczeństwo i reprodukcja

Każdy nowy replay przechodzi pełne kapsuły przy rzeczywistym yaw i konserwatywny between-step bound z #103; szczegóły min kerb/banda są w siedmiu JSON. Oryginalny body+wheel energy ledger ma residual<1J, yaw guard excess0, utilization≤1. Siły i momenty pochodzą wyłącznie z istniejącej ścieżki, Box2D jest jedynym integratorem. Zachowano wszystkie **{sum(a['HistoricalHashes'].values())} historyczne SHA-256** oraz6 fingerprintów #103,1 kontrolę #104 i nową observed/plain parity.

|Próba|kerb min/bound m|banda min/bound m|energy residual J|yaw guard excess W|
|---|---:|---:|---:|---:|
{safety}

Wersja robocza definicji resetu kończyła powrót dopiero przy następnym wejściu; preflight inicjalizacji wykrył, że reference20 nie jest wtedy ustaloną prostą. Poprawiono wspólny reset na32m, kończący się15m przed wejściem, **przed jakimkolwiek porównaniem matched replays**. Nie wybrano krzywej według czasu; zapisy pierwotnego preflight i ich hash są w `protocol-correction.json`. #104 i regularne inverse inner/OIO nie zmieniły się. Wszystkie końcowe matched geometry/inverse zamrożono ponownie przed siedmioma replay.

**71/71 celowanych C#,96/96 Python calibration (4 nowe),7/7 CI policy i12/12 Node; restore i Release --warnaserror poprawne.** Końcowy Windows CI podano w PR, wyniki lokalnego TRX w `targeted-tests.json`. Manifest obejmuje wszystkie nowe dowody. Surowe exporty odmawiają nadpisania. Korekta dwóch kolumn analitycznych target slide jest rejestrowana w `inverse-bound-correction.json`; wszystkie pozostałe kolumny sił/stanów oraz wszystkie native replays zachowują identyczne wartości. Python analizuje wyłącznie dane, nie implementuje fizyki. Istniejący replay viewer prezentuje #105 opt-in cases na progressive-b i planowane linie; bez nowej aplikacji.

```powershell
dotnet restore
dotnet build -c Release --no-restore --warnaserror
# Nowy pusty katalog, z root repo; każda faza musi się udać przed następną.
dotnet tools/shared-world-speedway/bin/Release/net8.0/shared-world-speedway.dll inverse-feasibility ../inverse-reproduction freeze
# Następnie: inverse, lower, geometry, replay-freeze, replay, verify, entry-proof, preview-telemetry
python tools/shared-world-speedway/inverse-feasibility-report.py --plots
python -m unittest discover -s tests/calibration -p test_inverse_feasibility.py
```

Raport czyta zapisany katalog docs/experiments/inverse-feasibility-matched-apex. --plots wymaga matplotlib, reszta standardowej biblioteki. Nie uruchamiać ponownie starych generatorów #99–104 do historycznych folderów.
'''
    (ROOT/'docs/experiments/inverse-feasibility-matched-apex.md').write_text(data,encoding='utf-8',newline='\n')

if __name__=='__main__':
    a,paths,inv,raw=analyse()
    if '--plots' in sys.argv:plots(a,paths,inv,raw)
    report(a)
    save('manifest.json',{p.name:hashlib.sha256(p.read_bytes()).hexdigest() for p in sorted(DATA.iterdir()) if p.is_file() and p.name!='manifest.json'})
    print('Wrote #105 analysis/report; historical files:',sum(a['HistoricalHashes'].values()))
