"""Independent offline #104 analysis. Reads native captures; never runs or corrects a body.
Only --plots needs matplotlib. The point-mass estimate is an optimistic friction-circle
model with ideal braking and a frozen drive envelope, not a second race simulator.
"""
import bisect
import csv
import hashlib
import json
import math
from pathlib import Path
import statistics
import sys

ROOT = Path(__file__).resolve().parents[2]
DATA = ROOT / 'docs/experiments/racing-line-root-cause'
NAMES = ['inner', 'outside-inside', 'outside-inside-outside', 'late-apex']
load = lambda p: json.loads(p.read_text(encoding='utf-8'))

def read_csv(p):
    with p.open(encoding='utf-8') as stream:
        return [{k: float(v) for k, v in r.items()} for r in csv.DictReader(stream)]

def save_json(name, value):
    (DATA / name).write_text(json.dumps(value, indent=2, allow_nan=False)+'\n', encoding='utf-8', newline='\n')

def speed_profile(points, lap, ay, drive_acceleration, resistance_acceleration):
    """Periodic forward/backward speed feasibility; ideal tyre-circle braking is explicit.
    Engine/wheel losses, yaw transients and a chosen rider's braking ability are absent.
    The 100m/s initialization is a numerical upper bound, never an achieved speed.
    """
    n = len(points); ds = lap/n
    dl = [p['metric']*ds for p in points]
    k = [abs(p['curvature']) for p in points]
    caps = [min(100, math.sqrt(ay/max(c, 1e-12))) for c in k]; v = caps[:]
    def remaining(speed, curvature):
        return math.sqrt(max(0, ay*ay-(speed*speed*curvature)**2))
    def acceleration(i):
        return min(remaining(v[i], k[i]), drive_acceleration(v[i], points[i]))-resistance_acceleration(v[i], points[i])
    for iteration in range(100):
        old = v[:]
        for i in range(n):
            j = (i+1) % n
            v[j] = min(v[j], math.sqrt(max(0, v[i]*v[i]+2*acceleration(i)*dl[i])))
        for j in reversed(range(n)):
            i = (j-1) % n
            decel = remaining(v[j], k[j])+resistance_acceleration(v[j], points[j])
            v[i] = min(v[i], math.sqrt(max(0, v[j]*v[j]+2*decel*dl[i])))
        change = max(abs(a-b) for a,b in zip(old,v))
        if change < 1e-8:
            break
    else:
        raise ValueError('Periodic speed profile did not converge')
    time = sum(2*dl[i]/(v[i]+v[(i+1) % n]) for i in range(n))
    violation = max(v[(i+1) % n]**2-v[i]**2-2*acceleration(i)*dl[i] for i in range(n))
    brake_violation=max(v[i]**2-v[(i+1)%n]**2-2*(remaining(v[(i+1)%n],k[(i+1)%n])+resistance_acceleration(v[(i+1)%n],points[(i+1)%n]))*dl[i] for i in range(n))
    assert violation < 1e-5 and brake_violation < 1e-5
    assert all(speed<=cap+1e-8 for speed,cap in zip(v,caps))
    return {'LapSeconds':time, 'TwoFlyingSeconds':2*time, 'LengthMeters':sum(dl), 'MinimumSpeed':min(v),
            'MaximumSpeed':max(v), 'Iterations':iteration+1, 'MaximumForwardConstraintResidual':max(0,violation),'MaximumBrakeConstraintResidual':max(0,brake_violation)}, v

def series_stats(rows, start, end):
    a = [(r, max(0, min(end, r['t']+r['dt'])-max(start,r['t']))) for r in rows]
    a = [(r,w) for r,w in a if w>0]; duration = sum(w for r,w in a)
    def mean(fn): return sum(fn(r)*w for r,w in a)/duration
    def maximum(fn): return max(fn(r) for r,w in a)
    return {'RmsLateral':math.sqrt(mean(lambda r:r['lateral_error']**2)), 'MaximumLateral':maximum(lambda r:abs(r['lateral_error'])),
            'RmsLongitudinal':math.sqrt(mean(lambda r:r['longitudinal_error']**2)), 'MaximumLongitudinal':maximum(lambda r:abs(r['longitudinal_error'])),
            'RmsVelocityHeadingDegrees':math.degrees(math.sqrt(mean(lambda r:r['velocity_heading_error']**2))),
            'MaximumVelocityHeadingDegrees':math.degrees(maximum(lambda r:abs(r['velocity_heading_error']))),
            'RmsBodyHeadingDegrees':math.degrees(math.sqrt(mean(lambda r:r['body_heading_error']**2))),
            'RmsCurvatureError':math.sqrt(mean(lambda r:(r['actual_curvature']-r['path_curvature'])**2)),
            'MaximumWheelspin':maximum(lambda r:r['wheelspin']), 'MeanFx':mean(lambda r:r['fx']), 'MeanFy':mean(lambda r:r['fy']),
            'NearGripPercent':100*mean(lambda r:r['utilization']>=.95),
            'RequiredAboveBudgetPercent':100*mean(lambda r:abs(r['required_path_accel'])>r['budget']/147),
            'SteeringClampedPercent':100*mean(lambda r:abs(r['steering_demand'])>.95*r['budget']/147),
            'YawForceClampedPercent':100*mean(lambda r:abs(r['yaw_requested'])>r['budget']*.5+1e-5),
            'YawEnergyClampedPercent':100*mean(lambda r:abs(r['yaw_authority']-r['yaw_energy'])>1e-5)}

def time_and_distance_functions(rows, run):
    final = run['Result']['Riders'][0]
    points = [(r['t'],r['s'],r['x'],r['y']) for r in rows]+[(run['Result']['TimeSeconds'],final['TotalReferenceMeters'],final['Position']['X'],final['Position']['Y'])]
    times = [p[0] for p in points]; references = [p[1] for p in points]; distance = [0]
    for a,b in zip(points,points[1:]): distance.append(distance[-1]+math.hypot(b[2]-a[2],b[3]-a[3]))
    def interpolate(x, axis, values):
        x=max(axis[0],min(axis[-1],x))
        i = max(0, min(len(axis)-2, bisect.bisect_right(axis,x)-1)); alpha = (x-axis[i])/(axis[i+1]-axis[i])
        return values[i]+alpha*(values[i+1]-values[i])
    return lambda s:interpolate(s,references,times), lambda t:interpolate(t,times,distance)

def analyse():
    freeze = load(DATA/'freeze.json'); lap = load(DATA/'geometry-summary.json')[0]['ReferenceLapMeters']; bend = (lap-124)/2
    envelope = load(DATA/'frozen-drive-envelope.json'); mass = freeze['MassKg']; budget = freeze['AccelerationBudget']
    assert mass == 147
    tables = [[r for r in envelope if r['Turn']==b] for b in [False,True]]
    def interp(v,key,row):
        s = row['s'] % lap; turn = int(35<=s<=35+bend or 35+lap/2<=s<=35+lap/2+bend)
        z = max(0,min(1000,v/.05)); i=min(999,int(z)); return tables[turn][i][key]*(1-(z-i))+tables[turn][i+1][key]*(z-i)
    geometries={}; estimates=[]
    for name in NAMES:
        g=read_csv(DATA/(name+'-geometry.csv'))[:-1]; geometries[name]=g
        summary, speed = speed_profile(g,lap,budget,lambda v,r:interp(v,'AvailableDrive',r)/mass,lambda v,r:interp(v,'Resistance',r)/mass)
        coarse,_=speed_profile(g[::2],lap,budget,lambda v,r:interp(v,'AvailableDrive',r)/mass,lambda v,r:interp(v,'Resistance',r)/mass)
        summary.update(Name=name,CoarseLapDifferenceSeconds=coarse['LapSeconds']-summary['LapSeconds'],Constant18LapSeconds=summary['LengthMeters']/18)
        estimates.append(summary)
        with (DATA/(name+'-point-mass.csv')).open('w',newline='',encoding='utf-8') as stream:
            writer=csv.writer(stream);writer.writerow(['reference_m','path_speed_mps','traction_cap_mps','required_ay_mps2'])
            writer.writerows((r['s'],v,r['speed_cap'],v*v*abs(r['curvature'])) for r,v in zip(g,speed))
    save_json('point-mass-summary.json',estimates)
    runs=load(DATA/'runs.json')+load(DATA/'preview-runs.json'); cases=[]; phases=[]; telemetry={}
    for run in runs:
        prefix=run['Case']; rows=read_csv(DATA/(prefix+'-tracking.csv'));telemetry[prefix]=rows
        time_at,distance_at=time_and_distance_functions(rows,run)
        start,end=run['CrossingSeconds'][0],run['CrossingSeconds'][2]
        summary={'Case':prefix,'Path':run['AuthoredPath'],'FlyingSeconds':end-start,'FlyingDistance':distance_at(end)-distance_at(start),
                 'BoundarySafe':run['BoundarySafe'],'MinimumInner':run['MinimumInner'],'MinimumOuter':run['MinimumOuter'],
                 'ConservativeInner':run['ConservativeInner'],'ConservativeOuter':run['ConservativeOuter']}
        if run['AuthoredPath']:
            summary.update(series_stats(rows,start,end))
            summary['TrackingPass']=summary['RmsLateral']<=.5 and summary['MaximumLateral']<=1 and summary['RmsVelocityHeadingDegrees']<=3 and summary['MaximumVelocityHeadingDegrees']<=10
        else:
            summary['TrackingPass']=False
            force_stats=series_stats(rows,start,end)
            for key in ['MaximumWheelspin','MeanFx','MeanFy','NearGripPercent','SteeringClampedPercent','YawForceClampedPercent','YawEnergyClampedPercent']:summary[key]=force_stats[key]
            summary['RequiredAboveBudgetPercent']=None
        summary['MeanTravelSpeed']=summary['FlyingDistance']/summary['FlyingSeconds'];cases.append(summary)
        for lap_index in [1,2]:
            for corner in [0,1]:
                entry=lap_index*lap+35+corner*lap/2;exit=entry+bend
                begin_time,finish_time=time_at(entry-31),time_at(exit+31)
                turn_rows=[r for r in rows if entry<=r['s']<=exit]
                actual_apex=min(turn_rows,key=lambda r:r['offset'])
                sample=lambda fraction:min(turn_rows,key=lambda r:abs(r['s']-(entry+bend*fraction)))
                plan_apex=.65 if run['AuthoredPath']=='late-apex' else .5
                half=next((r for r in turn_rows if r['offset']<=5.25),None) if run['AuthoredPath'] not in [None,'inner'] else None
                phases.append({'Case':prefix,'Lap':lap_index+1,'Corner':corner+1,'Seconds':finish_time-begin_time,
                    'CompleteWindow':exit+31<=run['Result']['Riders'][0]['TotalReferenceMeters']+1e-5,
                    'Distance':distance_at(finish_time)-distance_at(begin_time),'EntrySpeed':sample(0)['speed'],'MiddleSpeed':sample(.5)['speed'],'ExitSpeed':sample(1)['speed'],
                    'Offset35':sample(.35)['offset'],'Offset50':sample(.5)['offset'],'Offset80':sample(.8)['offset'],
                    'PlannedApexFraction':plan_apex if run['AuthoredPath'] not in [None,'inner'] else None,
                    'ActualMinimumOffset':actual_apex['offset'],'ActualApexFraction':(actual_apex['s']-entry)/bend,
                    'HalfTransitionLagSeconds':half['t']-time_at(entry+bend*plan_apex/2) if half else None,
                    'HalfTransitionLagReferenceMeters':half['s']-(entry+bend*plan_apex/2) if half else None})
    inner=next(c for c in cases if c['Case']=='inner-existing')
    for c in cases:
        c['DistanceCostAtInnerMeanSpeed']=(c['FlyingDistance']-inner['FlyingDistance'])/inner['MeanTravelSpeed']
        c['SpeedEffectSeconds']=c['FlyingDistance']*(1/c['MeanTravelSpeed']-1/inner['MeanTravelSpeed'])
        assert abs(c['FlyingSeconds']-inner['FlyingSeconds']-c['DistanceCostAtInnerMeanSpeed']-c['SpeedEffectSeconds'])<1e-9
    save_json('analysis.json',{'Cases':cases,'Phases':phases,'WheelComparisonExecuted':False,'Reason':'Existing continuous-path tracking fails frozen gate; path-preview also fails. No tyre/engine ablation authorized by gate.'})
    with (DATA/'phases.csv').open('w',newline='',encoding='utf-8') as stream:
        writer=csv.DictWriter(stream,fieldnames=list(phases[0]));writer.writeheader();writer.writerows(phases)
    return freeze,lap,bend,geometries,estimates,cases,phases,telemetry,runs

def make_plots(lap,bend,geometries,estimates,cases,phases,telemetry,runs):
    import matplotlib
    matplotlib.use('Agg')
    import matplotlib.pyplot as plt
    plt.rcParams.update({'svg.hashsalt':'racing-line-root-cause-104','font.family':'DejaVu Sans'})
    colors=['#4da37a','#d78d38','#4289c5','#a369b3']
    def save(fig,name):
        fig.tight_layout();fig.savefig(DATA/(name+'.png'),dpi=160);fig.savefig(DATA/(name+'.svg'),metadata={'Date':None});plt.close(fig)
        p=DATA/(name+'.svg');p.write_text('\n'.join(r.rstrip() for r in p.read_text(encoding='utf-8').splitlines())+'\n',encoding='utf-8',newline='\n')
    fig,axes=plt.subplots(2,2,figsize=(14,9))
    own=load(ROOT/'docs/experiments/progressive-bend-width/progressive-b-geometry.json')
    for segment in own['segments']:
        for edge in ['inner','outer']:axes[0,0].plot([s[edge]['x'] for s in segment['sections']],[s[edge]['y'] for s in segment['sections']],color='#777777',lw=.7)
    for color,name in zip(colors,NAMES):
        g=geometries[name];q=[r for r in g if 4<=r['s']<=35+bend+31];x=[(r['s']-35)/bend*100 for r in q]
        axes[0,0].plot([r['x'] for r in g],[r['y'] for r in g],color=color,label=name,lw=1)
        axes[0,1].plot(x,[r['curvature'] for r in q],color=color,label=name)
        model=read_csv(DATA/(name+'-point-mass.csv'));q2=[r for r in model if 4<=r['reference_m']<=35+bend+31]
        axes[1,0].plot([(r['reference_m']-35)/bend*100 for r in q2],[r['path_speed_mps'] for r in q2],color=color)
        axes[1,1].plot(x,[r['ay_at18'] for r in q],color=color)
    axes[0,0].set(aspect='equal',xlabel='X [m]',ylabel='Y [m]',title='Four frozen C2 paths; physical kerb and outer boundary')
    axes[0,0].legend(fontsize=8);axes[0,1].set(ylabel='Signed path curvature [1/m]',title='Full XY curvature, not reference curvature')
    axes[1,0].set(ylabel='Point-mass speed [m/s]',title='Optimistic drive/grip estimate; ideal braking')
    axes[1,1].axhline(14.546245,color='black',ls=':',label='Frozen grip acceleration');axes[1,1].legend(fontsize=8)
    axes[1,1].set(ylabel='Required normal acceleration [m/s²]',title='Constant 18 m/s; late apex exceeds budget locally')
    for ax in [axes[0,1],axes[1,0],axes[1,1]]:ax.set_xlabel('Bend reference progress [%]; entry 0 / exit 100')
    for ax in axes.flat:ax.grid(alpha=.2)
    save(fig,'geometry-and-speed')
    fig,axes=plt.subplots(2,2,figsize=(14,9))
    for ax,name in zip(axes.flat,NAMES):
        for edge in ['inner','outer']:
            sections=[s for segment in own['segments'] for s in segment['sections'] if 4<=s['referenceMeters']<=35+bend+31]
            ax.plot([s[edge]['x'] for s in sections],[s[edge]['y'] for s in sections],color='#999999',lw=.6,label='Physical '+edge)
        g=[r for r in geometries[name] if 4<=r['s']<=35+bend+31];ax.plot([r['x'] for r in g],[r['y'] for r in g],color='black',ls='--',lw=1.4,label='Intended XY')
        for suffix,color in [('existing','#d77d2d'),('path-frame','#999999'),('path-preview','#377fb9')]:
            key=name+'-'+suffix
            if key not in telemetry:continue
            a=[r for r in telemetry[key] if lap+4<=r['s']<=lap+35+bend+31]
            ax.plot([r['x'] for r in a],[r['y'] for r in a],color=color,lw=1,label=suffix)
        ax.set(aspect='equal',title=name,xlabel='X [m]',ylabel='Y [m]',xlim=(-2,84),ylim=(-15,80));ax.grid(alpha=.2);ax.legend(fontsize=7)
    save(fig,'intended-and-executed-xy')
    fig,axes=plt.subplots(3,2,figsize=(14,12));name='outside-inside-outside'
    g=[r for r in geometries[name] if 4<=r['s']<=35+bend+31];gx=[(r['s']-35)/bend*100 for r in g]
    axes[0,0].plot(gx,[r['offset'] for r in g],'k--',label='Intended');axes[0,1].plot(gx,[r['curvature'] for r in g],'k--',label='Intended')
    for suffix,color in [('existing','#d77d2d'),('path-frame','#999999'),('path-preview','#377fb9')]:
        a=[r for r in telemetry[name+'-'+suffix] if lap+4<=r['s']<=lap+35+bend+31];x=[(r['s']-lap-35)/bend*100 for r in a]
        axes[0,0].plot(x,[r['offset'] for r in a],color=color,label=suffix)
        axes[0,1].plot(x,[r['actual_curvature'] for r in a],color=color,label=suffix)
        axes[1,0].plot(x,[r['speed'] for r in a],color=color,label=suffix)
        axes[1,1].plot(x,[r['lateral_error'] for r in a],color=color,label=suffix+' lateral')
        if suffix!='path-frame':
            axes[1,1].plot(x,[r['longitudinal_error'] for r in a],color=color,ls=':',label=suffix+' longitudinal')
            axes[2,0].plot(x,[math.degrees(r['velocity_heading_error']) for r in a],color=color,label=suffix+' velocity')
            axes[2,0].plot(x,[math.degrees(r['body_heading_error']) for r in a],color=color,ls=':',label=suffix+' body')
        axes[2,1].plot(x,[r['steering_demand'] for r in a],color=color,label=suffix+' request')
    a=[r for r in telemetry[name+'-existing'] if lap+4<=r['s']<=lap+35+bend+31]
    axes[2,1].plot([(r['s']-lap-35)/bend*100 for r in a],[r['required_path_accel'] for r in a],'k--',label='Required path at actual existing speed');axes[2,1].axhline(14.546245,color='r',ls=':',lw=.8,label='Grip budget')
    for ax,ylabel in zip(axes.flat,['Offset [m]','Motion curvature [1/m]','Speed [m/s]','Path-frame error [m]','Heading minus path tangent [°]','Requested normal acceleration [m/s²]']):
        ax.set(xlabel='Bend reference progress [%], Lap2 / first bend',ylabel=ylabel);ax.grid(alpha=.2);ax.legend(fontsize=7)
    save(fig,'tracking-mechanism')
    fig,axes=plt.subplots(3,1,figsize=(13,9),sharex=True)
    a=[r for r in telemetry['legacy-wide-tighten'] if lap+4<=r['s']<=lap+35+bend+31];x=[(r['s']-lap-35)/bend*100 for r in a]
    axes[0].plot(x,[r['offset'] for r in a],label='Actual offset');axes[0].step(x,[r['target'] for r in a],where='post',label='Step target (not a geometric path)');axes[0].set_ylabel('Offset [m]')
    for key,label in [('old_geometry_demand','Reference preview'),('old_offset_demand','Offset feedback'),('old_damping_demand','Reference-frame damping'),('steering_demand','Total request')]:axes[1].plot(x,[r[key] for r in a],label=label)
    axes[1].set_ylabel('Acceleration demand [m/s²]')
    for key,label in [('yaw_requested','Yaw request'),('yaw_authority','After technique authority'),('yaw_energy','After energy guard')]:axes[2].plot(x,[r[key] for r in a],label=label)
    axes[2].set(ylabel='Yaw torque [Nm]',xlabel='First bend reference progress [%], Lap2')
    for ax in axes:ax.legend(fontsize=8);ax.grid(alpha=.2)
    save(fig,'legacy-wide-mechanism')

def table(headers,rows):
    return '\n'.join(['| '+' | '.join(headers)+' |','| '+' | '.join(['---']*len(headers))+' |']+['| '+' | '.join(map(str,r))+' |' for r in rows])+'\n'

def fmt(v,n=3):return '—' if v is None else f'{v:.{n}f}'

def write_report(freeze,lap,bend,geometries,estimates,cases,phases,telemetry,runs):
    proof=load(DATA/'targeted-tests.json');assert proof['Counters']['passed']=='47' and proof['Counters']['failed']=='0'
    geometry=load(DATA/'geometry-summary.json'); case={c['Case']:c for c in cases}
    history={}
    for name in ['autonomous-racing-line','slip-coupled-dynamics','slip-control-validation','physics-driven-racing-line','rear-wheel-dynamics','track-geometry-variants','local-offset-limit','progressive-bend-width']:
        folder=ROOT/'docs/experiments'/name; manifest=load(folder/'manifest.json')
        for file,expected in manifest.items():assert hashlib.sha256((folder/file).read_bytes()).hexdigest().lower()==expected.lower(),(name,file)
        history[name]=len(manifest)
    checks=load(DATA/'verification.json');assert all(c['Expected']==c['Actual'] for c in checks)
    for r in runs:
        energy=r['Energy'];assert abs(energy['BalanceResidualJoules'])<max(5,.001*energy['EngineWorkJoules'])
        assert energy['MaximumYawGuardExcessWatts']<.01 and r['MaximumUtilization']<=1.00001
    root=case['outside-inside-outside-existing']; preview=case['outside-inside-outside-path-preview']
    assert not any(c['TrackingPass'] for c in cases)
    def phase_mean(prefix,key):
        a=[p[key] for p in phases if p['Case']==prefix and p['CompleteWindow'] and p[key] is not None]
        return statistics.mean(a) if a else None
    lines=['''# Racing Line Root-Cause Audit (#104)

**Pierwsze potwierdzone ograniczenie sterowania: kontrakt target-offset i preview opisują reference P(s), zamiast pełnej zadanej Q(s).** Skok „apex2m przy35%” nie definiuje wykonanego apexu.

**Potwierdzono dwa niezależne ograniczenia:** (1) zamrożone gładkie outside–inside nie mają tutaj przewagi czasowej nawet w uproszczonym modelu idealnego prowadzenia; (2) regulator nie realizuje zadanej krzywej i używa niewłaściwego odniesienia dla jej krzywizny oraz zamierzonej prędkości poprzecznej. Nie wykazano wadliwości opony ani charakterystyki prędkości.

Pierwsza potwierdzona rozbieżność sterowania występuje **w żądaniu przyspieszenia, przed oponą i limitem yaw**: dla idealnego stanu na krzywej błąd pozycji wynosi zero, ale regulator nadal tłumi celowy ruch d′(s) i korzysta z preview reference zamiast całej trajektorii. Kontrolna zmiana samego układu odniesienia i preview silnie poprawia tracking. Nie przechodzi jednak pełnej zamrożonej bramki; pozostałego błędu nie przypisujemy arbitralnie oponie.

Baza: Draft #103, `9ae0117e90c8076634c8838d9cfee59111ed9ef7`, gałąź `experiment/racing-line-root-cause`, jeden Draft do `experiment/progressive-bend-width`, bez merge. `progressive-b`, referencyjne XY/krzywizna, banda, kerb, opona, wheelspin, silnik, AI, masa i wszystkie parametry regulatora są zamrożone. Default nie używa nowych wejść. Produkcyjne `src/` i `RiderController` niezmienione.

## Pytania, zakres i zamrożenie

Wykorzystano istniejące pomiary #99–#103. Nie powtórzono historycznych macierzy. Nowe pełne capture to cztery ciągłe krzywe × dwa tryby prowadzenia, jeden recapture `wide-tighten` potrzebny do sił/yaw co10ms oraz dwa dodatkowe testy preview. Ostatnie dwa zamrożono osobno **przed** pomiarami, z hashem pierwotnych dziewięciu wyników: [freeze](racing-line-root-cause/freeze.json), [preview-freeze](racing-line-root-cause/preview-freeze.json). Żadnej krzywej, siły ani stałej nie zmieniono po wyniku. Dodatkowe tanie kontrole to sześć fingerprintów #103 oraz dwa powtórzenia bez obserwatorów.

Neutralny zawodnik z #103, masa147kg, jednorodny Grip1/Ruts0/Moisture0,5, Drive1, fizyka #100, krok10ms/4substeps, trzy okrążenia. Dla nowych krzywych start reference4m (31m przed pierwszym łukiem), offset na danej krzywej, v18m/s, yaw wzdłuż prostej, angularVelocity0. To wspólny protokół bez początkowego błędu pozycji; kontrolerzy danej krzywej mają ten sam stan. Lotne okrążenia mierzone od tej samej pozycji startowej. Legacy zachowuje własny start0/offset5m z #103, bez porównywania jego rozruchu z nowymi krzywymi.

Zamrożona bramka trackingu na Lap2+Lap3: RMS poprzeczny≤0,5m, maksimum≤1m, RMS kierunku prędkości względem stycznej≤3°, maksimum≤10°. To jawny próg diagnostyczny, nie nowy balans. Yaw motocykla nie jest utożsamiony z kierunkiem prędkości; slide wymaga różnicy tych kątów.

## 1. Jawne krzywe geometryczne

`Q(s)=P(s)+d(s)n_out(s)`, gdzie P to niezmieniona reference progressive-b. Użyto C2 quintic smoothstep na każdym przejściu, z d′=d″=0 na węzłach:

- **inner:** d=1m na całym obiegu.
- **outside–inside:** wejście8,5m, minimum2m przy50% łuku, utrzymane do wyjścia; powrót do8,5m na pierwszych31m następnej prostej.
- **outside–inside–outside:**8,5→2→8,5m przez obie połowy łuku. Apex50% wynika z symetrii geometrycznej B i lustrzanych warunków wejścia/wyjścia, bez wyboru po czasie.
- **late-apex:** ta sama amplituda, minimum przy65%, zamrożona czułość na późniejszy apex.

Każda linia jest ciągłą krzywą XY, nie sekwencją skokowych targetów. W świecie aktualizowane jest tylko próbkowanie tej funkcji na istniejących accepted boundaries; nie dodaje się solver steps ani nie przesuwa ciała. Dwa narożniki są lustrzane, a d(s) periodyczne co pół okrążenia.

Pełna krzywizna pochodzi z pochodnych Q, nie z `1/(R_ref+d)`:

`A=1+κ_ref d; |Q′|=sqrt(A²+d′²)`

`κ_path=[A(κ_ref A−d″)+d′(κ_ref′d+2κ_ref d′)] / |Q′|³`.

Pochodne d są analityczne. κ_ref′ pochodzi z centralnej różnicy zamrożonego spline ±1mm; niezależny test różnic skończonych całego XY sprawdza κ_path, metrykę, C2 oraz domknięcie. Wszystkie d≥1 są zewnętrznymi normalnymi do wypukłej reference; `1+κ_ref d>0`, projekcja daje tę samą reference i nie ma samoprzecięć. To geometryczne sprawdzenie regularności, a nie wynik trackingu.
''']
    lines.append(table(['Krzywa','droga/lap m','max |κ| /m','min promień m','max a_y przy18 m/s','min v_grip m/s','bound kerb / banda m'],
        [[g['Name'],fmt(g['LengthMeters']),fmt(g['MaximumAbsoluteCurvature'],6),fmt(g['MinimumRadiusMeters']),fmt(g['MaximumAyAt18']),fmt(g['MinimumTractionSpeed']),fmt(g['ConservativeInnerCircle'])+' / '+fmt(g['ConservativeOuterCircle'])] for g in geometry]))
    lines.append('''Wszystkie cztery przechodzą **geometrię i footprint**, także dla dowolnego yaw: okrąg1,05m obejmuje chassis+kierownicę. Minimalny dystans środka do całej bandy/kerb pomniejszono o okrąg, zapasy chord/float i `max|Q′|·Δs/2`; próbki≤5cm. Nie są to punkty ograniczone wyłącznie szerokością przekroju. Pełne kapsuły sprawdzono dodatkowo dla12yaw.

Zamrożony budżet `a_grip=14,546245m/s²` pochodzi z tej samej zdolności Grip1 co #100. `v_grip=sqrt(a_grip/|κ_path|)`, `a_y=v²|κ_path|`. Trzy pierwsze krzywe spełniają **konieczny** warunek przy18m/s; late-apex lokalnie wymaga15,824>14,546 i potrzebuje mniejszej prędkości. Warunek siły normalnej nie dowodzi jeszcze wykonalności całego układu Fx/Fy/wheel/yaw przy Drive1. Bezpieczeństwo przestrzenne i wykonalność dynamiczna są oceniane osobno.

Outside–inside–outside ma niemal ten sam najgorszy promień co inner (25,451 vs25,478m) i dłuższą drogę o29,796m/lap. Są odcinki z łagodniejszą krzywizną, ale przejścia do/z apexu oddają ten zysk. Nie ma podstaw, by zakładać korzyść całego obiegu z samego szerokiego wejścia. Wniosek dotyczy tych jawnych krzywych, nie wszystkich możliwych linii na tym torze.

## 2. Niezależny szacunek prędkości i czasu

Offline point-mass używa pełnej κ_path, zamrożonego budżetu, eksportowanej charakterystyki dostępnego napędu F(v) prosta/łuk oraz oporu z CoreSim. Przejścia prędkości spełniają `v_next²≤v²+2a_tΔl`, z `a_t≤min(F_engine/m,sqrt(a_grip²−a_y²))−F_resistance/m`. Powtarzane przejście w przód/wstecz daje periodyczny profil. **Idealne hamowanie z koła przyczepności jest założeniem optymistycznym** — aktualny Drive1 nie ma takiego sterowania. Model pomija wheelspin, zależne od slide straty, transjenty i actuator yaw; nie jest prognozą Box2D ani próbą wheelspin OFF. Nie używa kar/bonusów. To prosty, jawny punkt odniesienia dla samej drogi, krzywizny i dostępnej mocy.
''')
    lines.append(table(['Krzywa','czas/lap s','dwa lotne s','min/max v m/s','różnica siatki10cm−5cm s/lap'],
        [[e['Name'],fmt(e['LapSeconds'],6),fmt(e['TwoFlyingSeconds'],6),fmt(e['MinimumSpeed'])+' / '+fmt(e['MaximumSpeed']),fmt(e['CoarseLapDifferenceSeconds'],6)] for e in estimates]))
    lines.append('''Nawet ten idealizowany model daje inner przewagę: OIO30m dodatkowej drogi nie odzyskuje czasem prędkości. Kod sprawdza zbieżność i ograniczenia dyskretne; test analitycznego okręgu odtwarza `T=2πsqrt(R/a_grip)`, a brak napędu nie daje samoczynnego przyspieszenia. Eksporty `*-geometry.csv`, `*-point-mass.csv`, [point-mass-summary](racing-line-root-cause/point-mass-summary.json) oddzielają geometrię od rzeczywistych przejazdów.

![Geometria, krzywizna, a_y i niezależny speed estimate](racing-line-root-cause/geometry-and-speed.png)

## 3. Zadana i wykonana trajektoria

Tryby:

- **existing:** oryginalny regulator, tylko ciągły target d(s) zamiast skoków.
- **path-frame:** izolowany wariant z bieżącą κ_path i błędami w układzie trajektorii, bez preview. Zachowany także tam, gdzie pogarsza wynik i narusza kerb/bandę. Usunięcie wyprzedzenia było dodatkową różnicą, więc nie użyto go jako dowodu przeciw krzywej lub oponie.
- **path-preview:** dwie dodatkowe kontrole przywracają dokładnie dotychczasowy reference lookahead `2·YawResponseSeconds=0,7s`; heading i długość łuku preview pochodzą teraz z całego Q(s). Błędy pozycji/prędkości są w aktualnej stycznej/normalnej Q. Te same T, tyre law, wheel, motor, clamp0,95, yaw authority i energy guard. Brak dopasowania parametrów.

Błąd poprzeczny i wzdłużny to projekcje `X_actual−Q(s_actual_reference)` na normalną/styczną Q; oba są mierzone przy tej samej zaakceptowanej reference. Nie są błędem względem arbitralnego harmonogramu czasu. Na zmiennej d(s) błąd offsetu ma również składową wzdłuż stycznej trajektorii. Nie wykorzystano najbliższego punktu, który z definicji zerowałby składową wzdłużną. Różnicę tempa jazdy ocenia osobno crossing time i speed.

Rzeczywista κ ruchu = zmiana headingu prędkości / wykonana droga w accepted interval. Yaw ciała, kierunek prędkości i zadana styczna są trzema osobnymi wielkościami. Tabele trackingu dotyczą Lap2+Lap3; safety obejmuje całe3lap.
''')
    lines.append(table(['Przypadek','RMS/max poprz. m','RMS wzdłużny m','RMS kierunku v °','RMS yaw−styczna °','tracking','safety'],
        [[c['Case'],fmt(c.get('RmsLateral'))+' / '+fmt(c.get('MaximumLateral')),fmt(c.get('RmsLongitudinal')),fmt(c.get('RmsVelocityHeadingDegrees')),fmt(c.get('RmsBodyHeadingDegrees')),
          'PASS' if c['TrackingPass'] else 'FAIL' if c['Path'] else 'skoki targetu', 'PASS' if c['BoundarySafe'] else 'FAIL'] for c in cases]))
    lines.append('''Nie uznano poprawy za zaliczenie progu. OIO path-preview: RMS≈0,548m, max≈1,573m; RMS kierunku≈2,745°, ale przestrzenne progi nadal niespełnione. Także inner ma lokalny błąd>1m przy szybkim wejściu. Nie ma tutaj dowodu, że dowolny z tych regulatorów dokładnie realizuje wszystkie geometrycznie bezpieczne linie przy aktualnym Drive1.

![Wszystkie krzywe: zadane XY i rzeczywiste przebiegi](racing-line-root-cause/intended-and-executed-xy.png)
![OIO: offset, krzywizna, prędkość, błędy i żądanie sterowania](racing-line-root-cause/tracking-mechanism.png)

## 4. Potwierdzony mechanizm przed limitem siły

Oryginalny [SlipCoupledDynamics](../../tools/shared-world-speedway/SlipCoupledDynamics.cs) liczy:

`a_request = v_ref_tangent²·κ_reference_preview + (d_actual−target)/T² + 2·v_out_reference/T`, `T=0,65+4·0,1=1,05s`.

Po tym żądaniu dopiero następują clamp±0,95 budżetu, przeliczenie target slip, yaw actuator i rzeczywiste siły opony. Preview używa headingu reference, a nie d′/d″. Przy idealnej realizacji Q(s) offset error=0, ale `v_out_reference=d′·ds/dt` nie jest zerem. Regulator traktuje więc zamierzony ruch jak ruch do wytłumienia. Właściwe zero błędu prędkości jest normalne do **stycznej Q**, nie P.

Poniższa kontrola jest czysto kinematyczna: rzeczywisty stan umieszczony dokładnie na OIO z prędkością18m/s styczną do Q, bez błędu pozycji. To nie siłowa korekta Box2D ani nowa symulacja. Wymagane a_n pochodzi z pełnej κ_path. To samo żądanie jest niezależnie sprawdzone bezpośrednim wywołaniem oryginalnej funkcji Forces w teście C#.
''')
    oracle=[]
    for fraction in [.3,.7]:
        r=min(geometries['outside-inside-outside'],key=lambda r:abs(r['s']-(35+bend*fraction)))
        oracle.append([fmt(fraction*100,0),fmt(r['offset']),fmt(324*r['curvature']),fmt(r['old_geometry_at18']),fmt(r['old_damping_at18']),fmt(r['old_perfect_demand_at18']),fmt(r['old_perfect_demand_at18']-324*r['curvature'])])
    lines.append(table(['% łuku','d m','wymagane a_n','reference preview','tłumienie v_out','żądanie stare','błąd żądania'],oracle))
    lines.append('''Wymagana siła normalna jest w tych stanach poniżej budżetu; zła komenda istnieje już przed ograniczeniem i przed odpowiedzią opony/yaw. Poprawa RMS2,49→0,55m po dostarczeniu pełnej geometrii przy identycznych pozostałych prawach jest interwencją potwierdzającą znaczenie tego mechanizmu. Nie dowodzi, że sam ten wariant usuwa cały błąd.

## 5. Siły, yaw, opóźnienie i apex

`Fx/Fy` poniżej są rzeczywistymi siłami w osiach motocykla; wymaganie `v²κ_path` przy aktualnym v jest osobnym warunkiem. Udział required>budget oznacza, że w tych punktach nie da się podążać po zadanej κ z taką prędkością nawet idealną siłą. Nie oznacza błędu tyre law. Yaw force clamp i energy guard są liczone osobno od stałego skalowania Technique≈0,875.
''')
    lines.append(table(['Przypadek','mean Fx/Fy N','max wheelspin','near grip ≥95%','required>budget','steer clamp','yaw force/energy clamp %'],
        [[c['Case'],fmt(c.get('MeanFx'))+' / '+fmt(c.get('MeanFy')),fmt(c.get('MaximumWheelspin'),4),fmt(c.get('NearGripPercent'),2),fmt(c.get('RequiredAboveBudgetPercent'),2),fmt(c.get('SteeringClampedPercent'),2),
          fmt(c.get('YawForceClampedPercent'),2)+' / '+fmt(c.get('YawEnergyClampedPercent'),2)] for c in cases]))
    lines.append('''Osiągnięty apex to minimum **rzeczywistego** offsetu wewnątrz łuku, nie polecenie. Dla OI d=2 jest docelowym plateau od połowy łuku; dla inner brak pojedynczego apexu. Opóźnienie połowy zacieśniania mierzy pierwsze przekroczenie rzeczywistego d=5,25m względem miejsca/czasu, gdzie ciągła krzywa przechodzi przez5,25m. Próbkowanie10ms ogranicza rozdzielczość opóźnienia; nie jest to dopasowana stała czasowa regulatora.
''')
    lines.append(table(['Przypadek','d rzeczywisty35/50/80% m','minimum d m','miejsce min %','lag połowy: s / reference m'],
        [[c['Case'],' / '.join(fmt(phase_mean(c['Case'],k)) for k in ['Offset35','Offset50','Offset80']),fmt(phase_mean(c['Case'],'ActualMinimumOffset')),
          fmt(100*phase_mean(c['Case'],'ActualApexFraction'),1),fmt(phase_mean(c['Case'],'HalfTransitionLagSeconds'))+' / '+fmt(phase_mean(c['Case'],'HalfTransitionLagReferenceMeters'))]
         for c in cases if c['Path'] not in [None,'inner'] or c['Case']=='legacy-wide-tighten']))
    lines.append('''Legacy wide-tighten jest odtworzony bitowo z #103: żądanie2m przy35% nie wyznacza geometrycznego apexu. Pełny10ms zapis pokazuje opóźnienie wykonania i przełączenie na9,5m przy80%, zanim ciało zrealizuje zacieśnianie. [phases.csv](racing-line-root-cause/phases.csv) zachowuje oba łuki osobno dla Lap2/Lap3, entry/middle/exit speeds, offsety i wszystkie crossing times; ostatnie legacy okno za metą jest jawnie niepełne i wyłączone ze średniej okien.

Konkretne chwile legacy, pierwszy łuk Lap2, pierwszy zaakceptowany stan na/za danym landmarkiem:
''')
    landmarks=[]
    a=[r for r in telemetry['legacy-wide-tighten'] if lap+35<=r['s']<=lap+35+bend]
    for fraction in [.35,.5,.8]:
        r=next(r for r in a if r['s']>=lap+35+bend*fraction)
        landmarks.append([fmt(100*fraction,0),fmt(r['t'],2),fmt(r['offset']),fmt(r['target']),fmt(r['speed']),fmt(r['old_geometry_demand']),fmt(r['old_offset_demand']),fmt(r['old_damping_demand']),fmt(r['steering_demand'])])
    lines.append(table(['% łuku','t s','offset m','target m','v m/s','preview a','offset a','damping a','żądanie a'],landmarks))
    lines.append('''Parametr zewnętrznego feedbacku T=1,05s celowo spowalnia zmianę linii względem yaw. W idealnym przybliżeniu PD o natychmiastowej sile i stałym układzie odniesienia odpowiedź skokowa ma `1−(1+t/T)exp(−t/T)`; połowę ruchu osiąga po≈1,678T=1,76s. Rzeczywiste układy tyre/yaw dodają dynamikę, więc nie traktujemy tego jako ścisłej identyfikacji, ale skok dopiero przy35% nie jest deklaracją osiągnięcia2m przy50%. Przesunięcie przyszłego targetu bez ciągłej d(s) i jej pochodnych nie definiuje oczekiwanego outside–inside–outside.

![Legacy: skokowe targety, składniki żądania i yaw](racing-line-root-cause/legacy-wide-mechanism.png)

## 6. Czas i droga: cały obieg oraz wejście–łuk–prosta

Okno od31m przed wejściem do31m za wyjściem obejmuje jeden łuk i obie połowy sąsiednich prostych. Cztery pełne okna dokładnie pokrywają dwa lotne obiegi nowych prób. Czas i droga są z rzeczywistych XY/crossingów, bez podstawiania idealnego d(s). Wynik bez zaliczonego trackingu nie jest czasem wykonania zaplanowanej linii. Odrzucona safety pozostaje diagnostyką.
''')
    lines.append(table(['Przypadek','Lap2+3 s','droga lotna m','okno średnio s/m','Δdroga / v_inner s','efekt średniej v s'],
        [[c['Case'],fmt(c['FlyingSeconds'],6),fmt(c['FlyingDistance']),fmt(phase_mean(c['Case'],'Seconds'))+' / '+fmt(phase_mean(c['Case'],'Distance')),
          fmt(c['DistanceCostAtInnerMeanSpeed']),fmt(c['SpeedEffectSeconds'])] for c in cases]))
    lines.append('''Rozkład czasu jest tożsamością `ΔT=(L−L_inner)/v̄_inner + L(1/v̄−1/v̄_inner)`, z rzeczywistym v̄=L/T. Nie jest obietnicą fizycznego utrzymania speed inner na innej trasie. Oddziela koszt drogi od zmiany tempa. Poprawniejsze zacieśnienie nie gwarantuje szybszego czasu: stary kontroler wykonuje inną, gładszą i mniej ciasną linię niż Q. Nie wolno przypisywać jej czasu planowanemu outside–inside–outside.

## 7. Bezpieczeństwo i energia wszystkich prób

Pełne kapsuły przy rzeczywistym yaw: accepted inner≥0,05m / outer≥0,5m. Między krokami bound z #103 odejmuje `(max speed+1,05·max |spin|)h/2+0,012m`; wymagany brak przekroczenia kredy i≥0,5m bandy. Dotyczy odizolowanego bezkontaktowego ciała, nie ogólnego CCD.
''')
    lines.append(table(['Przypadek','kerb min/bound m','banda min/bound m','safety','energy residual J'],
        [[c['Case'],fmt(c['MinimumInner'])+' / '+fmt(c['ConservativeInner']),fmt(c['MinimumOuter'])+' / '+fmt(c['ConservativeOuter']),
          'PASS' if c['BoundarySafe'] else 'FAIL',fmt(r['Energy']['BalanceResidualJoules'])] for c,r in zip(cases,runs)]))
    lines.append(f'''Nie usunięto nieudanych path-frame inner i late-apex, ich pełne trajektorie są zachowane. Wszystkie próby kończą3lap, tyre budget≤1 i dotychczasowy audyt całego układu body+wheel zamyka bilans. Yaw guard excess pozostaje0. To walidacja spójności obliczeń, nie empiryczna kalibracja rzeczywistej opony.

## Rozstrzygnięcie i jeden następny PR

1. **Geometria:** w tej rodzinie nie znaleziono podstaw do oczekiwanej przewagi outside–inside. OIO ma korzystniejsze fragmenty κ, lecz prawie nie poprawia najgorszego promienia i wydłuża drogę. To nie dowód, że żadna inna linia może być lepsza.
2. **Tracking:** obecny regulator nie wykonuje tych krzywych. Pierwszy potwierdzony błąd leży w reprezentacji i żądaniu: brak κ całej d(s) i tłumienie celowej prędkości poprzecznej jako błędu. Nie trzeba hipotezy o wadliwej oponie, żeby go odtworzyć.
3. **Force/yaw:** część stanów przy aktualnych szybkich prędkościach przekracza konieczny budżet v²κ, lecz zła komenda występuje także w idealnych stanach poniżej budżetu. Limity yaw/energii oraz grip są rozdzielone w tabelach; nie nazywamy ich wspólnie „brakiem AI”.
4. **Opona/prędkość:** brak rozstrzygającego dowodu wadliwości. Idealny point-mass nie uwzględnia strat slide/wheel i hamuje idealnie. Różnica czasu do niego nie jest testem poprawności opony. Nie uruchomiono wheelspin ON/OFF ani dopasowania napędu, ponieważ tracking nie przeszedł bramki.
5. **Nierozstrzygnięte:** po path-preview nadal pozostaje błąd. Nie wykazano jeszcze istnienia pełnego, zgodnego z rzeczywistym tyre/engine/yaw rozwiązania dla Q(t) przy aktualnym Drive1; sam warunek a_y≤a_grip jest konieczny, ale niewystarczający.

**Jeden minimalny następny PR: kontrakt ciągłej trajektorii C2 dla imposed-line controller, z referencją stycznej, κ, d′ i d″ oraz tym samym horyzontem preview, default OFF.** Usuwa potwierdzony brak informacji i błędny układ odniesienia, zamiast stroić oponę lub czasy. Nie promować obecnego prototypu jako gotowej poprawki trackingu.

Dokładny test akceptacyjny i rozstrzygający pozostałą niepewność: dla zamrożonego OIO przy18m/s (z limitowaniem tylko tam, gdzie nie istnieje rozwiązanie) wyznaczyć inverse-feasibility **oryginalnych** równań poślizgu/koła: wymagane globalne F=m·a, body slip, Fx/Fy, omega/engine torque i yaw torque wraz z energy guard. Bez dodatkowych sił, korekty XY lub zmiany parametrów. Jeśli rozwiązanie0≤Drive≤1 i legalny yaw istnieją w całym oknie, odegrać je w tym samym Box2D i wymagać zamrożonego trackingu. Istniejące rozwiązanie + błąd prowadzenia oznacza actuator/preview tracking; brak rozwiązania wskazuje konkretny constraint Fx/Fy/engine/yaw i wymaganą mniejszą prędkość. To test wykonalności, bez optymalizacji czasu i bez obwiniania modelu opony. Ten PR dostarcza dane wejściowe i progi, ale nie twierdzi, że rozwiązał to jeszcze nierozstrzygnięte zadanie.

## Walidacja, pochodzenie i reprodukcja

**47/47 celowanych C#,3/3 nowych testów analizy,11/11 Node; restore i Release --warnaserror poprawne.** [targeted-tests.json](racing-line-root-cause/targeted-tests.json) zawiera rzeczywiste wyniki końcowego TRX. Celowane testy geometrii, C2/κ przez niezależne różnice XY, pełnych kapsuł12yaw/projekcji, perfect-state żądania wywołanego wprost w oryginalnym Forces, izolacji opcji, poprawy i nadal niezaliczonego trackingu oraz obserwowanego/plain determinism. Analiza ma osobne testy analitycznego circle, granicy braku napędu i bramki wheelspin. Status standardowego Draft Windows CI na końcowym HEAD jest w PR. `metadata-correction.json` rejestruje poprawkę etykiety/flag dwóch preview capture; pozycje, siły, czasy i fingerprinty nie zmieniły się. Hash oryginalnych dziewięciu wyników z preview-freeze jest zachowany.

**6/6 fingerprintów progressive-b #103 bez zmian;2/2 nowych plain repeats bitowo zgodnych. {sum(history.values())}/{sum(history.values())} historycznych plików** zachowało SHA-256: {history}. Nowy manifest obejmuje surowe CSV/JSON oraz analizę/wykresy. Wszystkie surowe eksporty odmawiają nadpisania plików. Default nie korzysta z AuthoredRacingPath ani diagnosticPathGuidance; stare wymiary, opona/koło/silnik/AI, parametry i geometrie nie zostały zmienione. Nowy warunkowy blok modyfikuje wyłącznie żądanie diagnostycznego kontrolera, przed niezmienionym clamp/tyre/yaw. Stare API przy opcjach null/false zachowuje dotychczasowy rachunek.

```powershell
dotnet restore
dotnet build -c Release --no-restore --warnaserror
# Użyć nowego pustego katalogu; wykonywać z root repo.
dotnet tools/shared-world-speedway/bin/Release/net8.0/shared-world-speedway.dll racing-line-root-cause ../root-reproduction freeze
dotnet tools/shared-world-speedway/bin/Release/net8.0/shared-world-speedway.dll racing-line-root-cause ../root-reproduction geometry
dotnet tools/shared-world-speedway/bin/Release/net8.0/shared-world-speedway.dll racing-line-root-cause ../root-reproduction measure
dotnet tools/shared-world-speedway/bin/Release/net8.0/shared-world-speedway.dll racing-line-root-cause ../root-reproduction preview-freeze
dotnet tools/shared-world-speedway/bin/Release/net8.0/shared-world-speedway.dll racing-line-root-cause ../root-reproduction preview
dotnet tools/shared-world-speedway/bin/Release/net8.0/shared-world-speedway.dll racing-line-root-cause ../root-reproduction verify
python tools/shared-world-speedway/racing-line-root-cause-report.py --plots
python -m unittest discover -s tests/calibration -p test_racing_line_root_cause_estimate.py
```

Raport analizuje zapisany katalog docs/experiments/racing-line-root-cause; --plots wymaga matplotlib, pozostała analiza używa standardowej biblioteki. Reprodukcja surowych capture do osobnego katalogu nie nadpisuje dowodów. Kontrole historyczne czytają istniejące #103 i nie uruchamiają dawnych macierzy. Koszt wykonania obserwatorów i offline quadrature nie wpływa na fizykę ani fingerprint.
''')
    (ROOT/'docs/experiments/racing-line-root-cause.md').write_text('\n'.join(lines),encoding='utf-8',newline='\n')
    save_json('provenance-checks.json',{'HistoricalFiles':history,'FingerprintsMatched':len(checks),'WheelComparisonExecuted':False})
    manifest={p.name:hashlib.sha256(p.read_bytes()).hexdigest() for p in sorted(DATA.iterdir()) if p.is_file() and p.name!='manifest.json'}
    save_json('manifest.json',manifest)
    print(f'Wrote report, {len(cases)} real captures, {len(manifest)} evidence files; {sum(history.values())} historical hashes preserved.')

def main():
    freeze,lap,bend,geometries,estimates,cases,phases,telemetry,runs=analyse()
    if '--plots' in sys.argv:make_plots(lap,bend,geometries,estimates,cases,phases,telemetry,runs)
    write_report(freeze,lap,bend,geometries,estimates,cases,phases,telemetry,runs)


if __name__=='__main__':
    main()
