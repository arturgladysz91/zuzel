"""Read #106 native records and canonical #107 probes. No tyre law or motion integration."""
import bisect
import csv
import gzip
import hashlib
import json
import math
from pathlib import Path
import statistics
import sys
import xml.etree.ElementTree as ET

ROOT=Path(__file__).resolve().parents[2]
DATA=ROOT/'docs/experiments/full-throttle-slide-grip-audit'
SOURCE=ROOT/'docs/experiments/speed-grip-line-crossover'
LAP=318.77874994277954
ENTRY,EXIT=35,132.38937497138977
HALF=LAP/2
load=lambda path:json.loads(path.read_text(encoding='utf-8-sig'))

def read(path):
    with (gzip.open(path,'rt',encoding='utf8') if path.suffix=='.gz' else path.open(encoding='utf8')) as f:
        return [{k:float(v) for k,v in row.items()} for row in csv.DictReader(f)]

def save(name,value):
    (DATA/name).write_text(json.dumps(value,ensure_ascii=False,indent=2,allow_nan=False)+'\n',encoding='utf8')

def weighted(rows,key):
    return sum(r[key]*r['dt'] for r in rows)/sum(r['dt'] for r in rows)

def turn(s):
    local=s%LAP
    for n in (0,1):
        if ENTRY+n*HALF<=local<EXIT+n*HALF:return n+1,(local-ENTRY-n*HALF)/(EXIT-ENTRY)
    return 0,0

def at_s(rows,s,key):
    values=[r['s'] for r in rows];i=max(0,min(len(rows)-2,bisect.bisect_right(values,s)-1));a,b=rows[i:i+2]
    fraction=(s-a['s'])/(b['s']-a['s']);return a[key]+fraction*(b[key]-a[key])

def tracking_pass(stats):
    return stats['RmsError']<=.5 and stats['MaxError']<=1 and stats['RmsHeadingDegrees']<=3 and stats['MaxHeadingDegrees']<=10

def periodic_motion(complete,boundary_certified,last3):
    return complete and boundary_certified and len(last3)==3 and min(last3)>0 and max(last3)/min(last3)<=1.005

def analyze():
    extraction=load(DATA/'extraction.json');cache={};cases=[];phases=[]
    old={r['Name']:r for r in load(SOURCE/'analysis.json')['Cases']}
    for item in extraction:
        name=item['Name'];native=read(SOURCE/(name+'-tracking.csv.gz'));forces=read(SOURCE/(name+'-forces.csv.gz'));derived=read(DATA/(name+'-decomposition.csv.gz'))
        summary=load(SOURCE/(name+'.json'));assert len(derived)==item['UsedRows'];assert len(native)==len(forces)==item['NativeRows']
        rows=[]
        for n,(r,f,d) in enumerate(zip(native,forces,derived)):
            assert r['t']==f['t']==d['t']
            h=r['dt'];beta=math.degrees(math.atan2(math.sin(r['body_heading']-r['velocity_heading']),math.cos(r['body_heading']-r['velocity_heading'])))
            rows.append(dict(r,**{k:v for k,v in d.items() if k!='t'},engine_power=f['engine_work']/h,
                longitudinal_power=d['longitudinal_loss_work']/h,lateral_power=d['lateral_loss_work']/h,
                bearing_power=f['bearing_loss']/h,resistance_power=f['resistance_loss']/h,
                body_power=d['body_delta']/h,wheel_power=d['wheel_delta']/h,yaw_power=d['yaw_work']/h,
                beta=beta,normal_acceleration=f['normal_force']/f['mass'],required_normal=r['required_path_accel'],
                command_curvature=f['feed_forward_curvature'],accel_budget=r['budget']/f['mass'],
                target_beta=beta+math.degrees(r['yaw_error'])))
        crosses=summary['CrossingSeconds'];last2start=crosses[-3] if len(crosses)==8 and item['FirstPhysicalViolation'] is None else 0
        stop=crosses[-1] if item['FirstPhysicalViolation'] is None else item['FirstPhysicalViolation']['End']
        window=[r for r in rows if last2start<=r['t']<stop];bends=[r for r in window if turn(r['s'])[0]]
        core=[r for r in bends if .05<=turn(r['s'])[1]<=.95]
        duration=sum(r['dt'] for r in bends)
        def fraction(values,predicate):return 100*sum(r['dt'] for r in values if predicate(r))/sum(r['dt'] for r in values)
        mean=lambda key:weighted(bends,key)
        row={'Name':name,'Grip':item['Grip'],'NativeRows':len(rows),'PhysicalViolation':item['FirstPhysicalViolation'],
             'BoundaryCertified':summary['BoundarySafe'],'Rankable106':old[name]['Rankable'],
             'Scope':'final-two-laps' if last2start else 'prefix-through-first-crossing',
             'MaxSpeedKph':max(r['speed'] for r in window)*3.6,'MaxSlideDegrees':max(abs(r['beta']) for r in bends),'MaxWheelspin':max(r['wheelspin'] for r in bends),
             'MeanSlideDegrees':mean('beta'),'MinimumBetaDegrees':min(r['beta'] for r in bends),
             'Slide3Percent':fraction(bends,lambda r:abs(r['beta'])>=3),'CoreSlide3Percent':fraction(core,lambda r:abs(r['beta'])>=3),
             'DeceleratingPercent':fraction(bends,lambda r:r['a_tangent']<0),
             'PathAboveScalarBudgetPercent':fraction(bends,lambda r:abs(r['required_normal'])>r['accel_budget']),
             'SteeringClippedPercent':fraction(bends,lambda r:abs(r['steering_demand'])>=.95*r['accel_budget']-1e-5),
             'MeanEnginePower':mean('engine_power'),'MeanLateralPower':mean('lateral_power'),'MeanLongitudinalPower':mean('longitudinal_power'),
             'MeanBodyPower':mean('body_power'),'MeanWheelPower':mean('wheel_power'),'MeanTangentialAcceleration':mean('a_tangent'),
             'MeanFrontFy':mean('front_fy'),'MeanRearFy':mean('rear_fy'),'MeanFx':mean('fx'),
             'MeanActualCurvature':mean('actual_curvature'),'MeanPathCurvature':mean('path_curvature'),
             'MinimumInner':min(r['inner'] for r in rows),'MinimumOuter':min(r['outer'] for r in rows),
             'RmsError':math.sqrt(sum(r['lateral_error']**2*r['dt'] for r in window)/sum(r['dt'] for r in window)),
             'MaxError':max(abs(r['lateral_error']) for r in window),
             'RmsHeadingDegrees':math.degrees(math.sqrt(sum(r['velocity_heading_error']**2*r['dt'] for r in window)/sum(r['dt'] for r in window))),
             'MaxHeadingDegrees':math.degrees(max(abs(r['velocity_heading_error']) for r in window)),
             'MaximumAtMeasurementDifference':max(abs(r['a_tangent']-r['a_measured']) for r in window if r['dt']>=.004),
             'Energy':item['Energy'],'OriginalFingerprint':item['SourceFingerprint']}
        row['FiniteSafePeriodicOrbit']=periodic_motion(summary['Result']['Completed'] and item['FirstPhysicalViolation'] is None,summary['BoundarySafe'],summary['LapSeconds'][-3:])
        row['TracksAuthoredPath']=tracking_pass(row)
        native_static=summary['Initial']['Riders'][0];assert native_static['Drive']==1 and not summary['Result']['Decisions'] and not summary['Result']['Contacts']
        assert all(r['force_replay_error']==0 for r in rows)
        assert row['Rankable106'] is False
        if item['FirstPhysicalViolation'] is None:
            for cycle in range(int(window[0]['s']//LAP)-1,int(window[-1]['s']//LAP)+1):
                for bend in (0,1):
                    start=cycle*LAP+ENTRY+bend*HALF;end=cycle*LAP+EXIT+bend*HALF
                    if start<window[0]['s'] or end>window[-1]['s']:continue
                    segment=[r for r in window if start<=r['s']<end]
                    if not segment:continue
                    phases.append({'Name':name,'Cycle':cycle,'Bend':bend+1,'EntrySpeed':at_s(rows,start,'speed'),'MiddleSpeed':at_s(rows,(start+end)/2,'speed'),
                        'ExitSpeed':at_s(rows,end,'speed'),'MaximumNextStraight':max(r['speed'] for r in rows if end<=r['s']<=end+62) if end+62<=rows[-1]['s'] else None,
                        'EntryBeta':at_s(rows,start,'beta'),'MiddleBeta':at_s(rows,(start+end)/2,'beta'),'ExitBeta':at_s(rows,end,'beta'),
                        'MeanEngineKW':weighted(segment,'engine_power')/1000,'MeanLateralKW':weighted(segment,'lateral_power')/1000,
                        'MeanLongitudinalKW':weighted(segment,'longitudinal_power')/1000,'MeanBodyKW':weighted(segment,'body_power')/1000,
                        'DeltaBodyJ':sum(r['body_delta'] for r in segment),'DeltaWheelJ':sum(r['wheel_delta'] for r in segment),
                        'LateralLossJ':sum(r['lateral_loss_work'] for r in segment),'LongitudinalLossJ':sum(r['longitudinal_loss_work'] for r in segment),
                        'Slide3Percent':fraction(segment,lambda r:abs(r['beta'])>=3)})
        cases.append(row);cache[name]=rows
    # Finite 8-lap convergence, with no resets of body or omega.
    perturbations=[];base_summary=load(SOURCE/'baseline-inner.json');base_rows=cache['baseline-inner']
    for name in ['initial-minus5','repeat18','initial-plus5']:
        r=load(DATA/(name+'.json'));raw=read(DATA/(name+'-tracking.csv.gz'));laps=r['LapSeconds'][-3:]
        phase=[]
        for j in range(600):
            s=32+7*LAP+j*LAP/600
            if s>min(raw[-1]['s'],base_rows[-1]['s']):continue
            phase.append((math.hypot(at_s(raw,s,'x')-at_s(base_rows,s,'x'),at_s(raw,s,'y')-at_s(base_rows,s,'y')),abs(at_s(raw,s,'speed')-at_s(base_rows,s,'speed'))))
        perturbations.append({'Name':name,'InitialSpeed':r['Initial']['Riders'][0]['Speed'],'Fingerprint':r['Result']['Fingerprint'],
            'BoundarySafe':r['BoundarySafe'],'Last3Seconds':laps,'PeriodRatio':max(laps)/min(laps),
            'MaximumLastLapPositionDifferenceMeters':max(p[0] for p in phase),'MaximumLastLapSpeedDifferenceMps':max(p[1] for p in phase),
            'EnergyResidual':r['Energy']['BalanceResidualJoules']})
        assert r['BoundarySafe'] and max(laps)/min(laps)<1.005 and phase
        if name=='repeat18':assert r['Result']['Fingerprint']==base_summary['Result']['Fingerprint']
    history={}
    for base in (ROOT/'docs/experiments').iterdir():
        if base==DATA or not (base/'manifest.json').exists():continue
        manifest=load(base/'manifest.json')
        if not isinstance(manifest,dict) or not all(isinstance(v,str) for v in manifest.values()):continue
        for name,sha in manifest.items():assert hashlib.sha256((base/name).read_bytes()).hexdigest()==sha,base/name
        history[base.name]=len(manifest)
    counts=ET.parse(DATA/'targeted-release.trx').getroot().find('{*}ResultSummary/{*}Counters').attrib
    assert counts['failed']=='0' and int(counts['passed'])==21
    result={'Cases':cases,'Phases':phases,'Perturbations':perturbations,'HistoricalHashes':history,'TargetedTests':counts,
        'MainWorldsRerun':0,'AdditionalNativeWorlds':3,'OriginalEquationsChanged':False,'SourceHead':load(DATA/'freeze.json')['BaseHead']}
    save('analysis.json',result)
    for file,records in [('cases.csv',[{k:v for k,v in c.items() if not isinstance(v,(dict,list))} for c in cases]),('corner-phases.csv',phases)]:
        with (DATA/file).open('w',newline='',encoding='utf8') as f:
            w=csv.DictWriter(f,records[0].keys());w.writeheader();w.writerows(records)
    return result,cache

def plots(a,cache):
    import matplotlib
    matplotlib.use('Agg')
    import matplotlib.pyplot as plt
    plt.rcParams.update({'font.size':9,'axes.grid':True,'grid.alpha':.22,'svg.hashsalt':'slide107'})
    colors=['#134e79','#d15916','#288044','#8546a1']
    def finish(fig,name):
        fig.tight_layout();fig.savefig(DATA/(name+'.png'),dpi=160);fig.savefig(DATA/(name+'.svg'),metadata={'Date':None});plt.close(fig)
        vector=DATA/(name+'.svg');vector.write_text('\n'.join(line.rstrip() for line in vector.read_text(encoding='utf8').splitlines())+'\n',encoding='utf8',newline='\n')
    def bend_rows(name):
        rows=cache[name];s=6*LAP+ENTRY if not name.startswith('power') else ENTRY
        return [r for r in rows if s<=r['s']<=s+EXIT-ENTRY]
    fig,axes=plt.subplots(2,2,figsize=(12,7))
    for col,suffix in enumerate(['inner','wide']):
        for color,prefix in zip(colors,['baseline','grip85','grip70','grip55']):
            rows=bend_rows(prefix+'-'+suffix);x=[(r['s']%LAP-ENTRY)/(EXIT-ENTRY)*100 for r in rows]
            axes[0,col].plot(x,[r['speed']*3.6 for r in rows],color=color,label=prefix)
            axes[1,col].plot(x,[r['beta'] for r in rows],color=color)
        axes[0,col].set(title=suffix+' · actual final-two-lap bend',ylabel='Speed km/h');axes[0,col].legend()
        axes[1,col].set(xlabel='Reference bend progress %',ylabel='Body slide beta deg')
    finish(fig,'speed-and-slide')
    fig,axes=plt.subplots(3,1,figsize=(12,9),sharex=True)
    rows=bend_rows('baseline-inner');x=[(r['s']%LAP-ENTRY)/(EXIT-ENTRY)*100 for r in rows]
    for key,label in [('engine_power','Engine'),('lateral_power','Lateral slip loss'),('longitudinal_power','Longitudinal slip loss'),('resistance_power','External resistance loss')]:axes[0].plot(x,[r[key]/1000 for r in rows],label=label)
    axes[0].set(ylabel='kW',title='NATIVE baseline INNER · Drive1 · Grip1');axes[0].legend(ncol=2)
    for key,label in [('body_power','Body dK/dt'),('wheel_power','Rear wheel dK/dt'),('yaw_power','Yaw actuator work rate')]:axes[1].plot(x,[r[key]/1000 for r in rows],label=label)
    axes[1].axhline(0,color='gray',lw=.7);axes[1].set(ylabel='kW');axes[1].legend()
    axes[2].plot(x,[r['a_tangent'] for r in rows],label='Applied F dot travel / mass');axes[2].plot(x,[r['a_measured'] for r in rows],label='Native delta speed / dt',ls='--')
    axes[2].set(xlabel='Reference bend progress %',ylabel='Tangential m/s²');axes[2].legend();finish(fig,'power-and-energy')
    grid=load(DATA/'static-grid.json');steady=load(DATA/'steady-wheel.json');fig,axes=plt.subplots(2,2,figsize=(12,8))
    for color,grip in zip(colors,[1,.85,.7,.55]):
        values=[r['State'] for r in grid if abs(r['Grip']-grip)<1e-6 and abs(r['State']['Speed']-18)<1e-4 and abs(r['State']['BetaDegrees']-15)<1e-4]
        axes[0,0].plot([r['Kappa'] for r in values],[r['LongitudinalNewtons'] for r in values],label=f'Grip {grip}',color=color)
        axes[0,1].plot([r['Kappa'] for r in values],[r['RearFy'] for r in values],color=color)
        axes[0,1].plot([r['Kappa'] for r in values],[r['FrontFy'] for r in values],color=color,ls=':')
        values=[r['State'] for r in steady if abs(r['Grip']-grip)<1e-6 and abs(r['State']['Speed']-18)<1e-4]
        axes[1,0].plot([r['BetaDegrees'] for r in values],[r['TangentialAcceleration'] for r in values],label=f'Grip {grip}',color=color)
        axes[1,1].plot([r['BetaDegrees'] for r in values],[r['LateralLossWatts']/1000 for r in values],color=color)
    axes[0,0].set(title='STATIC original contact: v18 beta15°',xlabel='Driven-wheel kappa',ylabel='Rear Fx N');axes[0,0].legend()
    axes[0,1].set(title='Solid rear / dotted front',xlabel='Driven-wheel kappa',ylabel='Fy N')
    axes[1,0].axhline(0,color='gray');axes[1,0].set(title='STATIC steady wheel, full Drive (not body stability)',xlabel='Body beta deg',ylabel='Tangential m/s²')
    axes[1,1].set(title='STATIC steady-wheel lateral dissipation',xlabel='Body beta deg',ylabel='kW');finish(fig,'static-contact')
    fig,axes=plt.subplots(1,3,figsize=(15,5))
    for ax,name in zip(axes,['baseline-inner','baseline-wide','power3-inner']):
        suffix='wide-straight' if name.endswith('wide') else 'inner';plan=read(SOURCE/('crossover-'+suffix+'-geometry.csv'))
        ax.plot([r['x'] for r in plan],[r['y'] for r in plan],color='gray',ls='--',label='Frozen C3 plan')
        rows=cache[name];ax.plot([r['x'] for r in rows],[r['y'] for r in rows],label='Native actual, valid prefix')
        if name.startswith('power'):ax.scatter(rows[-1]['x'],rows[-1]['y'],marker='x',s=60,color='red',label='First crossing interval')
        ax.set(title=name,xlabel='x m',ylabel='y m');ax.axis('equal');ax.legend(fontsize=7)
    finish(fig,'actual-versus-intended')
    fig,axes=plt.subplots(2,1,figsize=(12,7),sharex=True)
    for name,style in [('baseline-inner','-'),('baseline-wide','--')]:
        rows=bend_rows(name);x=[(r['s']%LAP-ENTRY)/(EXIT-ENTRY)*100 for r in rows]
        axes[0].plot(x,[r['path_curvature'] for r in rows],label=name+' path',ls=style)
        axes[0].plot(x,[r['actual_curvature'] for r in rows],label=name+' actual',ls=style)
        axes[1].plot(x,[r['required_normal'] for r in rows],label=name+' required path',ls=style)
        axes[1].plot(x,[r['normal_acceleration'] for r in rows],label=name+' applied normal',ls=style)
    axes[0].set(ylabel='Curvature 1/m',title='NATIVE curvature and force · Grip1');axes[0].legend(ncol=2)
    axes[1].axhline(2138.2978515625/147,color='gray',ls=':',label='Scalar B/m (necessary bound)');axes[1].set(xlabel='Reference bend progress %',ylabel='Normal m/s²');axes[1].legend(ncol=2)
    finish(fig,'curvature-and-force')

def table(headers,rows):return '\n'.join(['|'+'|'.join(headers)+'|','|'+'---|'*len(headers)]+['|'+'|'.join(map(str,r))+'|' for r in rows])

def report(a):
    cases=a['Cases'];main=cases[:8];phases=a['Phases'];base=main[0]
    def values(name):return [r for r in phases if r['Name']==name]
    def mean(name,key):return statistics.mean(r[key] for r in values(name))
    speed_table=table(['Próba','entry / middle / exit km/h','max pełnej prostej km/h','β mean/max °','κ max','RMS/max błąd m'],
        [[r['Name'],' / '.join(f"{mean(r['Name'],k)*3.6:.2f}" for k in ['EntrySpeed','MiddleSpeed','ExitSpeed']),
          f"{max(p['MaximumNextStraight'] for p in values(r['Name']) if p['MaximumNextStraight'] is not None)*3.6:.2f}",
          f"{r['MeanSlideDegrees']:.2f}/{r['MaxSlideDegrees']:.2f}",f"{r['MaxWheelspin']:.4f}",f"{r['RmsError']:.3f}/{r['MaxError']:.3f}"] for r in main])
    power_table=table(['Próba','Pengine kW','Llat kW','Llong kW','dKbody/dt kW','a travel m/s²','czas hamowania %','Fx / Fy front / rear N'],
        [[r['Name'],*[f"{r[k]/1000:.3f}" for k in ['MeanEnginePower','MeanLateralPower','MeanLongitudinalPower','MeanBodyPower']],
          f"{r['MeanTangentialAcceleration']:.3f}",f"{r['DeceleratingPercent']:.2f}",f"{r['MeanFx']:.1f}/{r['MeanFrontFy']:.1f}/{r['MeanRearFy']:.1f}"] for r in main])
    stability_table=table(['Próba','bezpieczny okresowy ruch 8lap','β≥3° czas łuku %','β≥3° core90% %','path demand>B %','tracking/ranking'],
        [[r['Name'],'TAK' if r['FiniteSafePeriodicOrbit'] else 'brak certyfikatu',f"{r['Slide3Percent']:.2f}",f"{r['CoreSlide3Percent']:.2f}",f"{r['PathAboveScalarBudgetPercent']:.2f}",'FAIL / niedopuszczony'] for r in main])
    perturb_table=table(['v początkowe m/s','ostatnie 3 lap max/min','max różnica XY ostatniego lap m','max Δv m/s','safety'],
        [[r['InitialSpeed'],f"{r['PeriodRatio']:.9f}",f"{r['MaximumLastLapPositionDifferenceMeters']:.7f}",f"{r['MaximumLastLapSpeedDifferenceMps']:.7f}",r['BoundarySafe']] for r in a['Perturbations']])
    steady=load(DATA/'steady-wheel.json')
    static_table=table(['β °','κ steady wheel','Llat kW','Llong kW','a travel m/s²','a normal m/s²'],
        [[f"{r['State']['BetaDegrees']:.0f}",f"{r['State']['Kappa']:.4f}",f"{r['State']['LateralLossWatts']/1000:.3f}",f"{r['State']['LongitudinalLossWatts']/1000:.3f}",
          f"{r['State']['TangentialAcceleration']:.3f}",f"{r['State']['NormalAcceleration']:.3f}"] for r in steady if r['Grip']==1 and abs(r['State']['Speed']-18)<.001 and r['State']['BetaDegrees']<31])
    inverse=load(DATA/'controller-inversion-probes.json')
    inverse_table=table(['żądanie/B','target β °','żądane a normal','uzyskane a normal','uzyskane/żądane'],
        [[r['Fraction'],f"{r['TargetBetaDegrees']:.3f}",f"{r['RequiredNormalAcceleration']:.4f}",f"{r['ActualNormalAcceleration']:.4f}",f"{r['ActualToRequested']:.5f}"] for r in inverse if r['Grip']==1 and r['Speed']==18])
    e=base['Energy'];ledger=table(['8lap INNER','kJ'],[[k,f"{e[key]/1000:.6f}"] for k,key in [('engine','EngineWork'),('lateral loss','LateralLoss'),('longitudinal loss','LongitudinalLoss'),('external resistance work','ResistanceWork'),('bearing loss','BearingLoss'),('yaw actuator work','YawWork'),('body ΔK','BodyChange'),('wheel ΔK','WheelChange')]])
    max_res=max(abs(r['Energy']['Residual']) for r in cases)
    slip_counts=ET.parse(DATA/'slip-model-release.trx').getroot().find('{*}ResultSummary/{*}Counters').attrib
    assert slip_counts['failed']=='0' and int(slip_counts['passed'])==20
    content=f'''# Full Throttle Slide & Grip Audit (#107)

**A — wykonany audyt, bez zmiany fizyki.** Uślizg rzeczywiście rozprasza energię i może zmniejszać prędkość przy Drive1. Obecny model dopuszcza bezpieczny, okresowy ruch z uślizgiem w łuku, ale nie zapewnia dokładnego wykonania obu zamrożonych planów #106. Empiryczna wiarygodność opony/nawierzchni nadal nie jest rozstrzygnięta.

Baza: ostatni właściwy Draft #106 `experiment/speed-grip-line-crossover`, HEAD **{a['SourceHead']}**, sprawdzony lokalnie i zdalnie. Jeden nowy Draft, bez merge. Egg Mild/progressive-b, C3 A INNER/B WIDE STRAIGHT, gains, yaw, engine baseline, wymiary, masa, koło i opona nie zmieniają się. Produkcyjne `src/CoreSim` bez zmian. Nie dodano hamowania, roll-off, speed clampu, ściany ani korekty stanu.

## Źródła i koszt eksperymentu

Wszystkie8 wymaganych przypadków istnieje w #106. Zachowano i wykorzystano ich pełne native `*-tracking.csv.gz`, `*-forces.csv.gz` i JSON: baseline INNER/WIDE oraz Grip0,85/0,70/0,55. Dodano tylko **3 standardowe światy INNER/Grip1 po8lap**: dokładny repeat18 oraz początkowe17,1/18,9m/s (±5%). Koło początkowo toczy się, Drive1 w całym przejeździe, bez późniejszego przestawiania. Power3 wykorzystuje wyłącznie istniejący zapis, do pierwszego fizycznego naruszenia bandy9,42s; nie wykonano nowego power3 ani historycznej macierzy. Testy celowane zawierają swoje niewielkie istniejące scenariusze; nie są ukryte jako reuse.

[freeze.json](full-throttle-slide-grip-audit/freeze.json) utrwala źródłowe hashe i procedurę przed nowymi próbami. Ekstrakcja ponownie wywołuje **oryginalne** Forces/Evaluate na zapisanych stanach, nie integruje drugiego świata. Dla wszystkich użytych native kroków odtworzone Fx/Fy i globalne F są **bitowo identyczne: max błąd0N**. Dzięki temu brakujący podział Fy front/rear pochodzi z tego samego równania i tych samych stanów; nie został dopasowany do prędkości.

Nowe `*-decomposition.csv.gz` zapisują każdy użyty krok, u/w, Fy front/rear, rzeczywistą pracę strat, ΔKbody/ΔKwheel, yaw work i przyspieszenia. Pełny pierwotny zapis położeń, sił i momentów pozostaje w #106. Każdy wiersz jest połączony dokładnym t, dt i kolejnością, z kontrolą długości. Dla odrzuconego power3 przechowujemy także końcowy interwał przecinający granicę, a pomijamy późniejszy ruch. Nie ma rankingu niepoprawnych czasów.

## 1. Równania i faktyczna utrata energii

β=`heading_body−heading_velocity`; u to prędkość wzdłuż motocykla, w to jego prędkość boczna (dla dodatniego β: u=Vcosβ, w=−Vsinβ; V jest modułem rzeczywistej v). Osobno κ=`(Rω−u)/sqrt(u²+u₀²)`. Obrót koła szybszy od jazdy nie oznacza większego kąta motocykla. Przód i tył nie mają osobnych punktów kontaktu ani osobnych bocznych prędkości wynikających z yaw.

W oryginalnym `RearWheelDynamics` Br=0,6B, Bf=0,4B. Dla a=(Rω−u)/δx, b=w/δy: `Fx_rear=Br·a/sqrt(1+a²+b²)`, `Fy_rear=−Br·b/sqrt(1+a²+b²)`, `Fy_front=−Bf·w/sqrt(w²+δy²)`. **Front Fx=0** w tym modelu. W skończonym kroku używa się oryginalnego implicit midpoint kontaktu, nie tej chwilowej wersji h0. Oba Fy i tylny Fx działają w środku ciała. Opory rolling/aero są osobnym F w kierunku przeciwnym do rzeczywistej v; yaw pochodzi z osobnego aktuatora/guard.

Straty: `P_lat=−(Fy_front+Fy_rear)w≥0`, `P_long=Fx_rear(Rω−u)≥0`; silnik `Pengine=Tdrive·ω`. Koło: `Iw·ωdot=Tdrive−R·Fx−cω`. Box2D dostaje raz `F=forward·Fx+side·Fy+Fres` i moment yaw. Po jego jednym kroku następuje jeden commit ω. Nie istnieje drugi odejmowany składnik `Pslip/V` ani hamulec driftu. Wartości loss są diagnostycznym **pomiarem pracy rzeczywistych sił**, a nie diagnostyką bez skutku fizycznego. Samo odjęcie ich jeszcze raz od napędu podwoiłoby stratę.

Weryfikacja na rzeczywistych końcach interwałów, z frozen axes początku i fizycznym float32 h:

`ΔKbody+ΔKwheel = Wengine − Llong − Llat − Lbearing + Wresistance + Wyaw + ε`.

Do ledger użyto faktycznego u_mid/w_mid z zaakceptowanych prędkości, nie wyłącznie konstytutywnego przewidywania midpoint. To rozróżnienie jest zachowane w źródłowym ledger #100/#106. Body obejmuje translację i yaw. Rozliczono dokładnie tę samą siłę float, która trafiła przez `ContactAdapter.Force` do `b2Body_ApplyForceToCenter` oraz torque do Box2D. Brak kontaktów/ściany i ukrytej siły.

{ledger}

Residual tego8lap wynosi **{e['Residual']:.6f}J**, przy955,944kJ pracy silnika. Największy |residual| dziewięciu użytych prefiksów: **{max_res:.6f}J**. To niezależne rozliczenie native zmian v/ω i zapisanych sił, zgodne ze starym ledger w skali float. Przy mikrosekundowych podziałach sam residual siłowy Δv/h jest wrażliwy na round-off; przechowujemy też residual impulsu. Wyjście przyspieszenia po normalnym10ms różni się od F·travel/m o około0,05m/s² wskutek skończonego kroku i zmiany kierunku, nie dodatkowego hamulca.

## 2. Przejazdy pełnego gazu — istniejące dane native

{speed_table}

Entry/middle/exit są interpolacją **zapisanych stanów** przy landmarkach obu łuków, średnią z końcowych dwóch okrążeń. Maksimum prostej wymaga kompletnej prostej; ostatnie niepełne62m pominięto. β, κ i pozostałe średnie poniżej dotyczą czasu w obu łukach tej samej końcowej pary. To nie prędkości nakazane solverowi. Wszystkie8 przejazdów nie przechodzą trackingu #106, dlatego tabela **nie zawiera rankingu czasowego**. Dodatnie accepted clearance baseline-wide nie rozstrzygają jego brakującego certyfikatu pomiędzy krokami.

{power_table}

Baseline INNER traci w jednym łuku średnio **12,759kJ energii body**, pomimo pełnego napędu. Średnio Pengine6,974kW, strata boczna6,399kW i wzdłużna0,847kW; dochodzą opory i łożysko. Średnie dKbody/dt≈−2,498kW. Prędkość entry82,72→exit67,79km/h; chwilami ponownie rośnie na spłaszczonym środku/wyjściu. Nie jest stałym equilibrium w każdym punkcie łuku, lecz okresową wymianą energii w całym okrążeniu. Hamowanie dotyczy64,36% czasu łuku, z samych oryginalnych sił.

![Prędkość i kąt rzeczywistego ruchu](full-throttle-slide-grip-audit/speed-and-slide.png)
![Praca silnika, straty i zmiana energii](full-throttle-slide-grip-audit/power-and-energy.png)

[cases.csv](full-throttle-slide-grip-audit/cases.csv) zawiera Fx, osobne Fy, krzywizny, przyspieszenia, błędy i prześwity; [corner-phases.csv](full-throttle-slide-grip-audit/corner-phases.csv) zachowuje oba łuki osobno. Wszystkie native pozycje i bieżące κ_path/κ_actual/κ_feedforward są w źródłach #106; uzupełnienia mają to samo t. Wielkości w tabelach nie są statycznymi próbkami.

## 3. Osobne statyczne próby oryginalnych równań

756 h0 zapytań: Grip4×V3×β9×κ7;108 steady-wheel,24 warianty surowej nawierzchni i72 zapytania odwrotności sterowania. Bez kroków Box2D i bez siły podtrzymującej kąt. `steady-wheel` oznacza tylko Tdrive−R·Fx−cω≈0 w zadanym stanie; **nie** dowód stabilności nadwozia. Bracket i42 iteracje rozwiązują tylko scalar ω. Nie szukano szybkiej linii.

Stała V18m/s, Grip1, Drive1; każdy wiersz ma oddzielny β i równowagę koła:

{static_table}

Przy10° model już hamuje≈0,527m/s², przy15°≈1,922m/s². Nie wymaga β40–60°, aby dać istotne straty. Zmiana β z5 na10° przy stałym V zmienia bilans tak, że napęd nie równoważy strat. Korzeń między tymi β jest **lokalnym bilansem sił stycznych**, bez dowodu realizacji koła/ruchu po wymaganym łuku lub zgodności yaw.

Zwiększenie κ przy stałych V/β/E podnosi tylny Fx i P_long, zmniejsza tylny Fy; front Fy pozostaje niezmienny. Tylny budżet jest wspólny. To poprawny kierunek mechanizmu combined slip, ale charakterystyka jest monotonicznie nasycająca i bez empirycznej identyfikacji; nie dowodzi realistycznego maksimum siły czy zachowania konkretnej opony w materiale toru. Przy h0 Drive zmienia moment, nie zmienia natychmiast stanu ω — energię na chwilę może dostarczać również zapisany obrót koła.

![Statyczne charakterystyki — oddzielone od przejazdów](full-throttle-slide-grip-audit/static-contact.png)

## 4. Grip, Ruts, Moisture i ograniczenia nawierzchni

`EffectiveGrip=clamp01(Grip·(1−0,45 Ruts)−|Moisture−0,35|·(Moisture>0,35?0,55:0,20))`.

W badanej fizyce surowe parametry zapadają się do tego jednego skalaru. Identyczne EffectiveGrip z różnych raw inputs daje identyczne siły/koło — test sprawdza to bitowo. Ruts nie zmienia tutaj kierunkowej krzywej opony, a Moisture nie tworzy innej zależności ścinania. Nawierzchnia jest zamrożona, nie wykonuje pracy deformacji podczas biegu.

E dla Grip1/0,85/0,70/0,55 przy Ruts0/Moisture0,5:0,9175/0,7675/0,6175/0,4675. B/m maleje14,546→13,651→12,765→11,800m/s², czyli o18,9% na końcach. **Grip nie jest μ.** B pochodzi z kalibracji `m·v_safe²/R`, a zależność v_safe∝sqrt(R) usuwa tu bezpośrednią zależność budżetu od promienia.

To **nie tylko mnożenie całej charakterystyki przez B**. E zmienia dostępny engine torque oraz regularizację: δy²=0,12²(u²+0,5²)+(0,03 B/m)²; δx²=0,1²(u²+0,5²)+[0,03 Br(R²/Iw+1/m)]². Minimum relaxation30ms wpływa na statyczny kształt nawet przy h0. Spadek Grip zmienia więc też znormalizowane krzywe κ–Fx/Fy, lecz poprzez tę samą zadaną rodzinę równań, nie nowe własności materiału. Nie jest to stanowa relaksacja długości kontaktu ani model historii ścinania.

W native INNER niższy Grip obniża corner mean engine6,974→6,165kW i boczne straty6,399→5,631kW. Mimo tego βmean rośnie10,70→12,22°, κmax0,2267→0,2522 i tracking RMS0,456→0,723m. Mniejsze siły i inna osiągnięta prędkość mogą polepszać clearance; to nie dowód większej przyczepności. Bezpieczny okresowy ruch nadal istnieje, przy innej krzywej prędkości.

Brakuje empirycznej krzywej opona–żużel, pionowych obciążeń i ich transferu, camber/sterowanego przedniego koła, odrębnych kontaktów, śladu naprężeń, histerezy, zapadania i przesuwania materiału. Rozdział60/40 jest stały, yaw jest skutecznym aktuatorem, nie wynikiem osi/torque contact. To wystarcza do kontrolowanych testów obecnego modelu, **nie** do zatwierdzenia absolutnych prędkości/uślizgu lub doboru Grip pod rzeczywisty tor.

Kontekst, nie kalibracja: [badanie McIntyre, Auburn2015](https://auetd.auburn.edu/handle/10415/4630) badało połączone siły na przygotowanej odkształcalnej glinie z kontrolą kąta, camber, obciążenia i ciśnienia. Nie jest pomiarem opony żużlowej. [Dokumentacja Project Chrono](https://api.projectchrono.org/vehicle_terrain.html) odróżnia modele handling oparte na zapytaniach o tarcie od kontaktu z odkształcalnym gruntem. Te źródła uzasadniają ostrożność wobec jednego skalaru; nie uzasadniają teraz dobudowy SCM lub zwiększenia Grip.

## 5. Czy istnieje stabilny ruch z pełnym gazem?

{stability_table}

**Tak, w skończonym eksperymencie.** INNER/Grip1 ma dodatnie prześwity przez8lap, powtarzalną prędkość oraz β dodatni przez cały łuk (minimum2,59°, maksimum18,91°). |β|≥3° przez98,46% czasu całego łuku i100% środkowych90%. Pozostałe≈1,54% to łagodne wygaszanie poślizgu przy końcu, a nie brak kontrolowanej jazdy. Każda z8 prób ma uślizg przez100% środkowych90% łuku. **Nie** oznacza to dokładnego wykonania zamierzonej linii: baseline INNER ma max lateral1,235m, powyżej limitu1m.

Trzy nowe próby, ta sama fizyka i Drive1; porównanie faktycznego XY/v w ostatnim okrążeniu na wspólnej fazie reference:

{perturb_table}

Po perturbacji±5% końcowy ślad różni się od bazowego o≤0,603mm, v o≤0,000353m/s. Wszystkie przechodzą safety native/between-step. Repeat18 ma **identyczny fingerprint #106**. To dowód lokalnego przyciągania do okresowego ruchu w zbadanej parze perturbacji i8lap, bez dowodu globalnej stabilności dla dowolnego startu. Prędkość rośnie na prostych i spada w częściach łuku; bilans cyklu ogranicza ją bez clampu. Nie twierdzimy, że jeden stały β/v opisuje cały zmiennokrzywiznowy łuk.

## 6. Co blokuje #106: A, B i C osobno

**A — POTWIERDZONE w konkretnych fragmentach.** Przy założeniu śledzenia Q i tej prędkości `v²|κ_path|>B/m` przez16,84% czasu łuków baseline INNER i17,80% WIDE. To konieczna granica: nawet większy β nie dostarczy wymaganej normalnej siły z tego B. Rzeczywista κ maleje/ślad odchodzi, zamiast otrzymać brakującą siłę. Dla power3 istniejący prefix obejmuje52,99% takiego czasu przed pierwszym fizycznym crossing9,42s. Na planie brak siły pojawia się wcześniej niż duży błąd. Nie potrzeba hipotezy o „braku strat”, by wyjaśnić ten przypadek. Wszystkie późniejsze power3 po crossing wykluczono z audytu sił na torze.

**B — potwierdzona niespójność odwrotności; jej udział w całym błędzie NIEROZSTRZYGNIĘTY.** Steering wyznacza target β odwrotnością historycznej pojedynczej Fy, a następnie przykłada combined rear/front contact #100. Znormalizowane żądanie jest przycinane do0,95B; ogranicznik yaw physical/energy nie był aktywny w #106. Poniższy h0 test bierze target z dokładnego obecnego kodu w stanie β0, a potem mierzy normalną siłę właściwego kontaktu przy tym β i steady wheel. To pojedyncza próba feed-forward, nie zamknięta pętla yaw i nie eksperyment przyczynowy ze zmienionym regulatorem:

{inverse_table}

Przy0,95B uzyskujemy97,42% żądanego przyspieszenia. To pokazuje, dlaczego scalar demand≤B i brak clampu yaw nie są certyfikatem śledzenia. Rozmiar efektu nie dowodzi, że wyjaśnia wszystkie1–2m błędu; preview, czasowe przejścia, dostępny coupled force i położenie też mają znaczenie. Nie uznano każdego momentu poniżej B za fizycznie wykonalny i automatycznie „winę regulatora”.

**C — POTWIERDZONE uproszczenie, wpływ na realny tor NIEROZSTRZYGNIĘTY.** Płynny, nasycający shared budget, stałe obciążenia i skalar E nie przedstawiają luźnego żużla w sposób zidentyfikowany eksperymentalnie. Natomiast równania generują faktyczne straty, spadek v i ograniczony ruch przy pełnym gazie. Nie ma dowodu, że zwiększenie strat lub Grip naprawi #106; bardziej energochłonna opona może po prostu maskować overspeed niższą prędkością.

![Zadana i faktyczna trajektoria; power3 tylko do crossing](full-throttle-slide-grip-audit/actual-versus-intended.png)
![Krzywizna i przyspieszenie normalne](full-throttle-slide-grip-audit/curvature-and-force.png)

## Siedem odpowiedzi

|Pytanie|Ocena|Uzasadnienie|
|---|---|---|
|1. Czy jazda bokiem rzeczywiście zmniejsza v?|POTWIERDZONE|Native INNER:82,72→67,79km/h w łuku przy Drive1; F·v/m i Δv są zgodne.|
|2. Ile energii i jaki wpływ?|POTWIERDZONE|Średnio6,399kW lateral+0,847kW longitudinal; body traci≈12,759kJ/łuk, bez podwójnego odejmowania.|
|3. Czy większy wheelspin działa realistycznie?|NIEROZSTRZYGNIĘTE|Poprawny jakościowy shared-budget kierunek i dodatnie straty potwierdzone; brak empirycznej walidacji Fx/Fy/κ konkretnego żużla.|
|4. Wpływ Grip przy pełnym gazie?|POTWIERDZONE|Mniejszy budżet/napęd, większy κ i średni β, niższe v; RMS trackingu rośnie. Grip nie jest μ.|
|5. Stabilny slide przez cały łuk?|POTWIERDZONE w badanym zakresie|Dodatni β w całym łuku,98,46% czasu≥3°, bezpieczeństwo i powrót po±5% przez8lap. Nie dowodzi dokładnego Q ani globalnej stabilności.|
|6. Co blokuje szybszy plan?|POTWIERDZONE częściowo|Przy osiągniętych v wymaganie Q przekracza B; pozostałego błędu nie przypisano jednej przyczynie. Geometria i energia decydują o legalnym profilu v.|
|7. Który następny eksperyment poprawi wiarygodność?|NIEROZSTRZYGNIĘTE do porównania|Sprawdzić constitutive-consistent inverse zamiast uznawać scalar B i historyczny inverse za wykonalność.|

**Jeden następny krok:** izolowane porównanie obecnego target-slip feed-forward z odwrotnością **normalnej siły oryginalnego coupled rear/front contact i aktualnego ω**. Odwrotność musi umieć zwrócić „nieosiągalne”; B nie jest obietnicą osiągalnej siły normalnej przy pełnym gazie. Zachować Drive1, Q, progressive-b, gains, yaw i oponę. Z góry rozdzielić fragmenty powyżej osiągalnej obwiedni (A) od pozostałych i sprawdzić, czy zmiana ogranicza błąd tylko w tych drugich (B). Nie dodawać hamulca, nowego Grip ani arbitralnej straty. Oczekiwany zysk wymaga testu; nie obiecujemy wykonania power3 ponad limitem sił.

## Walidacja i granice dostarczenia

Release zmienionych projektów:0 warnings/errors. **42/42 celowanych C#** (21 audyt/koło/crossover,20 slip dynamics i1 pomiar alokacji geometrii), **4/4 nowych Python**. Nie powtarzano pełnej historycznej macierzy. Standardowe końcowe Windows CI Draft ma link i wynik w PR/dostarczeniu. Historyczne audyty pozostają opt-in.

Pierwszy [Windows CI](https://github.com/arturgladysz91/zuzel/actions/runs/39483184314) wykrył jedynie stary test zerowych alokacji geometrii:4496B, a po jednym ponowieniu nieudanej części312B; pozostałe1822 core i395 trajectory przeszły. Osobno ten sam test przeszedł lokalnie. Pomiar wcześniej rozgrzewał tylko Project, a obejmował też RoadAt i ReferenceSpeed na wątku runnera. Poprawiono wyłącznie harness testu: jeden dedykowany wątek i jedna pełna rozgrzewka dokładnie tej samej pętli5000 wywołań przed jednym pomiarem. **Nadal wymagane0B**, bez tolerancji, retry pomiaru lub zmiany geometrii. Poprawiony pomiar przeszedł celowany test Release; przyczyna wcześniejszych dodatkowych alokacji nie jest rozstrzygnięta. Końcowe CI sprawdza także ten harness.

**{sum(a['HistoricalHashes'].values())}/{sum(a['HistoricalHashes'].values())} historycznych SHA-256 zachowane**, włącznie ze wszystkimi132 plikami manifestu #106. Żaden plik fizyki opony/koła/Box2D ani geometry/controller nie zmienia się. Repeat18 potwierdza stary fingerprint, a read-only dekompozycja jest identyczna w każdym przebadanym Fx/Fy/F. Targeted TRX, [analysis.json](full-throttle-slide-grip-audit/analysis.json), [extraction.json](full-throttle-slide-grip-audit/extraction.json), [statyczne próby](full-throttle-slide-grip-audit/static-grid.json), [steady-wheel](full-throttle-slide-grip-audit/steady-wheel.json) i manifest pozwalają oddzielić native od statycznych próbek i hipotez.

Reprodukcja do pustego katalogu: Release narzędzia, `full-throttle-slide DIR freeze`, `extract`, `probes`, `controller-probes`, `perturbations` (każdy etap jako oddzielne wywołanie tej samej komendy). `extract` czyta dostarczone źródła #106 i nie wywołuje Box2D Step. Probes tworzą także jeden niekrokowany fixture świata dla odczytu aktualnej masy/inercji. Raport/helper Python nie implementuje fizyki. Wykresy wymagają matplotlib, analiza standardowej biblioteki. Trzy nowe światy i surowe native zapisy zachowano, bez nowego UI i bez merge.
'''
    (ROOT/'docs/experiments/full-throttle-slide-grip-audit.md').write_text(content,encoding='utf8')
    save('validation.json',{'TargetedCSharp':42,'TargetedPython':4,'MainMatrixWorldsRerun':0,'AdditionalWorlds':3,
        'SourceFingerprintExact':True,'MaximumReplayForceErrorNewtons':0,'MaximumEnergyResidualJoules':max_res,
        'HistoricalManifestCounts':a['HistoricalHashes'],'HistoricalHashes':sum(a['HistoricalHashes'].values()),'CI':'Exact final HEAD and Windows CI run in PR/final delivery.'})
    manifest={f.name:hashlib.sha256(f.read_bytes()).hexdigest() for f in sorted(DATA.iterdir()) if f.is_file() and f.name!='manifest.json'}
    save('manifest.json',manifest)

if __name__=='__main__':
    if '--report-only' in sys.argv:
        report(load(DATA/'analysis.json'));print('Report and manifest updated; no physics or source extraction.');sys.exit(0)
    analysis,cache=analyze()
    if '--plots' in sys.argv:plots(analysis,cache)
    report(analysis)
    print(json.dumps({'cases':len(analysis['Cases']),'new_worlds':3,'old_hashes':sum(analysis['HistoricalHashes'].values()),'tests':analysis['TargetedTests']['passed']}))
