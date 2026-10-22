"""Offline #127 telemetry/cost audit. Reads existing native data; never creates a world."""
import copy,csv,gzip,hashlib,importlib.util,io,json,math,struct
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2]
DATA=ROOT/'docs/experiments/first-bend-zigzag'
PRIOR=ROOT/'docs/experiments/predictive-rider-control'
def load(path):
    with gzip.open(path,'rt',encoding='utf-8') if path.suffix=='.gz' else path.open(encoding='utf-8-sig') as f:return json.load(f)
def rows(path):
    with gzip.open(path,'rt') as f:return [{k:float(v) for k,v in r.items()} for r in csv.DictReader(f)]
def save(name,value):
    text=json.dumps(value,indent=2,allow_nan=False)+'\n';(DATA/name).write_text(text,encoding='utf-8',newline='\n')
def zip_json(name,value):(DATA/name).write_bytes(gzip.compress(json.dumps(value,separators=(',',':'),allow_nan=False).encode(),mtime=0))
def wrap(x):return math.atan2(math.sin(x),math.cos(x))
def f32(x):return struct.unpack('f',struct.pack('f',x))[0]
def point(v,w,arm):return [v[0]-w*arm[1],v[1]+w*arm[0]]
def dot(a,b):return sum(x*y for x,y in zip(a,b))
def crossing(rr,key,start,end):
    return [dict(Before=a['t'],After=b['t'],Estimate=a['t']+(b['t']-a['t'])*abs(a[key])/(abs(a[key])+abs(b[key])),BeforeValue=a[key],AfterValue=b[key])
        for a,b in zip(rr,rr[1:]) if start<=b['t']<=end and a[key]*b[key]<0]
def extrema(rr,key,start,end):
    out=[]
    for a,b,c in zip(rr,rr[1:],rr[2:]):
        if start<=b['t']<=end and (b[key]-a[key])*(c[key]-b[key])<0:out.append(dict(Time=b['t'],Value=b[key]))
    return out
def build_native(name,stem):
    rr=rows(PRIOR/(stem+'.csv.gz'));pp=rows(PRIOR/(stem+'-plan.csv.gz'));run=load(PRIOR/(stem+'.json'))
    final=run['Result']['Riders'][0];end=dict(t=run['Result']['TimeSeconds'],x=final['Position']['X'],y=final['Position']['Y'],vx=final['Velocity']['X'],vy=final['Velocity']['Y'],body_omega=final['AngularVelocity'],heading=final['Heading'])
    result=[];psi=chi=0
    for i,(r,p,n) in enumerate(zip(rr,pp,rr[1:]+[end])):
        psi+=wrap(r['heading']-(rr[i-1]['heading'] if i else 0));chi+=wrap(r['velocity_heading']-(rr[i-1]['velocity_heading'] if i else 0))
        h=r['dt'];mid=[(r['vx']+n['vx'])/2,(r['vy']+n['vy'])/2];spin=(r['body_omega']+n['body_omega'])/2
        vf=point(mid,spin,[r['front_arm_x'],r['front_arm_y']]);vr=point(mid,spin,[r['rear_arm_x'],r['rear_arm_y']])
        front=-dot([r['front_fx'],r['front_fy']],vf)*h
        rear_work=dot([r['rear_fx'],r['rear_fy']],vr)*h;u=dot(vr,[f32(math.cos(r['heading'])),f32(math.sin(r['heading']))])
        rear_lateral=-(rear_work-r['rear_longitudinal']*u*h)
        rear_long=r['rear_longitudinal']*(.33*(r['omega_before']+r['omega_after'])/2-u)*h
        energy=dict(Engine=r['engine_work'],Front=front,RearLateral=rear_lateral,RearLongitudinal=rear_long,Bearing=r['bearing_loss'],Resistance=-dot([r['resistance_x'],r['resistance_y']],mid)*h)
        result.append(dict(t=r['t'],dt=h,s=r['s'],x=r['x'],y=r['y'],psi=psi*180/math.pi,chi=chi*180/math.pi,beta=r['beta']*180/math.pi,
            yaw=r['body_omega'],yaw_acceleration=(n['body_omega']-r['body_omega'])/h,steering=r['steer_before']*180/math.pi,request=r['steer_request']*180/math.pi,
            steering_after=r['steer_after']*180/math.pi,chi_dot=wrap(n.get('velocity_heading',math.atan2(n['vy'],n['vx']))-r['velocity_heading'])/h,
            desired_chi_dot=p['desired_chi_dot'],eta=wrap(r['velocity_heading']-p['path_heading'])*180/math.pi,error=p['normal_error'],path_x=p['path_x'],path_y=p['path_y'],path_heading=p['path_heading']*180/math.pi,
            speed=r['speed']*3.6,drive=r['drive']*100,front_force=r['front_lateral'],rear_force=r['rear_lateral'],rear_longitudinal=r['rear_longitudinal'],front_capacity=p['front_capacity'],rear_capacity=p['rear_capacity'],
            front_use=abs(r['front_lateral'])/p['front_capacity'],rear_use=math.hypot(r['rear_lateral'],r['rear_longitudinal'])/p['rear_capacity'],torque=r['front_torque']+r['rear_torque'],
            tyre_loss=(front+rear_lateral+rear_long)/h,engine_power=r['engine_work']/h,energy=energy))
    for i,r in enumerate(result):
        before=result[max(0,i-1)];after=result[min(len(result)-1,i+1)];r['error_rate']=(after['error']-before['error'])/(after['t']-before['t'])
    # Data-driven extrema and native zero-crossing brackets, never inferred from steering signs alone.
    turns=extrema(result,'error',1.2,10.3);significant=[]
    for turn in turns:
        nearest=lambda t:min(result,key=lambda r:abs(r['t']-t))
        if min(abs(nearest(turn['Time']-.1)['error']-turn['Value']),abs(nearest(turn['Time']+.1)['error']-turn['Value']))>=.01:significant.append(turn)
    ec=crossing(result,'error',1.2,10.3);eta_c=crossing(result,'eta',1.2,10.3)
    excursions=[]
    for a,b in zip(ec,ec[1:]):
        sub=[r for r in result if a['After']<=r['t']<=b['Before']];peak=max(sub,key=lambda r:abs(r['error']))
        excursions.append(dict(StartBracket=[a['Before'],a['After']],EndBracket=[b['Before'],b['After']],PeakTime=peak['t'],PeakError=peak['error'],
            PeakAbsEta=max(abs(r['eta']) for r in sub),ReturnToLineSeconds=b['After']-peak['t'],StableRecoveryClaim=False))
    lobes=[]
    for a,b in zip(eta_c,eta_c[1:]):
        sub=[r for r in result if a['After']<=r['t']<=b['Before']];peak=max(sub,key=lambda r:abs(r['eta']))
        if abs(peak['eta'])>=1 and b['After']-a['After']>=.1:lobes.append(dict(Start=a['After'],End=b['After'],PeakTime=peak['t'],PeakDegrees=peak['eta']))
    phases={};geo=load(ROOT/'docs/experiments/progressive-bend-width/progressive-b-geometry.json')
    for i,phase in enumerate(['Entry','Middle','Exit'],1):
        start=geo['segments'][i]['startReferenceMeters'];end=geo['segments'][i+1]['startReferenceMeters'];sub=[r for r in result if start<=r['s']<end]
        phases[phase]=dict(Start=sub[0]['t'],End=sub[-1]['t']+sub[-1]['dt'],EnergyJoules={k:sum(r['energy'][k] for r in sub) for k in sub[0]['energy']},MinimumSpeed=min(r['speed'] for r in sub))
    meta=dict(ErrorZeroCrossings=ec,ErrorTurningPoints=significant,RawErrorTurningPoints=turns,EtaZeroCrossings=eta_c,EtaExtrema=extrema(result,'eta',1.2,10.3),
        NativeYawZeroCrossings=crossing(result,'yaw',1.2,10.3),NativeChiRateZeroCrossings=crossing(result,'chi_dot',1.2,10.3),
        PhysicalLineExcursions=excursions,ResolvedDirectionLobes=lobes,MaximumTangentErrorDegrees=max(abs(r['eta']) for r in result if 1.2<=r['t']<=10.3),Phases=phases,
        RequestJumps=[dict(Time=b['t'],Before=a['request'],After=b['request'],Drive=b['drive'],Yaw=b['yaw'],ChiDot=b['chi_dot'],Error=b['error']) for a,b in zip(result,result[1:]) if 1.2<=b['t']<=10.3 and abs(b['request']-a['request'])>=5])
    return result,meta
def analyze():
    DATA.mkdir(exist_ok=True);series={};native={}
    for name,stem in [('baseline','baseline'),('candidate','controlled/candidate')]:series[name],native[name]=build_native(name,stem)
    contacts=load(DATA/'same-state-contacts.json.gz');matrix=load(DATA/'execution-and-cost.json');events=load(PRIOR/'controlled/candidate-steering-events.json')
    pulses=[]
    for event in events:
        at=event['ActualActivation'];end=at+.1;sub=[r for r in contacts['Records'] if at-1e-8<=r['Time']<end-1e-8]
        release=next(r for r in contacts['Records'] if abs(r['Time']-end)<1e-8)
        native_sub=[r for r in series['candidate'] if at-1e-8<=r['t']<end-1e-8]
        start=next(r for r in series['candidate'] if abs(r['t']-at)<1e-8);finish=next(r for r in series['candidate'] if abs(r['t']-end)<1e-8)
        pulses.append(dict(Observed=event['ObservedAt'],Activation=at,Release=end,HeldTargetDegrees=event['TargetRadians']*180/math.pi,Intervals=len(sub),Suppressed117Seconds=sum(r['Dt'] for r in sub),
            EffectiveRequestBiasRangeDegrees=[min((r['Actual']['SteeringRequest']-r['Original']['SteeringRequest'])*180/math.pi for r in sub),max((r['Actual']['SteeringRequest']-r['Original']['SteeringRequest'])*180/math.pi for r in sub)],
            ReleaseRequestedJumpDegrees=(release['Actual']['SteeringRequest']-sub[-1]['Actual']['SteeringRequest'])*180/math.pi,
            PeakAbsChiRateDeviation=max(abs(r['Actual']['Motion']['ChiDot']-r['Original']['Motion']['ChiDot']) for r in sub),
            PeakAbsTorqueDeviation=max(abs(r['Actual']['NetTorque']-r['Original']['NetTorque']) for r in sub),
            Start=start,Finish=finish,NativeDeltaChi=finish['chi']-start['chi'],NativeDeltaPsi=finish['psi']-start['psi'],NativeDeltaBeta=finish['beta']-start['beta']))
    # Decomposed scores must reconstruct every frozen #126 decision, including reversal memory.
    score_error=0;rankings=[];tradeoffs=[]
    for record in matrix['Records']:
        at=record['ObservedAt'];comparisons=record['Comparisons'];lookup={(r['Horizon'],r['Variant']):r['Summary'] for r in comparisons}
        for label,sign in [('A',0),('B+',1),('B-',-1)]:
            stored=next(r['Score'] for r in record['StoredCosts'] if (0 if r['CorrectionRadians']==0 else math.copysign(1,r['CorrectionRadians']))==sign)
            score_error=max(score_error,abs(stored-lookup[.6,label]['Total']))
        rankings.append(dict(Observed=at,Best06=min(['A','B+','B-'],key=lambda k:lookup[.6,k]['Total']),Best09=min(['A','B+','B-'],key=lambda k:lookup[.9,k]['Total'])))
        for horizon in [.6,.9]:
            a,b,c=lookup[horizon,'A'],lookup[horizon,'B-'],lookup[horizon,'C-']
            tradeoffs.append(dict(Observed=at,Horizon=horizon,
                BMinusA={k:b[k]-a[k] for k in ['Total','Tracking','Heading','EndBetaGrowth','Progress','DistanceXY','SpeedChange','PeakAbsBeta','FinalAbsBeta','PeakAbsYawRate','PeakAbsTangentError']},
                CMinusB={k:c[k]-b[k] for k in ['Total','Tracking','Heading','EndBetaGrowth','Progress','DistanceXY','SpeedChange','PeakAbsBeta','FinalAbsBeta','PeakAbsYawRate','PeakAbsTangentError']}))
    assert score_error<1e-8
    default=load(DATA/'default-model-validation.json');assert len(default['Records'])==63 and all(r['Passed'] for r in default['Records'])
    assert all(r[k]==0 for r in matrix['DefaultEquality'] for k in ['Position','Steering','Drive'])
    saved_decisions=load(PRIOR/'controlled/candidate-predictions.json.gz');full_default_checks=0;default_time_error=0
    for forecast in load(DATA/'offline-forecasts.json.gz'):
        if forecast['Horizon']!=.6 or forecast['Variant']=='C-':continue
        original=next(d for d in saved_decisions if abs(d['ObservedAt']-forecast['ObservedAt'])<1e-8)
        sign={'A':0,'B+':1,'B-':-1}[forecast['Variant']]
        selected=next(c for c in original['Candidates'] if (0 if c['CorrectionRadians']==0 else math.copysign(1,c['CorrectionRadians']))==sign)
        current,old=copy.deepcopy(forecast['Prediction']),copy.deepcopy(selected['Prediction'])
        for a,b in zip(current['Points'],old['Points']):
            default_time_error=max(default_time_error,abs(a['Time']-b['Time']))
            assert abs(a['Time']-b['Time'])<1e-12
            a.pop('Time');b.pop('Time')
        # The legacy CSV reader initializes these two unused metadata fields instead
        # of exporting them. Never rewrite the evidence or call them physical errors.
        for prediction in [current,old]:
            for point in prediction['Points']:
                point['Rider'].pop('DistanceMeters');point['Rider'].pop('Context')
        assert current==old,'Default physical prediction changed'
        full_default_checks+=1
    assert full_default_checks==30
    result=dict(Grade='PARTIAL',NewNativeWorlds=0,NewRuntimeController=False,BetaConvention='psi-chi',Native=native,Pulses=pulses,
        Original117SuppressedIntervals=contacts['Suppressed117Intervals'],MaximumNativeForceReplayError=contacts['MaximumNativeReplayError'],DefaultPhysicalPredictionEquality=full_default_checks,
        MaximumDefaultTimeRoundoff=default_time_error,ExcludedMetadataFromDefaultEquality=['Rider.DistanceMeters: not in native CSV, legacy reader initializes0; unused by this model','Rider.Context: legacy reader initializes2, native recorder0; unused in isolated forces/policy'],
        MaximumFrozenScoreReconstructionError=score_error,HorizonRankings=rankings,Tradeoffs=tradeoffs,
        DefaultModelValidation=dict(Forecasts=63,Passed=True,MaximumErrors={k:max(r[k] for r in default['Records']) for k in ['Position','Speed','ChiDegrees','BetaDegrees','SteeringDegrees','WheelOmega']}),
        BiasGate='Not passed: execution bias is closer to original control but has mixed peak-beta/speed/tracking effects. Native #117 already has the large weave before the late pulses. No unique isolated correction is justified.',
        CounterfactualLimits='C is offline only. Each forecast has at most0.9s and one delayed pulse, then resumes #117. Same-state contacts use the full continuous accepted native stream but do not claim an independently executed C trajectory. No later recorded steering/throttle commands are supplied as forecast inputs.',
        NextMeasurement='One predeclared native ablation with only the first two steering pulses disabled, later #126 policy and shared Q2 unchanged, to isolate their contribution to the4.2-6.8s weave; not run in this audit.')
    save('analysis.json',result)
    # Compact native-only telemetry for the existing viewer. Offline predictions stay in separate files.
    zip_json('native-telemetry.json.gz',dict(Provenance='Recorded #124/#126 Box2D only; no predicted poses',BetaConvention='psi-chi',Series=series,Activations=events,
        Windows=[dict(Id='main',Label='Główny zygzak · 3,9–9,2 s',Start=3.9,End=9.2),dict(Id='entry',Label='Pierwsze impulsy · 1,2–2,5 s',Start=1.2,End=2.5),dict(Id='pulses',Label='Seria impulsów · 5,0–8,0 s',Start=5,End=8),dict(Id='exit',Label='Wyjście · 8,0–10,3 s',Start=8,End=10.3),dict(Id='later',Label='Pomocniczo · 10,4–11,2 s',Start=10.4,End=11.2)],
        Markers=[dict(Time=4.15,Label='Przed późnymi impulsami: #117 żąda +35°'),dict(Time=5.51,Label='Yaw przechodzi przez zero'),dict(Time=5.7,Label='Rzeczywiste χ zaczyna maleć'),dict(Time=5.63,Label='η osiąga +34,18°'),dict(Time=6.43,Label='Błąd linii −5,375 m'),dict(Time=6.82,Label='χ znów rośnie'),dict(Time=7.15,Label='η osiąga −16,32°')]))
    print(json.dumps(dict(Grade=result['Grade'],NewNativeWorlds=0,ScoreError=score_error,RankingChanges=sum(r['Best06']!=r['Best09'] for r in rankings),Suppressed117=contacts['Suppressed117Intervals']),indent=2))
    for name in native:print(name,json.dumps({k:native[name][k] for k in ['ErrorTurningPoints','MaximumTangentErrorDegrees','RequestJumps']}))
    print('pulses',json.dumps([{k:r[k] for k in ['Activation','Release','EffectiveRequestBiasRangeDegrees','ReleaseRequestedJumpDegrees']} for r in pulses]))
    detailed_reports(result,matrix)
    return result,series

def detailed_reports(result,matrix):
    def table(headers,rr):return '| '+' | '.join(headers)+' |\n| '+' | '.join(['---']*len(headers))+' |\n'+''.join('| '+' | '.join(map(str,r))+' |\n' for r in rr)+'\n'
    def f(x,n=6):return f'{x:.{n}f}'
    out=['# Wszystkie10 decyzji #126 — rozkład kosztu i fizyczne skutki prognoz\n',
        'OFFLINE. A = #117; B+ / B− = zamrożony cel #126; C− = bias bieżącego żądania #117. Jedna akcja po0,24s przez0,1s, potem #117. Każda prognoza jest ciągła, lecz osobna; nie stanowi pełnej natywnej jazdy C. Brak przyszłych zapisanych poleceń w wejściu. Jednostka składników kosztu: m².\n']
    csvrows=[]
    for r in matrix['Records']:
        out.append(f"\n## Obserwacja {r['ObservedAt']:.2f}s → aktywacja {r['Activation']:.2f}s\n")
        for horizon in [.6,.9]:
            sub=[c for c in r['Comparisons'] if c['Horizon']==horizon]
            out.append(f'\nHoryzont {horizon:.1f}s.\n')
            out.append(table(['Akcja','Tracking','Kierunek χ','Wzrost β koniec','Postęp','Odwrócenie korekty','Suma'],[[c['Variant']]+[f(c['Summary'][k]) for k in ['Tracking','Heading','EndBetaGrowth','Progress','Reversal','Total']] for c in sub]))
            out.append(table(['Akcja','Postęp [m]','Droga XY [m]','Δv [km/h]','Min v [km/h]','Szczyt β [°]','Koniec β [°]','Koniec błędu [m]'],[[c['Variant'],f(c['Summary']['ReferenceProgress'],4),f(c['Summary']['DistanceXY'],4),f(c['Summary']['SpeedChange']*3.6,4),f(c['Summary']['MinimumSpeed']*3.6,3),f(c['Summary']['PeakAbsBeta']*180/math.pi,4),f(c['Summary']['FinalAbsBeta']*180/math.pi,4),f(c['Summary']['FinalNormalError'],4)] for c in sub]))
            for c in sub:csvrows.append(dict(Observed=r['ObservedAt'],Activation=r['Activation'],Horizon=horizon,Variant=c['Variant'],**c['Summary']))
        out.append('Efekt natywny tego impulsu, siły i zwolnienie override: zobacz tę samą aktywację w [native-pulses.md](native-pulses.md). Dalsza jazda zawiera kolejne decyzje i sprzężenie gazu; nie przypisujemy jej całej temu jednemu impulsowi.\n')
    (DATA/'decision-costs.md').write_text('\n'.join(out),encoding='utf-8',newline='\n')
    with (DATA/'decision-costs.csv').open('w',encoding='utf-8',newline='') as file:
        writer=csv.DictWriter(file,fieldnames=list(csvrows[0]));writer.writeheader();writer.writerows(csvrows)
    out=['# Ciągła natywna sekwencja impulsów #126\n',
        'Wszystkie wartości start/koniec pochodzą z rzeczywistego strumienia Box2D. Różnice sił A/C są zapytaniami z tego samego zaakceptowanego stanu B, nie wykonanymi trajektoriami. Zmiany native zawierają jednocześnie działanie całego układu.\n']
    out.append(table(['Obserwacja','Aktywacja','Powrót #117','Efektywny bias żądania [°]','Skok żądania przy powrocie [°]','Δψ / Δχ / Δβ native [°]'],[[f(p['Observed'],2),f(p['Activation'],2),f(p['Release'],2),f(p['EffectiveRequestBiasRangeDegrees'][0],4)+'…'+f(p['EffectiveRequestBiasRangeDegrees'][1],4),f(p['ReleaseRequestedJumpDegrees'],4),'/'.join(f(p[k],4) for k in ['NativeDeltaPsi','NativeDeltaChi','NativeDeltaBeta'])] for p in result['Pulses']]))
    for p in result['Pulses']:
        out.append(f"\n## {p['Activation']:.2f}–{p['Release']:.2f}s\n")
        out.append(table(['Pomiar','Początek','Koniec'],[[k,f(p['Start'][k],5),f(p['Finish'][k],5)] for k in ['steering','request','psi','chi','beta','yaw','yaw_acceleration','error','eta','speed','drive','front_force','rear_force','front_use','rear_use','tyre_loss','torque']]))
        out.append(f"Różnica względem natychmiastowej odpowiedzi #117 na tych samych stanach: max |Δχ̇|={p['PeakAbsChiRateDeviation']:.6f}rad/s; max |Δmoment|={p['PeakAbsTorqueDeviation']:.6f}N·m. Kąt i szybkość kierownicy pozostają fizycznie ograniczone.\n")
    (DATA/'native-pulses.md').write_text('\n'.join(out),encoding='utf-8',newline='\n')
if __name__=='__main__':analyze()
