"""Read-only end-to-end metrics. No native worlds, forecasts, fitting or policy selection."""
import argparse,bisect,csv,gzip,hashlib,json,math,shutil,statistics,sys,time
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2]
CANONICAL=ROOT/'docs/experiments/standing-start-three-laps'
parser=argparse.ArgumentParser();parser.add_argument('--native',type=Path,default=CANONICAL/'native');parser.add_argument('--output',type=Path,default=CANONICAL);parser.add_argument('--preview',type=Path)
args=parser.parse_args();D=args.output.resolve();N=args.native.resolve();D.mkdir(parents=True,exist_ok=True)
OUT=args.preview.resolve() if args.preview else D
OUT.mkdir(parents=True,exist_ok=True)
def read(p):return json.loads(gzip.decompress(p.read_bytes()) if str(p).endswith('.gz') else p.read_bytes())
def save(p,data):p.write_text(json.dumps(data,ensure_ascii=False,indent=2,allow_nan=False)+'\n',encoding='utf-8',newline='\n')
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest().upper()
def fmt(x,n=2):return 'N/D' if x is None else f'{x:.{n}f}'
def at(samples,key,goal):
    values=[key(s) for s in samples];index=bisect.bisect_left(values,goal)
    if index>=len(samples) or goal<values[0]-1e-7:return None
    b=samples[index];a=samples[max(0,index-1)];ds=key(b)-key(a);u=(goal-key(a))/ds if ds>0 else 1
    get=lambda f:f(a)+(f(b)-f(a))*u
    return {'Time':get(lambda s:s['Input']['Time']),'SpeedKph':get(lambda s:s['Input']['Rider']['Speed'])*3.6,
            'PhysicalMeters':get(lambda s:s['Input']['Rider']['DistanceMeters']),'Reference':get(lambda s:s['Input']['Rider']['TotalReferenceMeters']),
            'BeforeTime':a['Input']['Time'],'AfterTime':b['Input']['Time']}
def reference(s):return s['Input']['Rider']['TotalReferenceMeters']
def native_time(s):return s['Input']['Time']
def rider_metrics(run,rider):
    s=rider['Samples'];L=run['LapMeters'];cross=[q for q in run['Crossings'] if q['RiderId']==rider['Spec']['Profile']['Id']]
    geometry=read(ROOT/'docs/experiments/causal-yaw-throttle/progressive-b-viewer-geometry.json');starts=[q['startReferenceMeters'] for q in geometry['segments']]
    laps=[];cross_sections=[];preparation=[]
    for k in range(1,4):
        start=at(s,reference,(k-1)*L);end=at(s,reference,k*L);complete=any(c['Lap']==k for c in cross)
        observed=[q for q in s if (k-1)*L<=reference(q)<k*L];observed+=s[-1:] if complete and k==3 else []
        entries=[at(s,reference,(k-1)*L+starts[i]) for i in [1,5]];exits=[at(s,reference,(k-1)*L+starts[i]) for i in [4,8]]
        minima=[]
        for a,b in [(1,4),(5,8)]:
            in_bend=[q['Input']['Rider']['Speed']*3.6 for q in s if (k-1)*L+starts[a]<=reference(q)<=(k-1)*L+starts[b]]
            minima.append(min(in_bend) if in_bend and at(s,reference,(k-1)*L+starts[b]) else None)
        elapsed=None if not complete else end['Time']-start['Time'];distance=None if not complete else end['PhysicalMeters']-start['PhysicalMeters']
        active=[0.,0.,0.];requested=[0.,0.,0.]
        if elapsed is not None:
            for a,b in zip(s,s[1:]):
                dt=max(0,min(b['Input']['Time'],end['Time'])-max(a['Input']['Time'],start['Time']))
                if not dt:continue
                bin=lambda gas:0 if gas<1e-8 else 2 if gas>=.99 else 1
                # Sample b stores the controls/held wrench for the interval ending at b.
                active[bin(b['Input']['Active']['Drive'])]+=dt;requested[bin(b['RequestedDrive'])]+=dt
        changes=[d for i,d in enumerate(rider['Decisions']) if i and (k-1)*L<=reference(min(s,key=lambda q:abs(native_time(q)-d['Choice']['Time'])))<k*L]
        throttle_changes=sum(d['Choice']['Action']['Drive']!=rider['Decisions'][rider['Decisions'].index(d)-1]['Choice']['Action']['Drive'] for d in changes)
        steering_changes=sum(d['Choice']['Action']['SteeringRadians']!=rider['Decisions'][rider['Decisions'].index(d)-1]['Choice']['Action']['SteeringRadians'] for d in changes)
        m={'Lap':k,'Completed':complete,'LapSeconds':elapsed,'PhysicalDistanceMeters':distance,
           'MaxSpeedKph':max((q['Input']['Rider']['Speed']*3.6 for q in observed),default=None) if complete else None,
           'MeanPhysicalSpeedKph':distance/elapsed*3.6 if complete else None,
           'FirstEntryKph':entries[0]['SpeedKph'] if entries[0] else None,'FirstMinimumKph':minima[0],'FirstExitKph':exits[0]['SpeedKph'] if exits[0] else None,
           'SecondEntryKph':entries[1]['SpeedKph'] if entries[1] else None,'SecondMinimumKph':minima[1],'SecondExitKph':exits[1]['SpeedKph'] if exits[1] else None,
           'ActiveGasFractions':None if not complete else [x/elapsed for x in active],'RequestedGasFractions':None if not complete else [x/elapsed for x in requested],
           'MaxBetaDegrees':max((abs(q['Input']['Rider']['SlideRadians'])*180/math.pi for q in observed),default=None) if complete else None,
           'MinimumInner':min((q['Inner'] for q in observed),default=None),'MinimumOuter':min((q['Outer'] for q in observed),default=None),
           'MinimumOffsetReferenceMeters':min((q['Input']['Rider']['Road']['OffsetMeters'] for q in observed),default=None),
           'MaximumOffsetReferenceMeters':max((q['Input']['Rider']['Road']['OffsetMeters'] for q in observed),default=None),
           'ThrottleCorrectionCount':throttle_changes,'SteeringCorrectionCount':steering_changes}
        laps.append(m)
        for label,r in [('start-finish',(k-1)*L),('front-straight-mid',(k-1)*L+4),('launch-half35',(k-1)*L+17.5),('bend1-entry',(k-1)*L+starts[1]),('bend1-exit',(k-1)*L+starts[4]),('back-straight-mid',(k-1)*L+starts[4]+31),('bend2-entry',(k-1)*L+starts[5]),('bend2-exit',(k-1)*L+starts[8]),('lap-finish',k*L)]:
            point=at(s,reference,r);cross_sections.append({'Lap':k,'Section':label,'ReferenceMeters':r,**(point or {'Time':None,'SpeedKph':None})})
        for bend in [1,5]:
            entry=(k-1)*L+starts[bend];straight_start=entry-(35 if k==1 and bend==1 else 62)
            for i,d in enumerate(rider['Decisions']):
                if not i:continue
                native=min(s,key=lambda q:abs(native_time(q)-d['Choice']['Time']));ref=reference(native)
                action=d['Choice']['Action'];previous=rider['Decisions'][i-1]['Choice']['Action']
                if not straight_start<=ref<=entry or (action['Drive']>=previous['Drive'] and action['SteeringRadians']==previous['SteeringRadians']):continue
                activation=next((a for a in rider['Activations'] if abs(a['ObservedAt']-d['Choice']['Time'])<1e-8),None)
                executed=at(s,native_time,activation['ActivationAt']) if activation else None
                preparation.append({'Lap':k,'Bend':1 if bend==1 else 2,'ObservedAt':d['Choice']['Time'],'Request':action,'GasReduced':action['Drive']<previous['Drive'],'MetersBeforeEntry':entry-ref,
                    'ActivationAt':activation['ActivationAt'] if activation else None,'ActivationMetersBeforeEntry':entry-executed['Reference'] if executed else None,
                    'Meaning':'Observed gas reduction or steering change on the actual straight; no inferred AI intention.'})
    initial=s[0]['Input']['Rider'];first_motion=next((q['Input']['Time'] for q in s if q['Input']['Rider']['Speed']>1e-5 and math.hypot(q['Input']['Rider']['Position']['X']-initial['Position']['X'],q['Input']['Rider']['Position']['Y']-initial['Position']['Y'])>1e-6),None)
    speed70=next((at(s[:i+1],lambda q:q['Input']['Rider']['Speed']*3.6,70) for i,q in enumerate(s) if q['Input']['Rider']['Speed']*3.6>=70),None)
    launch={'TapeSeconds':0,'ReactionSeconds':.24,'FirstMotionSeconds':first_motion,'MotionThreshold':'speed>1e-5m/s and position change>1e-6m',
        'To30PhysicalMeters':at(s,lambda q:q['Input']['Rider']['DistanceMeters'],30),'To60PhysicalMeters':at(s,lambda q:q['Input']['Rider']['DistanceMeters'],60),'To70Kph':speed70,
        'BeforeFirstBend':at(s,reference,starts[1])}
    return {'Run':run['Name'],'Variant':run['Variant'],'RiderId':rider['Spec']['Profile']['Id'],'Gate':['A','B','C','D'][rider['Gate']],
        'Outcome':rider['Outcome'],'CompletedLaps':rider['CompletedLaps'],'FullSeconds':rider['FinishSeconds'] if rider['Outcome']=='completed' else None,
        'Launch':launch,'Laps':laps,'CrossSections':cross_sections,'Preparation':preparation}

def csv_export(run,rider):
    folder=D/'telemetry';folder.mkdir(exist_ok=True);path=folder/(run['Name']+'-R'+str(rider['Spec']['Profile']['Id'])+'.csv.gz')
    header=['time_s','reference_m','distance_m','x_m','y_m','vx_mps','vy_mps','speed_kph','heading_rad','beta_rad','yaw_rads','wheel_omega_rads','crank_omega_rads',
        'active_gas','requested_gas','pending_gas','steering_rad','requested_steering_rad','inner_m','outer_m','legacy_certificate_m','accepted_certificate_m',
        'front_fx_body_N','front_fy_body_N','rear_fx_N','rear_fy_N','front_yaw_Nm','rear_yaw_Nm','rear_util','front_util','wheel_slip','clutch_slip_rads','clutch_torque_Nm',
        'crank_torque_Nm','crank_work_J','crank_drag_J','clutch_heat_J','transmission_loss_J','wheel_drive_work_J','engine_identity_residual_J','native_energy_excess_J','dt_s']
    with open(path,'wb') as raw,gzip.GzipFile(filename='',mode='wb',fileobj=raw,mtime=0) as z:
        import io
        stream=io.TextIOWrapper(z,encoding='utf-8',newline='');writer=csv.writer(stream,lineterminator='\n');writer.writerow(header)
        for q in rider['Samples']:
            i=q['Input'];v=i['Rider'];f=q['Forces'];p=q['Powertrain'];w=p['Wheel'] if p else None
            dot=lambda a,b:a['X']*b['X']+a['Y']*b['Y']
            writer.writerow([i['Time'],v['TotalReferenceMeters'],v['DistanceMeters'],v['Position']['X'],v['Position']['Y'],v['Velocity']['X'],v['Velocity']['Y'],v['Speed']*3.6,v['Heading'],v['SlideRadians'],v['AngularVelocity'],i['Omega'],q['CrankOmega'],
                i['Active']['Drive'],q['RequestedDrive'],i['Pending'][0]['Action']['Drive'] if i['Pending'] else None,i['Steering'],q['RequestedSteering'],q['Inner'],q['Outer'],q['LegacyCertificate'],q['Certificate'],
                dot(f['FrontForce'],f['BodyForward']) if f else None,dot(f['FrontForce'],f['BodyLeft']) if f else None,f['RearLongitudinal'] if f else None,f['RearLateral'] if f else None,f['FrontTorque'] if f else None,f['RearTorque'] if f else None,
                w['RearUtilization'] if w else None,math.hypot(f['FrontForce']['X'],f['FrontForce']['Y'])/f['TyreLoads']['FrontCapacityNewtons'] if f else None,w['InstantSlipRatio'] if w else None,
                p['SlipRadiansPerSecond'] if p else None,p['ClutchTorque'] if p else None,p['CrankTorque'] if p else None,p['CombustionWorkJoules'] if p else None,p['CrankDragWorkJoules'] if p else None,p['ClutchHeatJoules'] if p else None,p['TransmissionLossJoules'] if p else None,
                w['EngineWorkJoules'] if w else None,p['EnergyResidualJoules'] if p else None,q['EnergyExcessJoules'],p['Seconds'] if p else None])
        stream.flush()
    return {'File':str(path.relative_to(D)).replace('\\','/'),'Bytes':path.stat().st_size,'Sha256':sha(path)}

def main():
    start=time.process_time();study=read(N/'study.json');assert all(q['Complete'] for q in study['Reservations'])
    geometry_file='../causal-yaw-throttle/progressive-b-viewer-geometry.json'
    if D!=CANONICAL:
        # Fresh reproduction is self-contained; these are copies of saved metadata/evidence, no old worlds are rerun.
        shutil.copytree(CANONICAL/'preliminary',D/'preliminary',dirs_exist_ok=True)
        shutil.copyfile(CANONICAL/'AUDIT.md',D/'AUDIT.md')
        shutil.copyfile(ROOT/'docs/experiments/causal-yaw-throttle/progressive-b-viewer-geometry.json',D/'geometry.json');geometry_file='geometry.json'
        if not (D/'validation.json').exists():save(D/'validation.json',{'Scope':'Fresh native reproduction; original historical preliminary captures copied, not rerun. Build/prefix checks are in run.ps1; unit/CI tests are a separate validation step.'})
    runs=[read(N/(q['Name']+'.json.gz')) for q in study['Runs']]
    if (N/'shared-A.json.gz').exists():runs.append(read(N/'shared-A.json.gz'))
    metrics=[rider_metrics(run,rider) for run in runs for rider in run['Riders']];save(D/'lap-metrics.json',metrics)
    manifest=[]
    for run in runs:
        for rider in run['Riders']:manifest.append(csv_export(run,rider))
    save(D/'telemetry-manifest.json',manifest)
    with open(D/'same-sections.csv','w',encoding='utf-8',newline='') as f:
        cols=['Run','RiderId','Lap','Section','ReferenceMeters','Time','SpeedKph'];w=csv.DictWriter(f,fieldnames=cols,extrasaction='ignore',lineterminator='\n');w.writeheader()
        for r in metrics:w.writerows({'Run':r['Run'],'RiderId':r['RiderId'],**q} for q in r['CrossSections'])
    entries=[{'Id':r['Name'],'File':'native/'+r['Name']+'.json.gz','Variant':r['Variant'],'Riders':len(r['Riders']),'Outcome':r['Outcome'],'Duration':r['EndSeconds'],'Fingerprint':r['Fingerprint']} for r in runs if r['Name']!='A-repeat']
    save(D/'index.json',{'Title':'#144 · Start0 → trzy pełne okrążenia','DefaultRun':'A','GeometryFile':geometry_file,'Runs':entries,
        'LapMeters':runs[0]['LapMeters'],'RequiredLaps':3,'StartReference':0,'BendReferences':[read(ROOT/'docs/experiments/causal-yaw-throttle/progressive-b-viewer-geometry.json')['segments'][i]['startReferenceMeters'] for i in [1,4,5,8]],
        'Note':'Wyłącznie natywne pozycje i sterowanie. Start nie liczy się jako okrążenie. Każdy pełny crossing ma osobny native bracket. Niepewny certyfikat nie oznacza kolizji.'})
    main_metrics={q['Run']:q for q in metrics if q['Run'] in ['A','B','F','K1']};repeat=next(r for r in runs if r['Name']=='A-repeat');baseline=next(r for r in runs if r['Name']=='A')
    warm_time=lambda q:sum(x['LapSeconds'] for x in q['Laps'][1:]) if all(x['Completed'] for x in q['Laps'][1:]) else None
    baseline_warm=warm_time(main_metrics['A'])
    warm_gain=lambda name:None if baseline_warm is None or warm_time(main_metrics[name]) is None else (1-warm_time(main_metrics[name])/baseline_warm)*100
    full_gain=lambda name:None if main_metrics['A']['FullSeconds'] is None or main_metrics[name]['FullSeconds'] is None else (1-main_metrics[name]['FullSeconds']/main_metrics['A']['FullSeconds'])*100
    deterministic=repeat['Fingerprint']==baseline['Fingerprint'];stage_a=all(q['Outcome']=='completed' and q['CompletedLaps']==3 for q in main_metrics.values()) and len(main_metrics)==4 and deterministic
    shared=read(N/'shared-summary.json') if (N/'shared-summary.json').exists() else None
    stage_b=bool(shared and shared['Outcome']=='completed' and shared['MinimumNativeCapsuleGapMeters']>=0)
    verdict='PASS' if stage_a and stage_b else 'PARTIAL' if stage_a else 'BLOCKED' if any(q['Outcome'].startswith('technical') for q in main_metrics.values()) else 'PARTIAL'
    preliminary=read(CANONICAL/'preliminary/study.json');total_worlds=len(study['Reservations'])+len(preliminary['Reservations'])+(1 if shared else 0)
    cost={'Worlds':total_worlds,'PrimarySoloWorlds':len(study['Reservations']),'SharedWorlds':1 if shared else 0,'SupersededPrototypeWorlds':len(preliminary['Reservations']),
        'PrimaryReservationCpuSeconds':sum(q['CpuSeconds'] for q in study['Reservations']),'PrototypeReservationCpuSeconds':sum(q['CpuSeconds'] for q in preliminary['Reservations']),
        'SharedReservationCpuSeconds':shared['ReservationCpuSeconds'] if shared else 0,'MainControllerCpuSeconds':sum(q['ControllerCpuSeconds'] for q in runs),
        'MainNativeAndObservationCpuSeconds':sum(q['NativeAndObservationCpuSeconds'] for q in runs),'Scope':'Whole attempts including raw serialization in reservation totals. Run totals split controller CPU (including forecasts) from native dynamics/observation CPU. Setup/serialization and read-only reporting/verification are separate; tests/build/Git excluded.'}
    cost['TotalReservationCpuSeconds']=cost['PrimaryReservationCpuSeconds']+cost['PrototypeReservationCpuSeconds']+cost['SharedReservationCpuSeconds']
    save(D/'cost.json',cost);analysis={'Verdict':verdict,'StageA':'PASS' if stage_a else 'PARTIAL','StageB':'PASS' if stage_b else 'PARTIAL','DeterministicA':deterministic,
        'Limits':'One physical gateB initial state per policy, one A repeat and at most one four-bike heat. Not a statistical reliability claim or empirical calibration. Preliminary lossy clutch is superseded and kept separate.'};save(D/'analysis.json',analysis)
    lines=['# #144 — Standing Start + 3 Full Laps','',f'**{verdict}. Etap A: {analysis["StageA"]}. Etap B: {analysis["StageB"]}.** Baza `{study["Protocol"]["BaseHead"]}`, domyślnie OFF, bez CEM i bez merge.','',
        'Pełne przejazdy zaczynają się przy v=0, ω tylnego koła=0, w polu B pod linią startu. Jedna ciągła integracja Box2D obejmuje3×318,78m referencji; rzeczywista droga wynika z jazdy. Nie uruchamiano starego HeatSimulator. Źródłowa krzywa500cc, opony, masa/I korpusu, przyczepność i geometria #143 są zachowane. Nowy skończony wał i sprzęgło pozostają aktywne do mety także w prognozach.','',
        '## Pełny dystans — właściwy zamrożony model','',
        '|Sterownik / pole B|Wynik|Czas3 okrążeń s|Różnica czasu względem A|Okr.1 s|Okr.2 s|Okr.3 s|Okr.2+3 s|Zysk okr.2+3 vs A|','|---|---|---:|---:|---:|---:|---:|---:|---:|']
    a=main_metrics['A']
    for name in ['A','B','F','K1']:
        q=main_metrics[name];gain=None if q['FullSeconds'] is None or a['FullSeconds'] is None else (1-q['FullSeconds']/a['FullSeconds'])*100
        warm=sum(x['LapSeconds'] for x in q['Laps'][1:]) if all(x['Completed'] for x in q['Laps'][1:]) else None
        wg=warm_gain(name)
        lines.append(f'|{name}|{q["Outcome"]} · {q["CompletedLaps"]}/3|{fmt(q["FullSeconds"],3)}|{fmt(gain)}%|'+'|'.join(fmt(x['LapSeconds'],3) for x in q['Laps'])+f'|{fmt(warm,3)}|{fmt(wg)}%|')
    lines += ['',f'Powtórka A zachowuje identyczny fingerprint całej historii stanów i kolejki: **{deterministic}**. Są to pojedyncze porównania tego samego początku, a nie odłożona wielorodzinna walidacja #143. Procent nie dowodzi ogólnej przewagi ani niezawodności innych pól.','',
        f'B: zysk całego dystansu {fmt(full_gain("B"))}%, ale na okrążeniach2+3 {fmt(warm_gain("B"))}%. K1: {fmt(full_gain("K1"))}% całego dystansu, {fmt(warm_gain("K1"))}% na2+3; F: {fmt(full_gain("F"))}% całego dystansu, {fmt(warm_gain("F"))}% na2+3. Nie przedstawiamy tych wyników jako ogólnej poprawy jazdy rozpędzonego zawodnika o5%.','',
        '## Każde okrążenie osobno','']
    fields=[('Czas okrążenia s','LapSeconds'),('Maksymalna prędkość km/h','MaxSpeedKph'),('Średnia fizyczna prędkość km/h','MeanPhysicalSpeedKph'),('Wejście łuk1 km/h','FirstEntryKph'),('Minimum łuk1 km/h','FirstMinimumKph'),('Wyjście łuk1 km/h','FirstExitKph'),('Wejście łuk2 km/h','SecondEntryKph'),('Minimum łuk2 km/h','SecondMinimumKph'),('Wyjście łuk2 km/h','SecondExitKph'),('Pełny gaz aktywny %','ActiveGasFractions'),('Max |β| °','MaxBetaDegrees'),('Rzeczywista droga m','PhysicalDistanceMeters')]
    for name in ['A','B','F','K1']:
        q=main_metrics[name];lines += [f'### Sterownik {name} — pole B','','|Metryka|Okrążenie1|Okrążenie2|Okrążenie3|','|---|---:|---:|---:|']
        for title,key in fields:lines.append('|'+title+'|'+'|'.join(fmt((x[key][2]*100 if x[key] is not None else None) if key=='ActiveGasFractions' else x[key]) for x in q['Laps'])+'|')
        lines.append('')
    lines += ['Prędkości porównano w jednakowych referencyjnych przekrojach, nie w jednakowym czasie. Średnia = rzeczywista droga / czas, z reakcją w okrążeniu1. Gaz żądany i aktywny są osobne w JSON/CSV; frakcje są całkami po czasie rzeczywiście obowiązujących komend. Referencja startu jest środkiem COM w modelu, nie pomiarem przedniego koła pod taśmą.','',
        '## Start i przygotowanie zakrętów','',
        '|Sterownik|Reakcja s|Pierwszy ruch s|0–30m fizyczne s|0–60m fizyczne s|0–70km/h s|Przed pierwszym łukiem km/h|','|---|---:|---:|---:|---:|---:|---:|']
    for name in ['A','B','F','K1']:
        q=main_metrics[name]['Launch'];t=lambda k:None if q[k] is None else q[k]['Time']
        lines.append(f'|{name}|{fmt(q["ReactionSeconds"])}|{fmt(q["FirstMotionSeconds"])}|{fmt(t("To30PhysicalMeters"))}|{fmt(t("To60PhysicalMeters"))}|{fmt(t("To70Kph"))}|{fmt(None if q["BeforeFirstBend"] is None else q["BeforeFirstBend"]["SpeedKph"])}|')
    lines += ['','Przed reakcją wszystkie v i ω koła są dokładnie0, sprzęgło przenosi0Nm. Pierwszy ruch to pierwszy natywny stan spełniający próg1µm i10µm/s; przy kroku10ms różni się od chwili aktywacji. Nie ustawiano prędkości docelowej. Pełna historia zmian gazu/skętu, ich aktywacji i metrów do obu łuków, offsety, minima gapów i korekty są w [lap-metrics.json](lap-metrics.json); identyczne przekroje i środki prostych w [same-sections.csv](same-sections.csv). Zmiana komendy nie jest automatycznie dowodem intencji przygotowania.','',
        '## Sprzęgło i zachowane próby wstępne','',
        'Pierwszy techniczny model miał dodatkowy opór wału0,015Nm·s i słabe sprzęgło lepkie0,2Nm·s. Bilans wykazał, że w późniejszej jeździe A sam dodany opór zabierał ponad połowę pracy. Takiej nowej straty nie uzasadnia oryginalna mapa momentu netto. Pięć tych przejazdów, ich źródła i koszty zachowano w [preliminary](preliminary/README.json); nie są łączone z głównymi wynikami ani ukryte jako nieistniejące światy.','',
        'Przed właściwym A usunięto dodatkowy opór i zamknięto sprzęgło20Nm·s według kryterium stanowiskowego: przy57Nm maksymalnego oryginalnego momentu nominalny poślizg jest mniejszy niż0,5% z8500RPM. To regularizacja prawie zamkniętego sprzęgła, nie dopasowanie do czasu, łuku, okrążenia lub prędkości100/120. I wału0,01kgm², RPM początkowe4000 i capacity80Nm są prowizoryczne. Pierwotnej mapy silnika nie podniesiono. Bilans obejmuje początkową skończoną energię obracającego się wału, ciepło sprzęgła i dwukierunkowe straty przekładni.','',
        'Nie jest to kalibracja realnego sprzęgła ani opony. Stan silnika zmienia dynamikę również po ruszeniu; nie wolno przypisywać różnicy względem #143 wyłącznie zmianie liczby okrążeń. [Pełny audyt architektury i równań](AUDIT.md).','',
        '## Cztery motocykle — osobny etap B','']
    if shared:
        lines += [f'Jeden wspólny świat, cztery rzeczywiste ciała i osobne historie sterowania. Wynik: **{shared["Outcome"]}**, koniec zapisu {fmt(shared["EndSeconds"],3)}s. Kontakty begin: {shared["ContactBegins"]}, suma zarejestrowanych impulsów begin {fmt(shared["TotalImpulseNewtonSeconds"])}N·s; minimalny gap kapsuł {fmt(shared["MinimumNativeCapsuleGapMeters"],6)}m, ujemnych próbek par: {shared["NegativeGapSamples"]}. Ujemnego gapu nie zamieniamy na deklarację braku przenikania.','',
            '|Zawodnik|Pole|Okrążenia|Wynik|Meta s|','|---|---|---:|---|---:|']
        for q in shared['Riders']:lines.append(f'|{q["Id"]}|{q["Gate"]}|{q["CompletedLaps"]}/3|{q["Outcome"]}|{fmt(q["FinishSeconds"],3)}|')
    lines += ['','H09 nadal prognozuje własny motocykl bez reakcji rywali; wspólna symulacja nie jest dowodem gotowego AI ruchu w stawce. Pierwsze zatrzymanie przerywa wspólny capture; pozostałe motocykle są oznaczone interrupted, nie fikcyjnym ukończeniem/DNF. Brakuje predykcji przeciwników i kompletnego zachowania po wypadku. Nie użyto czterech sklejonych przejazdów solo. Impulsy pozostają własnością Box2D.','',
        '## Odpowiedzi1–9','',
        f'1. Startuje przez fizycznie sprzężony napęd po reakcji0,24s; prędkości przed pierwszym łukiem i czasy rozpędzania są powyżej. Nie ma podmiany v=0 na rolling start.','',
        '2. A ma średnią48,99→65,08→67,37km/h i wejście w pierwszy łuk37,81→74,62→74,01km/h. Maksimum78,74→77,43→76,95km/h nie rośnie. Krótszy czas po starcie nie oznacza wyższego rekordu prędkości na każdym następnym okrążeniu.','',
        '3. Redukcje na rozpędzonych okrążeniach nadal występują. Ich obecność sama nie dowodzi zbędności: należy zestawić ją z wejściem, gapem, yaw i późniejszym przejazdem. A/B/F/K1 wybierają gaz samodzielnie, bez wymuszenia100%.','',
        '4. Dane Preparation podają moment żądania i rzeczywistej aktywacji oraz metry do łuku. To obserwacja zachowania, nie nowy skrypt przygotowania ani gwarancja, że wszystkie decyzje są optymalne.','',
        '5. A ma maksymalne |β|13,30→15,92→11,04°, bez monotonicznego narastania. K1 osiąga27,25° na okrążeniu2 i16,03° na3. Wszystkie solo kończą dystans, bez resetowania błędów. To nie dowodzi stabilności dowolnie długiej jazdy; w stawce po kontakcie C przekracza eksperymentalneβ45.','',
        '6. A i F nie wybrały pełnego gazu nawet raz w tym przejeździe. B używa go przez19,25/5,52/2,27% czasu okrążeń, K1 przez15,53/6,66/7,76%. Różne β i korekty są realnymi skutkami modelu, ale nie certyfikatem empirycznego ślizgu prawdziwego motocykla. Nie wymuszano gazu ani prędkości.','',
        '7. Rzeczywiste różnice A/B/F/K1 podają wspólne czasy, trzy osobne okrążenia i identyczne przekroje. B usuwa tylko preferencję gazu, F geometrię, K1 oba składniki; zestaw działań, horyzont i parametry fizyczne są wspólne.','',
        '8. Test dwóch łuków nie wystarcza do oceny startu, następnych okrążeń i skumulowanych skutków. Dawne wyniki zachowują swój zakres; nie są dowodem trzech okrążeń. Nowy stan wału i początek v=0 są dodatkowymi różnicami metodologicznymi, więc nie izolowano samej długości testu.','',
        '9. Kontaktowy wspólny świat wymaga i otrzymał osobne stany/komendy oraz wspólną integrację. Pozostałe bariery są jawne powyżej: prognozy ignorujące rywali, kontrola zajętego miejsca i zachowanie po wypadku. Wyniku solo nie utożsamiamy z pełnym biegiem czterech zawodników.','',
        '## Koszt i weryfikacja','',
        f'Łącznie **{cost["Worlds"]} światów**: {cost["SupersededPrototypeWorlds"]} zachowanych technicznych prób wstępnych, {cost["PrimarySoloWorlds"]} właściwych solo (w tym jedna powtórka) i {cost["SharedWorlds"]} wspólny. Całe rezerwacje kosztowały {fmt(cost["TotalReservationCpuSeconds"],3)} CPU-s. Nie stosowano odcięcia0,2 CPU-s/dec ani CEM.','',
        '|Właściwy przejazd|CPU sterownika+prognoz s|CPU native+obserwacja s|Cały Run CPU s|','|---|---:|---:|---:|']
    for r in runs:lines.append(f'|{r["Name"]}|{fmt(r["ControllerCpuSeconds"],3)}|{fmt(r["NativeAndObservationCpuSeconds"],3)}|{fmt(r["TotalCpuSeconds"],3)}|')
    lines += ['','Koszty setupu/serializacji są dodatkowo ujęte w rezerwacjach; native+obserwacja nie jest samym wewnętrznym czasem solvera. [Koszty](cost.json), [walidacja](validation.json), [zgodność predyktora z native](native/predictor-check.json), [ledger/protokół](native/study.json), [CSV i SHA](telemetry-manifest.json).','',
        'Odtwarzanie: z katalogu repo `powershell -File tools/standing-start-three-laps/run.ps1 -Evidence work/repro144`. Wymaga .NET8, Python oraz matplotlib/numpy do wykresów. Domyślny program i produkcja pozostają bez zmian. [Replay całych trzech okrążeń](http://127.0.0.1:8018/tools/replay-viewer/?case=standing144&run=A), z wyborem wariantu/zawodnika, markerami i skokami do okrążeń2/3.','',
        '![Trzy okrążenia i warianty](three-laps.svg)','',f'**Werdykt: {verdict}.**','']
    (D/'README.md').write_text('\n'.join(lines),encoding='utf-8',newline='\n')
    plots(runs,metrics)
    if OUT!=D:(OUT/'raport-144.md').write_text((D/'README.md').read_text(encoding='utf-8'),encoding='utf-8')
    history=read(D/'report-cost-history.json') if (D/'report-cost-history.json').exists() else [read(D/'report-cost.json')] if (D/'report-cost.json').exists() else []
    history.append({'CpuSeconds':time.process_time()-start,'NativeWorlds':0,'Forecasts':0,'Scope':'Read-only metrics, CSV export, plots and report.'})
    save(D/'report-cost-history.json',history);save(D/'report-cost.json',{'CpuSeconds':sum(q['CpuSeconds'] for q in history),'Passes':len(history),'NativeWorlds':0,'Forecasts':0,'Scope':'Sum of read-only formatting passes; native data not rerun.'})
    print(json.dumps({'Analysis':analysis,'Cost':cost},indent=2))
def plots(runs,metrics):
    import matplotlib;matplotlib.use('Agg')
    import matplotlib.pyplot as plt
    fig,axes=plt.subplots(3,1,figsize=(13,10),sharex=True);colors={'A':'#b5780b','B':'#d65532','F':'#008695','K1':'#9254bd'}
    for run in runs:
        if run['Name'] not in colors:continue
        samples=run['Riders'][0]['Samples'][::2];x=[reference(q) for q in samples]
        for ax,key,scale in [(axes[0],lambda q:q['Input']['Rider']['Speed'],3.6),(axes[1],lambda q:q['Input']['Active']['Drive'],100),(axes[2],lambda q:q['Input']['Rider']['SlideRadians'],180/math.pi)]:
            ax.plot(x,[key(q)*scale for q in samples],color=colors[run['Name']],label=run['Name'],lw=1)
    for ax,label in zip(axes,['Prędkość km/h','Gaz aktywny %','β stopnie']):
        ax.set_ylabel(label);ax.grid(alpha=.2)
        for lap in [1,2,3]:ax.axvline(lap*runs[0]['LapMeters'],color='#777',ls='--',lw=.6)
    axes[0].legend(ncol=4);axes[0].set_title('Start od0 · ciągła jazda przez trzy pełne okrążenia · bez resetów')
    axes[-1].set_xlabel('Metry referencyjne · pionowe linie = rzeczywiste przekroczenia L /2L /3L');fig.tight_layout()
    fig.savefig(D/'three-laps.svg');fig.savefig(OUT/'three-laps-144.png',dpi=145);plt.close(fig)
    p=D/'three-laps.svg';p.write_text('\n'.join(q.rstrip() for q in p.read_text(encoding='utf-8').splitlines())+'\n',encoding='utf-8',newline='\n')
if __name__=='__main__':main()
