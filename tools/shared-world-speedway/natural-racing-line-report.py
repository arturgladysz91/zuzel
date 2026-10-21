"""Read native Box2D evidence only. No dynamics integrator, braking model or optimizer."""
import csv
import argparse
import hashlib
import importlib.util
import json
import math
from pathlib import Path
import sys

ROOT=Path(__file__).resolve().parents[2]
DATA=ROOT/'docs/experiments/natural-racing-line-vs-inner'
OLD=ROOT/'docs/experiments/speed-grip-line-crossover'
spec=importlib.util.spec_from_file_location('crossover_metrics',ROOT/'tools/shared-world-speedway/speed-grip-crossover-report.py')
metrics=importlib.util.module_from_spec(spec);spec.loader.exec_module(metrics)
load=metrics.load
read=metrics.read
LAP=metrics.LAP;HALF=LAP/2;ENTRY=35;EXIT=metrics.EXIT

def save(name,value):
    (DATA/name).write_text(json.dumps(value,indent=2,allow_nan=False)+'\n',encoding='utf-8',newline='\n')

def validation(complete,safe,stats,laps):
    periodic=complete and len(laps)==3 and min(laps)>0 and max(laps)/min(laps)<=1.005
    checks={'eight laps':complete,'whole-run footprint and intervals':safe,'tracking':metrics.tracking_pass(stats),'last3 periodic within0.5%':periodic}
    return [key for key,okay in checks.items() if not okay]

def power3_allowed(cases):
    return len(cases)==2 and all(c['Rejections'] for c in cases)

def analyse_case(engine,natural):
    name=engine+('-natural' if natural else '-inner');folder=DATA if natural else OLD
    run=load(folder/(name+'.json'));rows=read(folder/(name+'-tracking.csv.gz'));forces=read(folder/(name+'-forces.csv.gz'))
    with (folder/(name+'-accepted.csv')).open(encoding='utf-8-sig') as f:
        assert all(float(r['drive'])==1 for r in csv.DictReader(f)), 'Drive must remain1'
    assert len(rows)==len(forces) and all(r['t']==f['t'] and r['dt']==f['dt'] for r,f in zip(rows,forces))
    full=len(run['CrossingSeconds'])==8 and run['Result']['Completed']
    start=run['CrossingSeconds'][-3] if full else 0;end=run['CrossingSeconds'][-1] if full else run['Result']['TimeSeconds']
    stats=metrics.old.series_stats(rows,start,end)
    monotonic=all(a['s']<b['s'] for a,b in zip(rows,rows[1:]))
    time_at,distance_at=metrics.old.time_and_distance_functions(rows,run)
    window=[(r,f) for r,f in zip(rows,forces) if start<=r['t']<end]
    def weight(r):return max(0,min(end,r['t']+r['dt'])-max(start,r['t']))
    seconds=sum(weight(r) for r,f in window)
    def mean(fn):return sum(fn(r,f)*weight(r) for r,f in window)/seconds
    slide=lambda r:abs(math.degrees(math.atan2(math.sin(r['body_heading']-r['velocity_heading']),math.cos(r['body_heading']-r['velocity_heading']))))
    # Clearance fields describe the END of each native step. These speed maxima
    # therefore use reconstructed END velocity and end reference from the next row.
    physical=[];events={}
    for i,(r,f) in enumerate(zip(rows,forces)):
        after_speed=math.hypot(r['vx']+f['accel_x']*r['dt'],r['vy']+f['accel_y']*r['dt'])
        after_s=rows[i+1]['s'] if i+1<len(rows) else run['Result']['Riders'][0]['TotalReferenceMeters']
        contained=r['inner']>=.05 and r['outer']>=.5
        if contained:physical.append({'t':r['t']+r['dt'],'s':after_s,'speed':after_speed,'straight':metrics.straight_id(after_s)})
        def event(key,condition,after=False):
            if condition and key not in events:
                events[key]={'Time':r['t']+(r['dt'] if after else 0),'BracketStart':r['t'],'BracketEnd':r['t']+r['dt'],
                    'Reference':r['s'],'SpeedKph':3.6*(after_speed if after else r['speed']),
                    'LateralError':r['lateral_error'],'VelocityHeadingErrorDegrees':math.degrees(r['velocity_heading_error']),
                    'RequiredPathAcceleration':r['speed']**2*abs(r['path_curvature']),'BudgetAcceleration':r['budget']/f['mass'],
                    'ActualNormalAcceleration':abs(f['normal_force'])/f['mass'],'PlannedCurvature':r['path_curvature'],'ActualCurvature':r['actual_curvature'],
                    'InnerAfter':r['inner'],'OuterAfter':r['outer'],'Utilization':r['utilization'],'Fx':r['fx'],'Fy':r['fy'],
                    'WheelSlip':r['wheelspin'],'YawRequested':r['yaw_requested'],'YawAuthority':r['yaw_authority'],'YawEnergy':r['yaw_energy']}
        event('PathAboveTotalBudget',r['speed']**2*abs(r['path_curvature'])>r['budget']/f['mass'])
        event('TrackingAbove1m',abs(r['lateral_error'])>1)
        event('HeadingAbove10deg',abs(math.degrees(r['velocity_heading_error']))>10)
        event('SafetyMarginLost',r['inner']<.05 or r['outer']<.5,True)
        event('OuterCrossed',r['outer']<0,True)
        event('InnerCrossed',r['inner']<0,True)
    phases=[]
    if full and monotonic:
        for cycle in range(12,16):
            entry=ENTRY+cycle*HALF;exit=EXIT+cycle*HALF;corner=[r for r in rows if entry<=r['s']<=exit]
            if not corner:continue
            p={'Name':name,'Lap':cycle//2+1,'Bend':cycle%2+1}
            for key,fraction in [('EntryKph',0),('P25Kph',.25),('P50Kph',.5),('P75Kph',.75),('ExitKph',1)]:
                p[key]=metrics.interpolate(rows,entry+(exit-entry)*fraction,'speed')*3.6
            p.update({'MinimumKph':min(r['speed']*3.6 for r in corner),'MaximumSlideDegrees':max(slide(r) for r in corner),
                'MaximumTrackingError':max(abs(r['lateral_error']) for r in corner),
                'EntryActualRadius':1/abs(metrics.interpolate(rows,entry,'actual_curvature')),
                'EntryPlannedRadius':1/abs(metrics.interpolate(rows,entry,'path_curvature')),
                'CornerMinActualRadius':1/max(abs(r['actual_curvature']) for r in corner),
                'CornerMinPlannedRadius':1/max(abs(r['path_curvature']) for r in corner),
                'EntryOffset':metrics.interpolate(rows,entry,'offset'),'ExitOffset':metrics.interpolate(rows,exit,'offset')})
            phases.append(p)
    lap_durations=run['LapSeconds'][-3:]
    rejection=validation(full,run['BoundarySafe'],stats,lap_durations)
    straight=[p for p in physical if p['straight']]
    flying_straight=[p for p in straight if start<=p['t']<=end]
    stats.update({'Name':name,'Engine':engine,'Natural':natural,'Complete':full,'MonotonicReference':monotonic,
        'BoundarySafe':run['BoundarySafe'],'Rejections':rejection,'Rankable':not rejection,'LapSeconds':run['LapSeconds'],
        'Last3SpreadPercent':100*(max(lap_durations)/min(lap_durations)-1),'TimingSeconds':(end-start)/2 if full else None,
        'DistancePerLap':(distance_at(end)-distance_at(start))/2 if full else None,
        'MeanSlideDegrees':mean(lambda r,f:slide(r)),'MaximumSlideDegrees':max(slide(r) for r,f in window),
        'MaximumContainedStraightKph':max((p['speed']*3.6 for p in straight),default=None),
        'FlyingContainedStraightKph':max((p['speed']*3.6 for p in flying_straight),default=None),
        'FlyingStraight1Kph':max((p['speed']*3.6 for p in flying_straight if p['straight']==1),default=None),
        'FlyingStraight2Kph':max((p['speed']*3.6 for p in flying_straight if p['straight']==2),default=None),
        'FlyingAnyStraightKph':max((r['speed']*3.6 for r,f in window if metrics.straight_id(r['s'])),default=None),
        'MinimumInner':run['MinimumInner'],'MinimumOuter':run['MinimumOuter'],'ConservativeInner':run['ConservativeInner'],'ConservativeOuter':run['ConservativeOuter'],
        'MaximumUtilization':run['MaximumUtilization'],'Energy':run['Energy'],
        'MaximumLinearImpulseResidualNs':max(f['body_force_residual']*f['dt'] for f in forces),
        'MaximumAngularImpulseResidualNms':max(abs(f['yaw_residual'])*f['dt'] for f in forces),
        'InitialTargetHeadingDegrees':math.degrees(rows[0]['path_heading']),'InitialTrackingError':rows[0]['lateral_error'],
        'MinimumForceBudgetAcceleration':min(r['budget']/f['mass'] for r,f in zip(rows,forces)),
        'Events':events,'Phases':phases,
        'FirstBend':[{'Reference':ENTRY+(EXIT-ENTRY)*fraction, 'SpeedKph':metrics.interpolate(rows,ENTRY+(EXIT-ENTRY)*fraction,'speed')*3.6,
            'LateralError':metrics.interpolate(rows,ENTRY+(EXIT-ENTRY)*fraction,'lateral_error')} for fraction in [0,.25,.5,.75,1]] if monotonic else []})
    return stats,rows

def history():
    checks={}
    # This report certifies its frozen base, not experiments added AFTER #111.
    # Retain the original manifest set when running its regression in later PRs.
    frozen_names=load(ROOT/'docs/experiments/natural-racing-line-vs-inner/analysis.json')['HistoricalHashes']
    for name in sorted(frozen_names):
        manifest=ROOT/'docs/experiments'/name/'manifest.json'
        values=load(manifest)
        for file,digest in values.items():assert hashlib.sha256((manifest.parent/file).read_bytes()).hexdigest().lower()==digest.lower(),(manifest,file)
        checks[manifest.parent.name]=len(values)
    return checks

def analyse(engines):
    cases=[];cache={};pairs=[]
    for engine in engines:
        for natural in [False,True]:
            c,rows=analyse_case(engine,natural);cases.append(c);cache[c['Name']]=rows
        a,b=cases[-2:];delta=b['TimingSeconds']-a['TimingSeconds']
        distance,speed=metrics.decompose(b['DistancePerLap'],b['TimingSeconds'],a['DistancePerLap'],a['TimingSeconds'])
        assert abs(delta-distance-speed)<1e-10
        pairs.append({'Engine':engine,'Qualified':a['Rankable'] and b['Rankable'],'DeltaSeconds':delta,'DeltaPercent':100*delta/a['TimingSeconds'],
            'DistanceCostSeconds':distance,'SpeedEffectSeconds':speed,'InnerSeconds':a['TimingSeconds'],'NaturalSeconds':b['TimingSeconds']})
    source_files=['freeze.json','engine-runs.json']+[engine+'-inner'+suffix for engine in engines for suffix in ['.json','-accepted.csv','-tracking.csv.gz','-forces.csv.gz']]
    a={'Cases':cases,'Pairs':pairs,'HistoricalHashes':history(),'Geometry':load(DATA/'geometry-summary.json'),'Reuse':load(DATA/'reuse-verification.json'),
        'HistoricalSourcesSha256':{file:hashlib.sha256((OLD/file).read_bytes()).hexdigest().upper() for file in source_files},
        'NewEightLapWorlds':len(engines),'HistoricalEightLapWorldsRerun':0,'AllAcceptedDriveOne':True}
    save('analysis.json',a)
    phases=[p for c in cases for p in c['Phases']]
    if phases:
        with (DATA/'phases.csv').open('w',newline='',encoding='utf-8') as f:
            writer=csv.DictWriter(f,fieldnames=list(phases[0]));writer.writeheader();writer.writerows(phases)
    return a,cache

def plots(a,cache):
    import matplotlib
    matplotlib.use('Agg')
    import matplotlib.pyplot as plt
    plt.rcParams.update({'svg.hashsalt':'natural-line-frozen','font.size':10})
    engines=[p['Engine'] for p in a['Pairs']];colors={'inner':'#147ca3','natural':'#b84222'}
    boundary=load(ROOT/'docs/experiments/progressive-bend-width/progressive-b-geometry.json')
    planned={n:read(DATA/(n+'-geometry.csv')) for n in ['crossover-inner','natural-racing-line']}
    def finish(fig,name):
        fig.savefig(DATA/(name+'.png'),dpi=160)
        fig.savefig(DATA/(name+'.svg'),metadata={'Date':None})
        p=DATA/(name+'.svg');p.write_text('\n'.join(line.rstrip() for line in p.read_text(encoding='utf-8').splitlines())+'\n',encoding='utf-8',newline='\n');plt.close(fig)
    fig,axes=plt.subplots(1,len(engines),figsize=(7*len(engines),6),squeeze=False,layout='constrained')
    for ax,engine in zip(axes[0],engines):
        for edge,color,label in [('inner','#555555','Kreda'),('outer','#ad8a36','Banda')]:
            xy=[p[edge] for segment in boundary['segments'] for p in segment['sections']]
            ax.plot([p['x'] for p in xy],[p['y'] for p in xy],color=color,lw=1.4,label=label)
        for suffix,geometry,label in [('inner','crossover-inner','A INNER'),('natural','natural-racing-line','B NATURAL')]:
            g=planned[geometry];ax.plot([r['x'] for r in g],[r['y'] for r in g],color=colors[suffix],lw=1.5,ls='--',label=label+' plan C3')
            c=next(c for c in a['Cases'] if c['Name']==engine+'-'+suffix);rows=cache[c['Name']];start=sum(c['LapSeconds'][:6]);end=sum(c['LapSeconds'])
            rr=[r for r in rows if start<=r['t']<=end]
            ax.plot([r['x'] for r in rr],[r['y'] for r in rr],color=colors[suffix],lw=1,label=label+' Box2D, okr.7–8')
        ax.set(aspect='equal',xlabel='X [m]',ylabel='Y [m]',title=engine.upper()+' — przejazdy odrzucone')
        ax.grid(alpha=.2);ax.legend(fontsize=8,loc='upper center',bbox_to_anchor=(.5,-.11),ncol=2)
    fig.suptitle('Linia zadana i rzeczywisty ruch; pełne ślady obejmują wyjazdy poza tor')
    finish(fig,'xy')
    fig,axes=plt.subplots(3,len(engines),figsize=(7*len(engines),10),squeeze=False,layout='constrained')
    for col,engine in enumerate(engines):
        for suffix,geometry,label in [('inner','crossover-inner','A INNER'),('natural','natural-racing-line','B NATURAL')]:
            rr=[r for r in cache[engine+'-'+suffix] if 6*LAP<=r['s']<=7*LAP];x=[r['s']-6*LAP for r in rr];color=colors[suffix]
            axes[0,col].plot(x,[r['speed']*3.6 for r in rr],color=color,label=label+' Box2D')
            axes[1,col].plot(x,[r['actual_curvature'] for r in rr],color=color,label=label+' ruch')
            g=planned[geometry];axes[1,col].plot([r['s'] for r in g],[r['curvature'] for r in g],ls='--',color=color,label=label+' plan')
            axes[2,col].plot(x,[r['lateral_error'] for r in rr],color=color,label=label)
        for ax in axes[:,col]:
            for e in [ENTRY,ENTRY+HALF]:ax.axvspan(e,e+EXIT-ENTRY,color='.8',alpha=.2)
            ax.set(xlim=(0,LAP),xlabel='Położenie referencyjne w okrążeniu [m]');ax.grid(alpha=.25);ax.legend(fontsize=8)
        axes[0,col].set(title=engine.upper()+' — okrążenie referencyjne 7, DIAGNOSTYKA',ylabel='Prędkość [km/h]')
        axes[1,col].set(ylabel='Krzywizna XY [1/m]');axes[2,col].set(ylabel='Błąd boczny [m]')
        axes[2,col].axhline(1,color='.4',ls=':');axes[2,col].axhline(-1,color='.4',ls=':')
    finish(fig,'speed-curvature-error')

def manifest():
    save('manifest.json',{p.name:hashlib.sha256(p.read_bytes()).hexdigest().upper() for p in sorted(DATA.iterdir()) if p.is_file() and p.name!='manifest.json'})

def write_report(a):
    def f(value,digits=3):return '—' if value is None else f'{value:.{digits}f}'
    def cell(c,key):return f(c[key])
    def phase(c,bend,key):return sum(p[key] for p in c['Phases'] if p['Bend']==bend)/2
    def table(engine):
        aa,bb=[c for c in a['Cases'] if c['Engine']==engine]
        rows=[]
        for bend in [1,2]:
            for label,key in [('Wejście','EntryKph'),('25%','P25Kph'),('50%','P50Kph'),('75%','P75Kph'),('Wyjście','ExitKph')]:
                rows.append((f'Łuk {bend}: {label} [km/h]',f(phase(aa,bend,key)),f(phase(bb,bend,key))))
        for label,key in [('Max na zawartej prostej, okr.7–8 [km/h]','FlyingContainedStraightKph'),
                          ('Max na prostej, cały zapis, tylko zawarte próbki [km/h]','MaximumContainedStraightKph'),
                          ('Max na prostej, okr.7–8, także poza torem [km/h]','FlyingAnyStraightKph'),
                          ('Średni |kąt uślizgu|, okr.7–8 [°]','MeanSlideDegrees'),('Max |kąt uślizgu|, okr.7–8 [°]','MaximumSlideDegrees'),
                          ('Rzeczywista droga / okrążenie [m]','DistancePerLap'),('Czas / okrążenie [s]','TimingSeconds'),
                          ('Max błąd boczny, okr.7–8 [m]','MaximumLateral'),('RMS błąd boczny [m]','RmsLateral'),
                          ('RMS / kierunek prędkości do stycznej [°]','RmsVelocityHeadingDegrees'),
                          ('Max / kierunek prędkości do stycznej [°]','MaximumVelocityHeadingDegrees'),
                          ('Min prześwit kreda, cały przejazd [m]','MinimumInner'),('Min prześwit banda, cały przejazd [m]','MinimumOuter'),
                          ('Dolna granica między próbkami, kreda [m]','ConservativeInner'),('Dolna granica między próbkami, banda [m]','ConservativeOuter'),
                          ('Rozrzut ostatnich 3 czasów max/min−1 [%]','Last3SpreadPercent')]:rows.append((label,cell(aa,key),cell(bb,key)))
        rows.extend([('8 okrążeń referencyjnych','TAK','TAK'),('Pełne bezpieczeństwo','NIE — rzeczywisty wyjazd','NIE — rzeczywisty wyjazd'),
            ('Okresowość ≤0,5%', 'TAK' if aa['Last3SpreadPercent']<=.5 else 'NIE','TAK' if bb['Last3SpreadPercent']<=.5 else 'NIE'),
            ('Stabilna bezpieczna jazda przez 8 okrążeń','NIE','NIE'),('Walidacja / ranking','ODRZUCONY','ODRZUCONY')])
        return '| Parametr | A INNER CONSTANT | B NATURAL RACING LINE |\n|---|---:|---:|\n'+'\n'.join('| '+' | '.join(v.replace('|','\\|') for v in r)+' |' for r in rows)
    pairrows='\n'.join(f"| {p['Engine']} | {p['InnerSeconds']:.6f} | {p['NaturalSeconds']:.6f} | {p['DeltaSeconds']:+.6f} | {p['DeltaPercent']:+.3f}% | {p['DistanceCostSeconds']:+.6f} | {p['SpeedEffectSeconds']:+.6f} |" for p in a['Pairs'])
    prefixes=load(DATA/'fixed-state-probes.json')['Prefixes']
    eventrows=[]
    for c,p in zip(a['Cases'],prefixes):
        path=p['FirstUnreachablePath'];controller=p['FirstUnreachableController']
        eventrows.append(f"| {c['Name']} | {f(controller['Time'])} | {f(path['Time']) if path else 'brak do progu tracking'} | {f(c['Events']['TrackingAbove1m']['Time'])} | {f(c['Events']['PathAboveTotalBudget']['Time'])} | {f(c['Events']['OuterCrossed']['Time'])} |")
    geometryrows='\n'.join(f"| {g['Name']} | {g['LengthMeters']:.6f} | {g['MinimumRadiusMeters']:.6f} | {g['MaximumAbsoluteCurvature']:.6f} | {3.6*g['MinimumTractionSpeed']:.3f} | {g['ConservativeInnerCircle']:.6f} | {g['ConservativeOuterCircle']:.6f} |" for g in a['Geometry'])
    text=f'''# Natural Racing Line vs INNER — jedna zamrożona hipoteza geometryczna

Status: **PROVISIONAL, eksperyment diagnostyczny. Żaden przejazd nie kwalifikuje się do rankingu.**
Zweryfikowana baza: Draft #109, `a5164d9daddb091eb86a2809792703941069fd56`, gałąź `experiment/full-contact-yaw-feedforward`. Bez merge, bez zmian `src/CoreSim`.

## Odpowiedź czasowa

Power4: B jest diagnostycznie **0,282433 s / 1,870% wolniejsza**. Power3: B jest diagnostycznie **0,468097 s / 2,997% szybsza**. **Nie ma zwycięzcy:** wszystkie cztery zapisy przekraczają fizyczną bandę, nie spełniają trackingu; Power4 dodatkowo nie jest okresowe. Krótszy czas Power3/B wynika z krótszej rzeczywistej pętli poza torem, a nie z większej średniej prędkości. Nie jest to przewaga poprawnie wykonanej linii B.

## Zamrożenie i uczciwość pary

[freeze.json](natural-racing-line-vs-inner/freeze.json) powstał przed pomiarem. A to dokładny `AuthoredRacingPath.Crossover(false)` z #106: także eksport geometrii jest identyczny bajtowo. B wykorzystuje ten sam konstruktor okresowej septic Hermite XY: wspólne P/P′/P″/P‴ w węzłach co ≤1 m, te same próbki źródłowe ±0,25/0,5 m. Produkcyjna geometria Egg Mild / progressive-b pozostaje identyczna.

Jedyna nowa hipoteza: outside offset **8 m**, apex **1,5 m**, rozpoczęcie zmiany **25 m przed wejściem**, apex **58% łuku**, outside **20 m za wyjściem**. Offset jest mierzony od istniejącej referencji, nie od fizycznej kredy. Dla każdego półokrążenia, z wejściem e=35 m i długością łuku b=97,3893749714 m: początek e−25, apex e+0,58b, koniec e+b+20. Źródło offsetu przechodzi 8→1,5→8 przez `S(u)=35u⁴−84u⁵+70u⁶−20u⁷`, potem utrzymuje 8 do kolejnego początku. Pełna krzywa XY, a nie sam offset, jest C3. Parametrów nie zmieniano po odczytaniu czasów. W dostępnym załączniku był opis rysunku, bez obrazu; B realizuje opis, nie digitalizację rysunku ani dowód realizmu.

**C3 i bezpieczny obrys nie oznaczają łagodnego profilu krzywizny.** B dziedziczy od źródłowej referencji wyraźne ramiona krzywizny przy wejściu/wyjściu: promień planu przy s≈35/38/40 m to około 142/46/28 m. Następnie promień ponownie rośnie przed apexem. Nie jest to monotoniczne zmniejszanie promienia aż do apexu; ta konkretna, nieoptymalizowana hipoteza ma ten istotny limit. Wczesna zmiana offsetu nie usuwa ramienia krzywizny XY.

Power4 ma dokładnie scale4 / ratio0,7 z #106; Power3 scale3 / ratio0,8. Drive=1 we wszystkich zaakceptowanych zapisach, Grip1/ruts0/moisture0,5, masa147 kg, krok0,01 s z odziedziczonymi krótszymi granicami. Ta sama opona, tylne koło, wheelspin, silnik, gains, yaw i bezpieczeństwo. #108 inverse **OFF**, #109 full yaw **OFF**; istniejące guidance/preview #106 pozostają w obu liniach takie same. Nie ma liftu, hamulca ani limitera prędkości.

Wspólny start: referencja32 m, heading0, v=(18,0) m/s, ω=18/0,33 rad/s; XY i target odpowiadają danej linii. B zaczyna na offset≈7,411845 m. Jego styczna wynosi wtedy **4,887°**, więc wspólny heading i prędkość nie są styczne do B. To jawny stan początkowy, nie korekta po rozpoczęciu jazdy. Cały transient wchodzi do audytu bezpieczeństwa; czas/tracking dotyczą ostatnich dwóch okrążeń.

[reuse-verification.json](natural-racing-line-vs-inner/reuse-verification.json): pełny snapshot początkowy A, w tym koło, jest dokładnie zgodny z #106 również przy zwykłej inicjalizacji rolling użytej dla B; 51 natywnych próbek pierwszych0,5 s obu silników zgadza się dokładnie dla XY/v/heading/Fx/Fy. Parametry są zgodne z zamrożeniem #106. Dlatego nie powtarzano ośmiookrążeniowego INNER. Dwa nowe główne światy: B/Power4 i — po odrzuceniu obu Power4 — B/Power3. [Gate Power3](natural-racing-line-vs-inner/power4-gate.json). Bez macierzy historycznej i optymalizacji.

## Kontrola geometrii przed fizyką

| Plan | Długość [m] | Min R XY [m] | Max abs κ [1/m] | Najniższa v z całego budżetu [km/h] | Dolna granica kreda [m] | Dolna granica banda [m] |
|---|---:|---:|---:|---:|---:|---:|
{geometryrows}

B dodaje **16,740490 m / 5,150%** drogi planowanej, ale minimalny promień zwiększa tylko o **0,705046 m / 2,768%**. Obie geometrie przechodzą test pełnego motocykla przy dowolnym headingu: konserwatywny okrąg obrysu1,05 m przeciw całym krawędziom i zapas pomiędzy próbkami geometrii. Niezależny test sprawdza kapsuły w ośmiu orientacjach. To certyfikat **planu**, osobny od przejazdu.

Przy budżecie≈14,546 m/s² stała prędkość110/120 km/h wymaga R≥64,19/76,38 m. Sam punkt wejścia B (R≈142 m) tego nie wyklucza, ale dalsze R≈26–28 m wymaga spadku do około70–73 km/h nawet przy wykorzystaniu całego budżetu na normalną. Jest to warunek konieczny, nie prognoza zachowania aktualnego kontaktu pod pełnym gazem.

## Pełne porównanie A/B

Wszystkie wartości czasowe poniżej są **diagnostyczne**. Czas i droga to średnia okrążeń7–8, od przecięcia referencji32+6L do32+8L. Droga to całka po rzeczywistych natywnych odcinkach XY; końce okna są interpolowanymi przecięciami, nie dodatkową symulacją. Fazy to średnia dwóch ostatnich przejść danego łuku; Power4 nie jest okresowe, więc pojedyncze przejścia pozostają w [phases.csv](natural-racing-line-vs-inner/phases.csv). Dla uślizgu podano średnią czasową wartości bezwzględnej, nie znoszącą się średnią signed.

### Power4

{table('power4')}

### Power3

{table('power3')}

„—” dla prostej oznacza brak próbek z całym motocyklem zachowującym wymagane prześwity w końcowym oknie. Maksimum z całego zapisu może pochodzić z chwilowego ponownego znalezienia się na torze po wcześniejszym wyjeździe; nie jest certyfikatem bezpiecznego dojazdu. Prześwity używają końca native step i odpowiadającej mu prędkości, nie pozycji z początku tego kroku. Ujemne minimum bandy oznacza **zaobserwowane przecięcie**, a nie tylko brak certyfikatu między próbkami. Osiem przecięć licznika referencyjnego nie oznacza ośmiu prawidłowych okrążeń.

Kryteria są identyczne z #106: cały zapis inner≥0,05/outer≥0,5 m oraz dolne granice między krokami inner≥0/outer≥0,5 m; ostatnie2 RMS/max boczny≤0,5/1 m i RMS/max błędu kierunku prędkości≤3/10°; ostatnie3 czasy max/min≤1,005. Power3 jest okresowe **poza torem**, co nie wystarcza do walidacji.

## Droga kontra prędkość

Dla rzeczywistych Lₐ,Tₐ,Lᵦ,Tᵦ oraz średnich uₐ=Lₐ/Tₐ, uᵦ=Lᵦ/Tᵦ użyto dokładnej tożsamości: `ΔT=(Lᵦ−Lₐ)/uₐ + Lᵦ·(1/uᵦ−1/uₐ)`. Pierwszy składnik zmienia drogę przy prędkości A, drugi prędkość na drodze B. Jest to jawna dekompozycja kinematyczna zależna od tej kolejności, nie kontrfaktyczna symulacja ani dowód przyczynowy.

| Silnik | T INNER [s] | T NATURAL [s] | B−A [s] | B−A [%] | Składnik drogi [s] | Składnik prędkości [s] |
|---|---:|---:|---:|---:|---:|---:|
{pairrows}

Power4 odzyskuje tylko0,132247 s z kosztu0,414680 s większej faktycznej drogi. Power3 B jedzie faktycznie **15,445268 m krócej** na pętli poza torem; mniejsza średnia prędkość kosztuje0,099769 s. Plan B był dłuższy, lecz nie został wykonany. Nie wolno interpretować tego jako korzystnej realizacji outside–inside–outside.

## Pierwsze ograniczenie: oddzielne mechanizmy i znaczniki czasu

[fixed-state-probes.json](natural-racing-line-vs-inner/fixed-state-probes.json) używa istniejącego solvera kontaktu #108 **wyłącznie offline**, z zapisanym v, ω, momentem silnika, budżetem i krokiem; zero Body Steps i Wheel Commits, kontroler OFF. Przeszukuje istniejący zakres β±45° i jego numeryczne ekstrema. „Nieosiągalne” dotyczy tego zamrożonego kontaktu i zakresu, z tolerancją0,25 N; rozwiązanie kontaktu nie dowodzi osiągalności kąta przez yaw ani wykonalności całego przejazdu.

| Zapis | Pierwszy request preview+PD poza obwiednią kontaktu [s] | Pierwsze v²κ planu poza obwiednią kontaktu [s] | Błąd boczny>1 m [s] | v² abs κ ponad cały budżet [s] | Pierwszy rzeczywisty wyjazd bandą [s] |
|---|---:|---:|---:|---:|---:|
{chr(10).join(eventrows)}

**Pierwsze potwierdzone ograniczenie B jest w torze sterowania/osiągalności żądanej siły normalnej, zanim nastąpi przekroczenie1 m:** Power4 przy0,18 s żąda2010,819 N wobec maksimum1997,765 N; Power3 przy0,20 s żąda2001,566 N wobec1993,668 N. Preview obejmuje rosnącą krzywiznę, a PD koryguje już początkowy błąd. To nie jest geometryczne przecięcie planu z bandą.

Przy pierwszym błędzie>1 m B/Power4 (1,20 s) plan potrzebuje1954,943 N i ma kontaktowy root β≈22,185°, ale legacy target≈19,423° daje tylko1907,8 N; aktualny kąt≈18,410° daje1884,1 N. Cały request sterownika2144,887 N jest wtedy poza kontaktowym maksimum1989,807 N. B/Power3 przy1,98 s ma wykonalne zarówno v²κ=1623,895 N, jak i request1778,037 N; root requestu≈11,803°, lecz legacy target≈10,181° daje1683,0 N. Jest więc konkretny dowód niedopasowania dawnego odwzorowania request→β do pełnego kontaktu z kołem; nie jest to dowód braku siły opony dla każdej chwilowej krzywizny B. Ten mechanizm jest zgodny z historycznym #108; nie włączano jego poprawki.

**Następnie pojawia się niezależne, rozstrzygające ograniczenie szybkości/krzywizny:** B/Power4 przekracza cały budżet przy3,14 s (14,591>14,546 m/s²), B/Power3 przy4,32 s (14,582>14,546). Żaden lepszy yaw ani inverse nie dostarczy v²κ większego niż cały budżet. B/Power4 faktycznie przekracza bandę przy5,29 s, B/Power3 przy9,67 s. Stan ma pamięć: siła chwilowo wystarczająca na samym wyjściu nie usuwa wcześniej zgromadzonego błędu pozycji i kierunku.

Nie rozstrzygamy ilościowo udziału wspólnego startu z różnicą stycznej4,887° versus samego preview w najwcześniejszym transiencie. Dokładny test rozstrzygający: **jeden krótki przejazd B/Power3 od referencji4 m** (przed turn-in10 m, styczna/heading/velocity0, identyczne18 m/s i rolling wheel), Drive1 i wszystkie obecne parametry; porównanie zdarzeń względem referencji35–66 m z zapisanym B. Zero zmiany regulatora i opony. Trzeba zachować różnicę prędkości na wejściu i równolegle przeliczyć obwiednię kontaktu, zamiast przypisać jej efekt inicjalizacji. Tego dodatkowego świata tutaj nie uruchamiano, ponieważ nie zmieni potwierdzonego późniejszego przekroczenia całego budżetu ani odrzucenia rankingu.

Siły spełniają native bilans impulsu: max residual<0,00113 Ns, momentu<0,000013 Nms; utilization<0,919, yaw guard excess=0, bilans energii wszystkich zapisów |residual|<1,15 J. Brak teleportowania, korekt prędkości, dodatkowych sił lub arbitralnych kar. Niższe od1 utilization samo nie dowodzi dostępności większej **normalnej**: znaczenie ma cały kontakt, kierunek siły, Fx i stan koła.

## Siedem odpowiedzi i następny krok

1. **Bezpieczne110–120 km/h? Nie wykazano.** B osiąga takie prędkości w zapisie, lecz cały przejazd jest odrzucony. Sam punkt wejścia ma duży promień; dalsze ramiona krzywizny nie są pokonywane zgodnie z planem. Pierwsze wejście z rozbiegu18 m/s ma tylko około67 km/h.
2. **Naturalna strata prędkości mimo pełnego gazu? Tak.** Na końcowych łukach Power3/B prędkość spada około109,309→88,961 km/h; Power4/B także zwalnia na części łuków. To rzeczywisty zapis Box2D, jednak utrata prędkości jest zbyt mała/późna dla wykonania planu.
3. **Płynne wyjście szerzej? Plan tak; ruch nie realizuje bezpiecznego wyjścia.** Offset B na końcu łuku Power3 wynosi około29,07 m wobec zaplanowanych≈6,92 m. To ucieczka poza bandę, nie poprawne naturalne otwieranie.
4. **Większy rzeczywisty promień? Nie ma uniwersalnego zysku.** W końcowym Power3 B ma min R ruchu≈46,01 m wobec≈47,05 m INNER; przy samym wejściu≈76,20 m wobec≈256,45 m. Promienie te opisują odrzucone pętle. Planowane minR B jest tylko nieznacznie większe niż A. Offset nie jest promieniem; κ liczono z XY i zmiany kierunku prędkości.
5. **Która szybsza? Brak ważnego rankingu.** Diagnostyczne delty są na początku i w tabeli; żadna nie jest czasem bezpiecznie wykonanej strategii.
6. **Czy większa prędkość spłaca drogę? Power4 nie; Power3 pozorny zysk pochodzi z krótszej faktycznej pętli poza torem.** Brak dowodu przewagi poprawnej linii B.
7. **Czy trzeba zmienić tor? Te dane tego nie dowodzą.** Oba plany mieszczą pełny motocykl. Ta linia B i obecny request/preview nie tworzą wykonalnego full-gas przejazdu110–120; nie dowodzi to niemożliwości każdej innej geometrii XY na tym torze. Brak dowodu błędnego modelu opony czy błędnej prędkości Box2D; jest dowód niedopasowania bieżących żądań, prędkości i krzywizny.

Rekomendacja: następny **mały PR diagnostyczny start/preview** z opisanym jednym krótkim testem B/Power3 przed turn-in oraz kontrolą ramienia κ i kontaktowego requestu. Dopiero rozdzielenie tego transienta uzasadni zmianę konstrukcji linii lub sterownika. Bez poszerzania toru, zwiększania Grip i optymalizatora. Nie rekomenduję traktować #108/#109 jako automatycznej naprawy: w tym zapisie występuje również żądanie ponad całkowity budżet.

## Wykresy i odtworzenie

![Planowane C3 i rzeczywisty Box2D](natural-racing-line-vs-inner/xy.png)

![Prędkość, krzywizna i błąd](natural-racing-line-vs-inner/speed-curvature-error.png)

Przerywane linie to plan, ciągłe to native Box2D. Szare obszary oznaczają referencyjne łuki. Pełne XY pokazuje również wszystkie wyjazdy poza tor; niczego nie docięto do bandy. SVG są obok PNG. Dane: [analysis.json](natural-racing-line-vs-inner/analysis.json), [surowe zapisy](natural-racing-line-vs-inner/), [manifest](natural-racing-line-vs-inner/manifest.json). Źródła A pozostają w #106; hashe konkretnych wejść są w analysis. Zachowano **881/881** historycznych hashy, w tym fingerprinty; nie uruchamiano dużych audytów historycznych.

Windows / PowerShell, z katalogu repozytorium, zawsze świeży katalog wyniku (eksporter odmawia nadpisywania):

```powershell
dotnet restore SpeedwayManager.sln
dotnet build SpeedwayManager.sln -c Release --no-restore
$outDir = 'results/natural-line-recapture'
foreach ($stage in @('freeze','geometry','verify','power4')) {{
  dotnet run --no-build -c Release --project tools/shared-world-speedway -- natural-line $outDir $stage
}}
python tools/shared-world-speedway/natural-racing-line-report.py --data $outDir gate
# Tylko jeśli gate.BothRejected=true:
dotnet run --no-build -c Release --project tools/shared-world-speedway -- natural-line $outDir power3
python tools/shared-world-speedway/natural-racing-line-report.py --data $outDir
dotnet run --no-build -c Release --project tools/shared-world-speedway -- natural-line $outDir probes
# Opcjonalnie pip install matplotlib, wyłącznie do odtworzenia wykresów:
python tools/shared-world-speedway/natural-racing-line-report.py --data $outDir --plots
```

Lokalna walidacja: Release `--warnaserror`, **10/10 C#**, **117/117 Python analizy**, **7/7 CI policy**, **13/13 Node**. Testy C3/pełnego obrysu/initial+native-prefix/original-force invariants i bramek rankingu; [validation.json](natural-racing-line-vs-inner/validation.json). Standardowe **Windows Draft CI** dla końcowego HEAD: wynik i link w opisie Draft PR. Nie wykonano merge.
'''
    (ROOT/'docs/experiments/natural-racing-line-vs-inner.md').write_text(text,encoding='utf-8',newline='\n')

if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('stage',nargs='?',choices=['analyse','gate'],default='analyse')
    parser.add_argument('--data',type=Path,default=DATA)
    parser.add_argument('--plots',action='store_true');parser.add_argument('--report',action='store_true')
    args=parser.parse_args();DATA=args.data.resolve()
    if args.stage == 'gate':
        cases=[analyse_case('power4',n)[0] for n in [False,True]]
        save('power4-gate.json',{'BothRejected':power3_allowed(cases),'Cases':[{'Name':c['Name'],'Rejections':c['Rejections']} for c in cases]})
        print(json.dumps(load(DATA/'power4-gate.json'),indent=2))
    else:
        engines=['power4']+(['power3'] if (DATA/'power3-natural.json').exists() else [])
        a,cache=analyse(engines)
        if args.plots:plots(a,cache)
        if args.report:write_report(a)
        manifest()
        print(json.dumps({'Pairs':a['Pairs'],'HistoricalHashes':sum(a['HistoricalHashes'].values()),'Cases':[{'Name':c['Name'],'Rejections':c['Rejections'],'Events':c['Events']} for c in a['Cases']]},indent=2))
