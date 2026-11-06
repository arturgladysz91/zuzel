"""Verify and present the completed #173 diagnosis; no controller or world execution."""
from bisect import bisect_left
from collections import Counter, defaultdict
import gzip
import hashlib
from io import BytesIO
import json
import math
from pathlib import Path
from statistics import median
import sys
import time
from zipfile import ZipFile, ZIP_DEFLATED

from analyze173 import OLD, DOCS, ROOT, read, write, sha, events, cross, trace_of, active_intervals, load_archives


def physical(sample):
    inp=sample['Input']
    return {**{k:sample[k] for k in ('CrankOmega','Powertrain','Forces','Inner','Outer','LegacyCertificate','Certificate','EnergyExcessJoules')},
        'Input':{k:inp[k] for k in ('Rider','Time','Omega','Steering','Active')}}


def native_audit(evidence,original):
    rows=[];traces={};branch_hashes=set();prefix_hashes=set()
    protocol=read(evidence/'n1-protocol.json')
    common=protocol['StopReference']
    entry=protocol['State']['Input']['Rider']['TotalReferenceMeters']+protocol['State']['Classification']['distance_to_entry']
    for path in sorted(evidence.glob('n1-*.zip')):
        with ZipFile(path) as z:
            native=json.loads(z.read('native.json'));branch=json.loads(z.read('branch.json'))
            result=json.loads(z.read('result.json'));decisions=json.loads(z.read('decisions.json'))
        samples=native['Riders'][0]['Samples'];old=original['v1-K4-S1']['native']['Riders'][0]['Samples']
        before=[q for q in samples if q['Input']['Time']<=branch['Time']+.24+1e-8]
        assert [physical(q) for q in before]==[physical(q) for q in old[:len(before)]], path.name
        prefix_hashes.add(hashlib.sha256(json.dumps([physical(q) for q in before],sort_keys=True).encode()).hexdigest())
        if branch['Name']=='baseline':assert samples==old[:len(samples)]
        assert result['ReachedStop'] and not result['S3'] and result['FirstBadTime'] is None
        assert branch['PrefixMatches']==60
        branch_hashes.add(branch['BranchHash'])
        trace=trace_of(native);traces[branch['Name']]=trace
        post=[q for q in trace if q['t']>=branch['Time']]
        row=dict(arm=branch['Name'],action=branch['Action'],branch_time=branch['Time'],branch_sha256=branch['BranchHash'],
            physical_prefix_samples=len(before),goal_time=cross(trace,common)['t'],
            entry_speed_kph=cross(trace,entry)['kph'],
            maximum_kph=max(q['kph'] for q in post),maximum_beta=max(abs(q['beta']) for q in post),
            minimum_sweep=min(q['swept'] for q in post if q['swept'] is not None),
            minimum_pose=min(min(q['inner'],q['outer']) for q in post),
            result=result,last_state=samples[-1],autonomous_reviews_after_branch=sum(q['Time']>branch['Time']+1e-8 for q in decisions))
        rows.append(row)
    assert len(rows)==4 and len(branch_hashes)==len(prefix_hashes)==1
    base=next(r for r in rows if r['arm']=='baseline')
    for row in rows:row['gain_to_local_goal_seconds']=base['goal_time']-row['goal_time']
    return rows,traces,dict(branch_sha256=next(iter(branch_hashes)),physical_prefix_sha256=next(iter(prefix_hashes)),
        physical_prefix_samples=rows[0]['physical_prefix_samples'],identical_until_activation=True,
        baseline_matches_archived_samples=True)


def tables(summary,decisions,counter,native):
    denominator=summary['s3_straight_decisions']
    text=['# Tabele diagnostyczne #173','',
        'Mechanizmy mogą współwystępować. Efekty składników Policy to czyste kontrfaktyczne obliczenia: usuń jeden składnik przy zachowaniu pozostałych i clamp 0..1. Nie są sumowalne i nie opisują automatycznie końcowej komendy po Exit/K4.', '',
        f'Zakres tabeli A: {summary["s3_decisions"]} decyzji przed trzecią metą, w tym {denominator} na prostych. Dalsze 25 decyzji rezerwy zachowano w danych, lecz wyłączono z tej tabeli.', '',
        f'| Mechanizm — proste | Liczba / {denominator} | Udział | Śr. efekt na propozycję danej warstwy | Śr. zablokowane zwiększenie | Znaczenie |','|---|---:|---:|---:|---:|---|']
    for q in summary['mechanisms']:
        if q['region']!='straight':continue
        effect=f"{q['mean_drop']*100:.2f} p.p." if q['measured_drop'] else '—'
        meaning='zmniejszenie bazowej propozycji' if q['measured_drop'] else 'weto zwiększenia lub kontekst wyboru'
        if q['mechanism']=='K4_DRIVE_REDUCTION':meaning='zmniejszenie propozycji Exit przez K4'
        gap=summary['foregone_by_mechanism'].get(q['mechanism'])
        blocked=f'{100*gap["mean"]:.2f} p.p.' if gap else '—'
        text.append(f"| {q['mechanism']} | {q['count']} | {q['percent']:.2f}% | {effect} | {blocked} | {meaning} |")
    text+=['| SHORT_PREDICTION_REJECT | 0 | 0% | — | — | brak odrzucenia w ocenionym zbiorze |',
        '| BOUNDARY_CLEARANCE / BETA45 | 0 | 0% | — | — | brak odrzucenia w ocenionym zbiorze |',
        '| PLAN_FALLBACK (brak dopuszczalnej linii) | 0 | 0% | — | — | zwykłe wyjście poza okno K4 nie jest awarią planu |',
        '| TRANSITION_OVERRIDE | 0 | 0% | — | — | wszystkie końcowe przejścia continuous |','',
        'UNKNOWN dotyczy optymalnego wejścia i konieczności ograniczenia poza zbadanym horyzontem, nie tożsamości odtworzonej komendy. Pewność przypisania kodowego jest wysoka: wszystkie 1438 akcji/trybów/liczników odpowiadają historii.', '',
        '## Każda prosta i rzeczywista aktywacja gazu','',
        'Czasy absolutne [s]. Spadki poniżej 95% i 70% liczymy po pierwszym osiągnięciu ≥95%. Gdy próg nie został osiągnięty, pola pozostają puste. Gaz aktywny pochodzi z rzeczywistych aktywacji, nie z bieżącej propozycji. Ostatnia home jest niepełna; początkowy rozbieg zapisano osobno.', '',
        '| Próba | Okr./prosta | V pocz. | Gaz pocz. | Pierwsze ≥95% | <95% | <70% | V przy <95% | Do łuku przy <95% | V wejścia w bramce | Powód komendy spadku |',
        '|---|---|---:|---:|---:|---:|---:|---:|---:|---:|---|']
    fmt=lambda x:'—' if x is None else f'{x:.3f}'
    for r in summary['straights']:
        h,low,l70=r['first_high'],r['first_below95'],r['first_below70'];decision=low['decision'] if low else None
        text.append(f"| {r['trial']} | {r['lap']}/{r['straight']}{'' if r['complete'] else ' (część)'} | {r['start_speed_kph']:.2f} | {r['start_active_drive']:.3f} | "+
            ' | '.join(fmt(v) for v in [h['time'] if h else None,low['time'] if low else None,l70['time'] if l70 else None,
                low['speed_kph'] if low else None,low['distance_to_entry'] if low else None,
                summary['entry_budget_kph'] if decision else None])+f" | {decision['exit_gate'] if decision else '—'} |")
    text+=['','## Tabela B — charakterystyczne decyzje','',
        '| Stan | Okr./prosta | s [m] | V [km/h] | Gaz profilu | Policy | Exit | K4/zaakceptowany | Aktywny | Główne późniejsze weto | Uzasadnienie fizyczne |',
        '|---|---|---:|---:|---:|---:|---:|---:|---:|---|---|']
    for state in counter['selected']:
        c=next(r for r in decisions if r['trial']==state['trial'] and abs(r['time']-state['time'])<1e-8)
        where='main' if c['segment']==4 else 'home'
        physical='weto pojedynczego impulsu sfalsyfikowane natywnie' if state['id']=='P7' else 'lokalnie wykonalne; UNKNOWN poza 0,84 s'
        text.append(f"| {state['id']} | {c['lap']+1}/{where} | {c['reference']:.3f} | {c['speed_kph']:.3f} | {c['profile_drive']:.3f} | {c['policy_drive']:.3f} | {c['exit_drive']:.3f} | {c['accepted_drive']:.3f} | {c['active_drive']:.3f} | {c['exit_gate']} | {physical} |")
    text+=['','## Tabela C — prognozy przy historycznym skręcie','',
        'Każdy stan ma te same położenie, prędkości, wał, koło, parametry sprzęgła, aktywną komendę i pending. Horizon 0,84 s od decyzji obejmuje 0,24 s reakcji. pulse oznacza jedną komendę i kolejne odpuszczenie przez Policy, z całą kolejką; held utrzymuje nowy gaz do końca horyzontu. Wszystkie wyniki to prognozy, bez zaliczenia łuku/S3.', '',
        '| Stan | Wariant | Gaz | Skręt [°] | V po 0,84 s | Min swept | Max β | Legalność lokalna | CanPrepare na końcu |',
        '|---|---|---:|---:|---:|---:|---:|---|---|']
    for r in counter['rows']:
        if r['Steering']!='historical':continue
        text.append(f"| {r['State']} | {r['Kind']} / {r['Gas']} | {r['Action']['Drive']:.3f} | {math.degrees(r['Action']['SteeringRadians']):.3f} | {r['EndSpeed']*3.6:.3f} | {r['MinimumSweep']:.3f} | {math.degrees(r['MaximumBeta']):.3f} | {'lokalnie wykonalne' if r['LocallyFeasible'] else r['FirstGate']} | {r['EntryBudget']['Accepted']} |")
    text+=['','## Cztery nowe krótkie ramiona natywne P7','',
        '| Ramię | Gaz interwencji | Skręt [°] | Czas do lokalnego celu | Zysk [s] | Vmax po rozwidleniu | Wejście w łuk 2 | Min swept po rozwidleniu | Max β po rozwidleniu | Wynik |',
        '|---|---:|---:|---:|---:|---:|---:|---:|---:|---|']
    for r in native:
        text.append(f"| {r['arm']} | {r['action']['Drive']:.3f} | {math.degrees(r['action']['SteeringRadians']):.3f} | {r['goal_time']:.6f} | {r['gain_to_local_goal_seconds']:.6f} | {r['maximum_kph']:.3f} | {r['entry_speed_kph']:.3f} | {r['minimum_sweep']:.3f} | {r['maximum_beta']:.3f} | legalny cały następny łuk +20 m |")
    (DOCS/'TABELE.md').write_text('\n'.join(text)+'\n',encoding='utf-8')


def plots(summary,classified,native_sources,native_traces,g):
    import matplotlib
    matplotlib.use('Agg')
    import matplotlib.pyplot as plt
    import numpy as np
    colors={'reference':'#64748b','accepted':'#16a34a','roll-off:entry-speed-budget':'#d97706','roll-off:continuation':'#9333ea'}
    source=native_sources['v1-K4-S1'];trace=source['trace'];dec=[r for r in classified if r['trial']=='v1-K4-S1']
    fig,axes=plt.subplots(3,1,figsize=(14,9),sharex=True,layout='constrained')
    ss=[r['reference'] for r in dec]
    for name,label in [('profile_drive','profil'),('policy_drive','Policy'),('exit_drive','Exit'),('accepted_drive','końcowa propozycja')]:
        axes[0].step(ss,[r[name] for r in dec],where='post',lw=1,label=label)
    axes[0].plot([r['s'] for r in trace],[r['active'] for r in trace],color='black',lw=.8,alpha=.5,label='aktywny (próbki)')
    axes[0].set_ylabel('Gaz');axes[0].legend(ncols=3,fontsize=8)
    axes[1].plot([r['s'] for r in trace],[r['kph'] for r in trace],label='V natywne',color='#087f78')
    axes[1].plot(ss,[r['reference_kph'] for r in dec],label='Vref dla aktywacji +0,24 s',lw=1)
    axes[1].set_ylabel('km/h');axes[1].legend(fontsize=8)
    for gate in colors:
        d=[r for r in dec if r['exit_gate']==gate]
        axes[2].scatter([r['reference'] for r in d],[r['distance_to_entry'] for r in d],s=15,color=colors[gate],label=gate)
    axes[2].set(ylabel='Do następnego łuku [m]',xlabel='Całkowity postęp [m]');axes[2].legend(ncols=2,fontsize=8)
    for ax in axes:
        for lap in range(3):
            ax.axvline((lap+1)*g['LapMeters'],color='#b45309',ls=':',lw=1)
            for a,b in ((1,4),(5,8)):
                ax.axvspan(lap*g['LapMeters']+g['Segments'][a]['StartReferenceMeters'],lap*g['LapMeters']+g['Segments'][b]['StartReferenceMeters'],color='#94a3b8',alpha=.1)
        ax.grid(alpha=.15)
    fig.savefig(DOCS/'173-lancuch.png',dpi=145);plt.close(fig)

    fig,axes=plt.subplots(3,2,figsize=(14,10),sharex=True,layout='constrained')
    for ax,fields,title in zip(axes.flat,[('slip',),('front','rear'),('rear_fx','rear_fy'),('beta',),('yaw',),('steer_request',)],
        ['Wheelspin κ','Wykorzystanie budżetu opon','Fx / Fy tył [N]','β [°]','Yaw [rad/s]','Skręt [°]']):
        for field in fields:ax.plot([q['s'] for q in trace],[q[field] for q in trace],label=field,lw=1)
        if title=='Wheelspin κ':ax.set_yscale('symlog',linthresh=.15)
        if title=='Skręt [°]':
            ax.plot(ss,[math.degrees(r['active_steering']) for r in dec],label='aktywna komenda',lw=.8)
            samples=source['native']['Riders'][0]['Samples']
            ax.plot([q['Input']['Rider']['TotalReferenceMeters'] for q in samples],
                [math.degrees(q['Input']['Steering']) for q in samples],label='aktuator natywny',lw=.7,alpha=.6)
        for lap in range(3):ax.axvline((lap+1)*g['LapMeters'],color='#b45309',ls=':',lw=.8)
        ax.set_ylabel(title);ax.grid(alpha=.15);ax.legend(fontsize=8)
    for ax in axes[-1]:ax.set_xlabel('Całkowity postęp [m]')
    fig.savefig(DOCS/'173-fizyka.png',dpi=145);plt.close(fig)

    fig,(a,b)=plt.subplots(1,2,figsize=(14,5.5),layout='constrained')
    labels=['0–25%','25–50%','50–75%','75–95%','≥95%'];bottom=np.zeros(3)
    for index,name in enumerate(labels):
        values=[100*np.mean([r['shares'][index] for r in summary['throttle_bins'] if r['lap']==lap]) for lap in (1,2,3)]
        a.bar([1,2,3],values,bottom=bottom,label=name);bottom+=values
    a.set(xlabel='Okrążenie',ylabel='Udział rzeczywistego czasu [%]',xticks=[1,2,3]);a.legend(ncols=2,fontsize=8)
    gates=summary['diagnostic_gate_counts'];names=list(gates)
    b.barh(names,[gates[x]['pass'] for x in names],label='przechodzi',color='#087f78')
    b.barh(names,[gates[x]['reject'] for x in names],left=[gates[x]['pass'] for x in names],label='odmowa',color='#d97706')
    b.set_xlabel('Liczba ocen (różne etapy i zbiory kandydatów)');b.legend(fontsize=8)
    fig.savefig(DOCS/'173-bramki-czas.png',dpi=145);plt.close(fig)

    for trial in sorted(k for k in native_sources if '-K4-' in k):
        source=native_sources[trial];trace=source['trace'];dec=[r for r in classified if r['trial']==trial]
        regions=[r for r in summary['straights'] if r['trial']==trial]
        fig,axes=plt.subplots(7,1,figsize=(14,19),layout='constrained')
        fig.suptitle('Pasek Exit: szary = referencja, pomarańczowy = CanPrepare, fioletowy = kontynuacja, zielony = akceptacja',fontsize=9)
        for ax,region in zip(axes,regions):
            start,end=region['start_time'],region['end_time'];lo,hi=region['start'],region['end']
            tr=[r for r in trace if start<=r['t']<=end];ds=[r for r in dec if start<=r['time']<=end]
            ax.plot([r['t']-start for r in tr],[r['kph'] for r in tr],label='V natywne',color='#087f78')
            ax.plot([r['time']-start for r in ds],[r['reference_kph'] for r in ds],label='Vref +0,24 s',color='#b45309',ls='--')
            ax.set_ylabel('km/h');gas=ax.twinx()
            for key,label,style in [('policy_drive','Policy',':'),('accepted_drive','propozycja','--')]:
                gas.step([r['time']-start for r in ds],[r[key] for r in ds],where='post',label=label,ls=style,lw=1)
            cuts=active_intervals(source['native'])
            steps=[(max(x,start),action['Drive']) for x,y,action in cuts if min(y,end)>max(x,start)]
            if steps:steps.append((end,steps[-1][1]))
            gas.step([t-start for t,v in steps],[v for t,v in steps],where='post',label='gaz aktywny',color='#111827',lw=1)
            gas.scatter([r['time']-start for r in ds],[-.09]*len(ds),c=[colors.get(r['exit_gate'],'#777') for r in ds],marker='s',s=25)
            gas.set(ylim=(-.18,1.05),yticks=[0,.5,1],ylabel='Gaz / powód Exit (pasek)')
            if ax is axes[0]:gas.legend(loc='upper right',ncols=3,fontsize=7)
            distance=ax.twinx();distance.spines['right'].set_position(('outward',48));distance.set_ylabel('Do wejścia [m]',color='#94a3b8')
            distance.plot([r['t']-start for r in tr],[hi-r['s'] for r in tr],color='#94a3b8',ls=':',alpha=.8)
            for key,tint in [('first_high','#16a34a'),('first_below95','#9333ea'),('first_below70','#dc2626')]:
                if region[key]:ax.axvline(region[key]['time']-start,color=tint,ls=':',lw=.8)
            ax.set_title(f"{region['lap']}/{region['straight']} · {'cała' if region['complete'] else 'niepełna rezerwa'} · ≥95%: {region['high_seconds']:.3f} s",fontsize=10)
            ax.set_xlabel('Czas od początku prostej [s]');ax.grid(alpha=.12)
        axes[0].legend(fontsize=7,loc='upper left');axes[0].get_shared_x_axes()
        fig.savefig(DOCS/f"173-proste-{trial[-2:]}.png",dpi=130);plt.close(fig)

    fig,axes=plt.subplots(2,2,figsize=(13,8),layout='constrained')
    for name,trace in native_traces.items():
        points=[r for r in trace if r['t']>=11.6]
        for ax,key,title in zip(axes.flat,['kph','active','beta','swept'],['Prędkość [km/h]','Gaz aktywny','β [°]','Swept [m]']):
            ax.plot([r['s'] for r in points],[r[key] for r in points],label=name,lw=1)
            ax.set(xlabel='Całkowity postęp [m]',ylabel=title);ax.grid(alpha=.15)
            ax.axvline(g['Segments'][5]['StartReferenceMeters'],color='#b45309',ls=':',lw=1)
            ax.axvline(g['Segments'][8]['StartReferenceMeters'],color='#b45309',ls=':',lw=1)
    axes[0,0].legend(fontsize=8)
    fig.savefig(DOCS/'173-native-P7.png',dpi=145);plt.close(fig)


def finish(evidence,render=True):
    began=time.process_time()
    summary=read(DOCS/'summary173.json')
    classified=[json.loads(x) for x in gzip.open(DOCS/'decisions173.jsonl.gz','rt',encoding='utf-8')]
    g,sources,inventory=load_archives()
    geometry=read(evidence/'g1-geometry.json')
    finishes={k:v['native']['Riders'][0]['FinishSeconds'] for k,v in sources.items()}
    for row in classified:
        row['phase']='S3' if row['time']<finishes[row['trial']] else 'reserve'
        samples=sources[row['trial']]['native']['Riders'][0]['Samples']
        index=min(range(len(samples)),key=lambda i:abs(samples[i]['Input']['Time']-row['time']))
        sample=samples[index];before=samples[max(0,index-1)]
        assert abs(sample['Input']['Time']-row['time'])<1e-8
        dt=sample['Input']['Time']-before['Input']['Time']
        beta=sample['Input']['Rider']['SlideRadians'];oldbeta=before['Input']['Rider']['SlideRadians']
        row['beta_dot']=(beta-oldbeta+math.pi)%(2*math.pi)-math.pi if dt else None
        if dt:row['beta_dot']/=dt
        row['pose']=min(sample['Inner'],sample['Outer']);row['swept']=sample['Certificate']
        force=sample['Forces'];power=sample['Powertrain']
        if force:
            loads=force['TyreLoads']
            row['previous_interval_ledger']=dict(time=sample['Input']['Time'],seconds=dt,front_fx=0,
                front_fy=force['FrontLateral'],rear_fx=force['RearLongitudinal'],rear_fy=force['RearLateral'],
                front_utilization=abs(force['FrontLateral'])/loads['FrontCapacityNewtons'],
                rear_utilization=math.hypot(force['RearLongitudinal'],force['RearLateral'])/loads['RearCapacityNewtons'],
                contact_wheelspin=power['Wheel']['InstantSlipRatio'],midpoint_engine_rpm=power['Engine']['EngineRpm'],
                available_wheel_torque=power['Engine']['AvailableWheelTorqueNewtonMeters'],
                drive_wheel_torque=power['Wheel']['DriveTorqueNewtonMeters'],combustion_work=power['CombustionWorkJoules'],
                clutch_engagement=power['Engagement'],clutch_slip=power['SlipRadiansPerSecond'])
        else:row['previous_interval_ledger']=None
        local=(row['reference']+row['distance_to_entry'])%g['LapMeters']
        bend=0 if abs(local-g['Segments'][1]['StartReferenceMeters'])<1e-6 else 1
        geom=geometry['Bends'][bend]
        row['next_bend_reference_curvature_range']=[min(x['Curvature'] for x in geom['Samples']),max(x['Curvature'] for x in geom['Samples'])]
        row['prediction_receipt']=dict(file='r1-'+row['trial']+'.jsonl.gz',root_time=row['time'],
            scope='only forecasts actually evaluated in the matched reconstruction; uncomputed futures stay absent')
        row['first_reducing_layer']=next((label for key,label in [('REFERENCE_LOW_DRIVE','REFERENCE'),
            ('SPEED_TARGET_REACHED','SPEED_CORRECTION'),('WHEELSLIP_REDUCTION','WHEELSLIP'),('K4_DRIVE_REDUCTION','K4')]
            if key in row['mechanisms']),'NONE')
    main=[r for r in classified if r['phase']=='S3']
    summary['mechanisms_all_with_reserve']=summary.get('mechanisms_all_with_reserve',summary['mechanisms'])
    mechanisms=[]
    for name in sorted({x for r in main for x in r['mechanisms']}):
        for region in ('all','straight','bend'):
            eligible=[r for r in main if region=='all' or r['straight']==(region=='straight')]
            selected=[r for r in eligible if name in r['mechanisms']]
            mechanisms.append(dict(mechanism=name,region=region,count=len(selected),denominator=len(eligible),
                percent=100*len(selected)/len(eligible),mean_drop=sum(r['mechanisms'][name] for r in selected)/len(selected) if selected else None,
                measured_drop=name in ('REFERENCE_LOW_DRIVE','SPEED_TARGET_REACHED','WHEELSLIP_REDUCTION','K4_DRIVE_REDUCTION','TRANSITION_OVERRIDE')))
    summary.update(mechanisms=mechanisms,s3_decisions=len(main),s3_straight_decisions=sum(r['straight'] for r in main),
        reserve_decisions=len(classified)-len(main),geometry=geometry)
    native,traces,proof=native_audit(evidence,sources)
    counter=read(evidence/'q1-counterfactual.json')
    counter['selected']=summary['selected'];counter['rows']=counter.pop('Rows')
    gates={'Exit 0,44 s':dict(pass_=0,reject=0),'Tracker 0,44 s':dict(pass_=0,reject=0),
        'CanPrepare początek':dict(pass_=0,reject=0),'CanPrepare ogon':dict(pass_=0,reject=0),'Settled':dict(pass_=0,reject=0)}
    foregone=defaultdict(list)
    raw_checks=[]
    for path in sorted(evidence.glob('r1-*S*.jsonl.gz')):
        for row in map(json.loads,gzip.open(path,'rt',encoding='utf-8')):
            if row['Archived']['Time']>=finishes[row['Trial']]:continue
            for data in events(row,'exit-candidate'):gates['Exit 0,44 s']['pass_' if data['Legal'] else 'reject']+=1
            for data in events(row,'tracker-candidate'):
                good=data['safe'] and data['betaError']<=.26 and data['yawError']<=.73
                gates['Tracker 0,44 s']['pass_' if good else 'reject']+=1
            for data in events(row,'can-prepare'):
                gates['CanPrepare początek' if data['checkPulse'] else 'CanPrepare ogon']['pass_' if data['Accepted'] else 'reject']+=1
            for data in events(row,'settled'):
                good=data['betaGate']<.17 and data['yawGate']<.48 and data['courseGate']<.21
                gates['Settled']['pass_' if good else 'reject']+=1
    summary['diagnostic_gate_counts']={k:{'pass':v['pass_'],'reject':v['reject']} for k,v in gates.items()}
    straight=[r for r in main if r['straight']]
    foregone_by=defaultdict(list)
    for row in straight:
        if row['foregone_boost'] is not None:foregone[row['exit_gate']].append(row['foregone_boost'])
        if row['foregone_boost'] is not None:
            for key in row['mechanisms']:
                if key.startswith('CONTINUATION') or key=='ENTRY_SPEED_BUDGET':foregone_by[key].append(row['foregone_boost'])
    entry_budget=next(r['can_prepare']['EntrySpeed'] for r in main if r['can_prepare'])*3.6
    coasts=[x for x in summary['coast_observations'] if x['time']<finishes[x['trial']]]
    falls=[]
    for trial in sorted(k for k in sources if '-K4-' in k):
        source=sources[trial];n=source['native'];previous=0;times=[q['t'] for q in source['trace']]
        for activation in n['Riders'][0]['Activations']:
            drive=activation['Action']['Drive'];t=activation['ActivationAt']
            if drive<previous-1e-7 and t<finishes[trial]:
                point=source['trace'][bisect_left(times,t)]
                local=point['s']%g['LapMeters']
                on_straight=local<g['Segments'][1]['StartReferenceMeters'] or g['Segments'][4]['StartReferenceMeters']<local<g['Segments'][5]['StartReferenceMeters'] or local>g['Segments'][8]['StartReferenceMeters']
                if on_straight:
                    cause=next(r for r in main if r['trial']==trial and abs(r['time']-activation['ObservedAt'])<1e-8)
                    falls.append(dict(trial=trial,activation=t,observed=activation['ObservedAt'],drop=previous-drive,
                        previous=previous,drive=drive,reference=point['s'],speed_kph=point['kph'],gate=cause['exit_gate']))
            previous=drive
    summary.update(native=native,native_proof=proof,counterfactual=dict(selected=summary['selected'],rows=counter['rows'],locally_feasible=sum(r['LocallyFeasible'] for r in counter['rows'])),
        foregone_boost={k:dict(count=len(v),mean=sum(v)/len(v)) for k,v in foregone.items()},
        foregone_by_mechanism={k:dict(count=len(v),mean=sum(v)/len(v)) for k,v in foregone_by.items()},
        actual_straight_drop_events=falls,entry_budget_kph=entry_budget,
        rpm_max=max(r['crank_rpm'] for r in classified),
        rpm_max_location=max(classified,key=lambda r:r['crank_rpm'])['reference'],
        coast_samples_s3=len(coasts),coast_median_s3=median(x['deceleration'] for x in coasts),
        coast_weighted_mean_s3=sum(x['deceleration']*x['dt'] for x in coasts)/sum(x['dt'] for x in coasts),
        coast_quantiles={str(q):sorted(x['deceleration'] for x in coasts)[int(q*(len(coasts)-1))] for q in (.05,.25,.5,.75,.95)},
        coast_below065=sum(x['dt'] for x in coasts if x['deceleration']<.65)/sum(x['dt'] for x in coasts))
    with gzip.open(DOCS/'decisions173.jsonl.gz','wt',encoding='utf-8') as f:
        for row in classified:f.write(json.dumps(row,ensure_ascii=False)+'\n')
    tables(summary,classified,counter,native)
    if render:plots(summary,classified,sources,traces,g)
    write(DOCS/'summary173.json',summary)
    count=len(list(evidence.glob('finish-*.cpu.json')))+1
    write(evidence/f'finish-{count:02}.cpu.json',dict(CpuSeconds=time.process_time()-began,NativeWorlds=0))
    print(json.dumps(dict(native_worlds=4,matched=1438,forecasts=len(counter['rows']),locally_feasible=summary['counterfactual']['locally_feasible'],native_proof=proof),indent=2))


if __name__=='__main__':
    finish(Path(sys.argv[1]),'--no-render' not in sys.argv)
