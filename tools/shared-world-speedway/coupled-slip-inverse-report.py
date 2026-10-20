"""Native-only analysis for #108. No tyre equations or motion integrator."""
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
DATA=ROOT/'docs/experiments/coupled-slip-inverse'
if '--data-dir' in sys.argv:DATA=Path(sys.argv[sys.argv.index('--data-dir')+1]).resolve()
SOURCE=ROOT/'docs/experiments/speed-grip-line-crossover'
LAP=318.77874994277954
HALF=LAP/2
ENTRY,EXIT=35,132.38937497138977
load=lambda p:json.loads(p.read_text(encoding='utf-8-sig'))
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest().upper()

def save(name,obj):
    (DATA/name).write_text(json.dumps(obj,ensure_ascii=False,indent=2,allow_nan=False)+'\n',encoding='utf8')

def read(path):
    with gzip.open(path,'rt',encoding='utf8') as f:return [{k:float(v) for k,v in row.items()} for row in csv.DictReader(f)]

def window(rows,summary):
    crosses=summary['CrossingSeconds']
    if len(crosses)!=8:return []
    return [r for r in rows if crosses[-3]<=r['t']<crosses[-1]]

def bin_id(s):return int((s%LAP)/.5)

def freeze_bins():
    target=DATA/'baseline-bins.json'
    if target.exists():raise FileExistsError(target)
    cases={}
    for line in ('inner','wide'):
        name='baseline-'+line;summary=load(SOURCE/(name+'.json'))
        rows=window(read(DATA/(name+'-inverse.csv.gz')),summary)
        bins={}
        for r in rows:bins.setdefault(bin_id(r['s']),[]).append(r)
        cases[line]={str(k):{'Group':next(iter(status))+1 if len(status:={int(r['status']) for r in v})==1 else 3,
            'Statuses':sorted(status),'Count':len(v),'Duration':sum(r['dt'] for r in v)} for k,v in sorted(bins.items())}
    save('baseline-bins.json',{'Stage':'before coupled worlds','LapMeters':LAP,'BinMeters':.5,'Cases':cases,
        'Sources':{name:sha(DATA/name) for name in ('groups-freeze.json','baseline-inner-inverse.csv.gz','baseline-wide-inverse.csv.gz')},
        'Rule':'Final2 baseline laps; fixed reference half-metre bins. Uniform solved->1/unreachable->2; mixed, ambiguous or absent->3. Compare equal-bin aggregates on the same common bins.'})

if __name__=='__main__' and '--freeze-bins' in sys.argv:freeze_bins()

def bend(s):
    local=s%LAP
    for n in (0,1):
        if ENTRY+n*HALF<=local<EXIT+n*HALF:return n+1,(local-ENTRY-n*HALF)/(EXIT-ENTRY)
    return 0,0

def mean(rows,key):return sum(r[key]*r['dt'] for r in rows)/sum(r['dt'] for r in rows)
def rms(rows,key):return math.sqrt(sum(r[key]**2*r['dt'] for r in rows)/sum(r['dt'] for r in rows))
def percent(rows,predicate):return 100*sum(r['dt'] for r in rows if predicate(r))/sum(r['dt'] for r in rows)

def tracking_pass(c):
    return c['RmsError']<=.5 and c['MaxError']<=1 and c['RmsHeadingDegrees']<=3 and c['MaxHeadingDegrees']<=10

def classify_outcome(legacy,coupled):
    if coupled['TrackingPass'] and coupled['BoundarySafe'] and coupled['Periodic']:return 'PASS'
    # Safety and peak error are independent gates, not traded for RMS.
    if (coupled['MaxError']>legacy['MaxError'] or coupled['MinimumInner']<legacy['MinimumInner']
        or coupled['MinimumOuter']<legacy['MinimumOuter'] or coupled['ConservativeInner']<legacy['ConservativeInner']
        or coupled['ConservativeOuter']<legacy['ConservativeOuter'] or (legacy['BoundarySafe'] and not coupled['BoundarySafe'])
        or not coupled['Periodic']):return 'FAIL'
    if coupled['RmsError']<legacy['RmsError']-.001:return 'PARTIAL'
    return 'NO MATERIAL IMPROVEMENT'

def lag(rows):
    # Temporal diagnostic, not a controller retune: beta(t) against target(t-lag).
    times=[r['t'] for r in rows];scores=[]
    for ticks in range(51):
        delay=ticks*.01;pairs=[]
        for r in rows:
            if not bend(r['s'])[0]:continue
            i=bisect.bisect_right(times,r['t']-delay)-1
            if i<0 or i+1>=len(rows):continue
            a,b=rows[i:i+2];f=(r['t']-delay-a['t'])/(b['t']-a['t'])
            pairs.append((a['target_beta']+f*(b['target_beta']-a['target_beta']),r['beta']))
        x=[a for a,b in pairs];y=[b for a,b in pairs];mx=statistics.mean(x);my=statistics.mean(y)
        cov=sum((a-mx)*(b-my) for a,b in pairs);den=math.sqrt(sum((a-mx)**2 for a in x)*sum((b-my)**2 for b in y))
        scores.append((cov/den if den else 0,delay))
    correlation,delay=max(scores,key=lambda x:(x[0],-x[1]))
    return {'Seconds':delay,'Correlation':correlation,'GridSeconds':.01,'MaximumSeconds':.5}

def interp(rows,s,key):
    xs=[r['s'] for r in rows];i=max(0,min(len(rows)-2,bisect.bisect_right(xs,s)-1));a,b=rows[i:i+2]
    return a[key]+(b[key]-a[key])*(s-a['s'])/(b['s']-a['s'])

def analyze():
    frozen=load(DATA/'baseline-bins.json')
    for name,digest in frozen['Sources'].items():assert sha(DATA/name)==digest
    runs=load(DATA/'runs.json')
    assert all(r['BaselineBinsSha256']==sha(DATA/'baseline-bins.json') for r in runs)
    cases=[];cache={};phases=[];groups=[];locations=[]
    for line in ('inner','wide'):
        for mode in ('baseline','coupled'):
            name=mode+'-'+line;directory=SOURCE if mode=='baseline' else DATA;summary=load(directory/(name+'.json'))
            native=read(directory/(name+'-tracking.csv.gz'));forces=read(directory/(name+'-forces.csv.gz'));inverse=read(DATA/(name+'-inverse.csv.gz'))
            assert len(native)==len(forces)==len(inverse)
            decisions=read(DATA/(name+'-decisions.csv.gz')) if mode=='coupled' else None
            if decisions is not None:
                assert len(decisions)==len(inverse)
                assert all(abs(a['t']-b['t'])<=1e-12 and a['status']==b['status'] for a,b in zip(decisions,inverse))
            rows=[]
            for t,f,i in zip(native,forces,inverse):
                assert t['t']==f['t']==i['t'] and t['dt']==f['dt']==i['dt']
                beta=math.atan2(math.sin(t['body_heading']-t['velocity_heading']),math.cos(t['body_heading']-t['velocity_heading']))
                group=frozen['Cases'][line].get(str(bin_id(t['s'])),{'Group':3})['Group']
                rows.append(dict(t,**{k:v for k,v in i.items() if k not in t},normal_error=(i['required_normal']-i['actual_normal'])/f['mass'],
                    desired_acceleration=i['required_normal']/f['mass'],actual_acceleration=i['actual_normal']/f['mass'],
                    beta=math.degrees(beta),target_beta=math.degrees(beta+t['yaw_error']),yaw_error_degrees=math.degrees(t['yaw_error']),
                    heading_error_degrees=math.degrees(t['velocity_heading_error']),frozen_group=group,
                    force_limit=float(abs(t['yaw_requested'])>t['budget']*.5+1e-5),energy_limit=float(abs(t['yaw_authority']-t['yaw_energy'])>1e-5),
                    missing_fx_acceleration=t['fx']*math.sin(beta)/f['mass'],engine_power=f['engine_work']/t['dt']))
            final=window(rows,summary);bends=[r for r in final if bend(r['s'])[0]];assert final and bends
            period=max(summary['LapSeconds'][-3:])/min(summary['LapSeconds'][-3:])
            c={'Name':name,'Line':line,'Mode':mode,'RmsError':rms(final,'lateral_error'),'MaxError':max(abs(r['lateral_error']) for r in final),
                'RmsHeadingDegrees':rms(final,'heading_error_degrees'),'MaxHeadingDegrees':max(abs(r['heading_error_degrees']) for r in final),
                'NormalErrorRms':rms(bends,'normal_error'),'YawErrorRmsDegrees':rms(bends,'yaw_error_degrees'),'YawLag':lag(final),
                'MeanBetaDegrees':mean(bends,'beta'),'MaxBetaDegrees':max(abs(r['beta']) for r in bends),'MaxWheelspin':max(r['wheelspin'] for r in bends),
                'Slide3Percent':percent(bends,lambda r:abs(r['beta'])>=3),'CoreSlide3Percent':percent([r for r in bends if .05<=bend(r['s'])[1]<=.95],lambda r:abs(r['beta'])>=3),
                'UnreachablePercent':percent(bends,lambda r:int(r['status'])==1),'AmbiguousPercent':percent(bends,lambda r:int(r['status'])==2),
                'AllRunUnreachablePercent':percent(rows,lambda r:int(r['status'])==1),'ForceLimitPercent':100*mean(bends,'force_limit'),'EnergyLimitPercent':100*mean(bends,'energy_limit'),
                'MeanFxNormalAcceleration':mean(bends,'missing_fx_acceleration'),
                'MinimumInner':summary['MinimumInner'],'MinimumOuter':summary['MinimumOuter'],
                'ConservativeInner':summary['ConservativeInner'],'ConservativeOuter':summary['ConservativeOuter'],'BoundarySafe':summary['BoundarySafe'],
                'PeriodRatio':period,'Periodic':summary['Result']['Completed'] and period<=1.005,'Completed':summary['Result']['Completed'],
                'EnergyResidualJoules':summary['Energy']['BalanceResidualJoules'],'Fingerprint':summary['Result']['Fingerprint'],
                'NativeRows':len(rows),'MaximumMathResidualNewtons':max(abs(r['math_residual']) for r in rows if int(r['status'])==0),
                'UnreachableCount':sum(int(r['status'])==1 for r in rows),'AmbiguousCount':sum(int(r['status'])==2 for r in rows),
                'ForceReplayError':load(DATA/(name+'-classification.json'))['MaximumForceReplayErrorNewtons']}
            c['TrackingPass']=tracking_pass(c)
            assert not summary['Result']['Contacts'] and not summary['Result']['Decisions']
            assert abs(c['EnergyResidualJoules'])<5 and c['ForceReplayError']<=.003
            assert summary['Result']['Work']['SolverCalls']==summary['Result']['Work']['RearWheelCommits']
            for cycle in range(int(final[0]['s']//LAP)-1,int(final[-1]['s']//LAP)+1):
                for b in (0,1):
                    start=cycle*LAP+ENTRY+b*HALF;end=cycle*LAP+EXIT+b*HALF
                    if start<final[0]['s'] or end>final[-1]['s']:continue
                    phases.append({'Name':name,'Bend':b+1,'Cycle':cycle,'EntryKph':3.6*interp(rows,start,'speed'),
                        'MiddleKph':3.6*interp(rows,(start+end)/2,'speed'),'ExitKph':3.6*interp(rows,end,'speed'),
                        'Seconds':interp(rows,end,'t')-interp(rows,start,'t')})
            own=[p for p in phases if p['Name']==name]
            c.update({key:statistics.mean(p[key] for p in own) for key in ('EntryKph','MiddleKph','ExitKph','Seconds')})
            c['UnreachableIntervals']=[]
            for b in (1,2):
                local=[r for r in bends if bend(r['s'])[0]==b and int(r['status'])==1]
                if local:c['UnreachableIntervals'].append({'Bend':b,'FirstPhase':min(bend(r['s'])[1] for r in local),'LastPhase':max(bend(r['s'])[1] for r in local),
                    'FractionOfBendPercent':percent([r for r in bends if bend(r['s'])[0]==b],lambda r:int(r['status'])==1)})
            # Keep each native unreachable sample and its position, not merely min/max spans.
            locations.extend({'Name':name,**{k:r[k] for k in ('t','dt','s','status','required_normal','min_normal','max_normal')}} for r in rows if int(r['status'])!=0)
            cache[name]={'Rows':rows,'Final':final,'Bends':bends,'Summary':summary};cases.append(c)
        a,b=cases[-2:];b['Outcome']=classify_outcome(a,b);a['Outcome']='FAIL #106 tracking'+(' / uncertified' if not a['BoundarySafe'] else '')
        assert cache['baseline-'+line]['Summary']['Initial']==cache['coupled-'+line]['Summary']['Initial']
        # Spatial comparisons: same frozen baseline bins, each bin has equal weight.
        by_mode={}
        for mode in ('baseline','coupled'):
            bins={}
            for r in cache[mode+'-'+line]['Final']:bins.setdefault(bin_id(r['s']),[]).append(r)
            by_mode[mode]=bins
        common=sorted(set(by_mode['baseline'])&set(by_mode['coupled']))
        for group in (1,2,3):
            ids=[i for i in common if frozen['Cases'][line].get(str(i),{'Group':3})['Group']==group]
            for mode in ('baseline','coupled'):
                buckets=[by_mode[mode][i] for i in ids];buckets=[r for r in buckets if bend(r[0]['s'])[0]]
                if not buckets:continue
                groups.append({'Line':line,'Mode':mode,'FrozenGroup':group,'Bins':len(buckets),
                    'RmsError':math.sqrt(statistics.mean(rms(r,'lateral_error')**2 for r in buckets)),
                    'MaxError':max(abs(x['lateral_error']) for r in buckets for x in r),
                    'NormalErrorRms':math.sqrt(statistics.mean(rms(r,'normal_error')**2 for r in buckets)),
                    'YawErrorRmsDegrees':math.sqrt(statistics.mean(rms(r,'yaw_error_degrees')**2 for r in buckets)),
                    'MeanSpeed':statistics.mean(mean(r,'speed') for r in buckets),
                    'CurrentUnreachablePercent':statistics.mean(percent(r,lambda x:int(x['status'])==1) for r in buckets)})
    history={}
    for folder in (ROOT/'docs/experiments').iterdir():
        if folder==DATA or not (folder/'manifest.json').exists():continue
        manifest=load(folder/'manifest.json')
        if not isinstance(manifest,dict) or not all(isinstance(v,str) for v in manifest.values()):continue
        for file,digest in manifest.items():assert sha(folder/file).lower()==digest.lower(),folder/file
        history[folder.name]=len(manifest)
    result={'Cases':cases,'FrozenSpatialGroups':groups,'CornerPhases':phases,'HistoricalHashes':history,'NewMainWorlds':2,
        'LegacyWorldsRerun':0,'Mathematics':load(DATA/'mathematics.json'),'Runs':runs,'BaseHead':load(DATA/'freeze.json')['BaseHead']}
    save('analysis.json',result)
    for name,rows in [('cases.csv',[{k:v for k,v in c.items() if not isinstance(v,(list,dict))} for c in cases]),('groups.csv',groups),('corner-phases.csv',phases),('unreachable-locations.csv',locations)]:
        with (DATA/name).open('w',newline='',encoding='utf8') as f:
            writer=csv.DictWriter(f,rows[0].keys());writer.writeheader();writer.writerows(rows)
    return result,cache

if __name__=='__main__' and '--analyze' in sys.argv:
    result,_=analyze()
    print(json.dumps({'Cases':[{k:c[k] for k in ('Name','RmsError','MaxError','NormalErrorRms','YawLag','UnreachablePercent','Outcome')} for c in result['Cases']],
        'Groups':result['FrozenSpatialGroups']},indent=2))

def plots(a,cache):
    import matplotlib
    matplotlib.use('Agg')
    import matplotlib.pyplot as plt
    plt.rcParams.update({'font.size':9,'axes.grid':True,'grid.alpha':.2,'svg.hashsalt':'coupled108'})
    colors={'baseline':'#176395','coupled':'#d15b20'}
    def finish(fig,name):
        fig.tight_layout(rect=(0,.035,1,.96));fig.savefig(DATA/(name+'.png'),dpi=160)
        fig.savefig(DATA/(name+'.svg'),metadata={'Date':None})
        svg=DATA/(name+'.svg');svg.write_text('\n'.join(s.rstrip() for s in svg.read_text(encoding='utf8').splitlines())+'\n',encoding='utf8');plt.close(fig)
    fig,axes=plt.subplots(2,2,figsize=(12,8))
    for col,line in enumerate(('inner','wide')):
        base=cache['baseline-'+line]['Final'];planned=[r for r in base if r['s']>=base[-1]['s']-LAP]
        for ax in axes[:,col]:ax.plot([r['path_x'] for r in planned],[r['path_y'] for r in planned],'--',color='#3e4146',lw=1.5,label='Planned C3')
        for mode in colors:
            rows=cache[mode+'-'+line]['Final'];rows=[r for r in rows if r['s']>=rows[-1]['s']-LAP]
            for ax in axes[:,col]:ax.plot([r['x'] for r in rows],[r['y'] for r in rows],color=colors[mode],lw=1.35,label=mode.upper())
        axes[0,col].set_title(line.upper()+' — whole track');axes[1,col].set_title(line.upper()+' — first bend detail')
        focus=[r for r in planned if 32<=r['s']%LAP<=EXIT+3]
        axes[1,col].set_xlim(min(r['path_x'] for r in focus)-2,max(r['path_x'] for r in focus)+2)
        axes[1,col].set_ylim(min(r['path_y'] for r in focus)-2,max(r['path_y'] for r in focus)+2)
        for ax in axes[:,col]:ax.set_aspect('equal',adjustable='box');ax.set_xlabel('x [m]');ax.set_ylabel('y [m]');ax.legend(fontsize=8)
    fig.suptitle('Recorded Box2D paths; original plan and parameters — final two laps',fontsize=13)
    fig.text(.5,.012,'INNER remains boundary-certified; WIDE is uncertified between steps in both modes. Exact safety/peak metrics are in the table.',ha='center',fontsize=8)
    finish(fig,'native-trajectories')

    def bins(name,b):
        buckets={}
        for r in cache[name]['Bends']:
            if bend(r['s'])[0]==b:buckets.setdefault(bin_id(r['s']),[]).append(r)
        return [(statistics.mean(bend(r['s'])[1] for r in v)*100,v) for k,v in sorted(buckets.items())]
    frozen=load(DATA/'baseline-bins.json')['Cases']
    def shade(ax,line,b):
        for k,v in frozen[line].items():
            s=(int(k)+.5)*.5
            if bend(s)[0]!=b or v['Group']==1:continue
            x=bend(s)[1]*100;half=.25/(EXIT-ENTRY)*100
            ax.axvspan(x-half,x+half,color='#c74747' if v['Group']==2 else '#777777',alpha=.13,lw=0)
    fig,axes=plt.subplots(2,2,figsize=(13,7.5),sharex=True)
    for row,line in enumerate(('inner','wide')):
        for col,b in enumerate((1,2)):
            ax=axes[row,col];shade(ax,line,b)
            for mode,color in colors.items():
                values=bins(mode+'-'+line,b);x=[p for p,v in values]
                ax.plot(x,[mean(v,'desired_acceleration') for p,v in values],':',color=color,lw=1.4,label=mode.upper()+' requested')
                ax.plot(x,[mean(v,'actual_acceleration') for p,v in values],color=color,lw=1.7,label=mode.upper()+' applied')
            ax.set_title(f'{line.upper()} / bend {b}');ax.set_ylabel('normal acceleration [m/s²]');ax.set_xlabel('bend position [%]');ax.legend(fontsize=7,ncol=2)
    fig.suptitle('Normal force relative to actual velocity — original finite-step contact',fontsize=13)
    fig.text(.5,.012,'Red: frozen baseline UNREACHABLE bins; gray: mixed/ambiguous. Equal 0.5 m bins; actual current-state labels are reported separately.',ha='center',fontsize=8)
    finish(fig,'normal-acceleration')
    fig,axes=plt.subplots(3,4,figsize=(16,9),sharex='col')
    for col,(line,b) in enumerate((('inner',1),('inner',2),('wide',1),('wide',2))):
        for row,(key,label,mult) in enumerate((('beta','body slip β [deg]',1),('speed','speed [km/h]',3.6),('lateral_error','signed path error [m]',1))):
            ax=axes[row,col];shade(ax,line,b)
            for mode,color in colors.items():
                values=bins(mode+'-'+line,b)
                ax.plot([p for p,v in values],[mult*mean(v,key) for p,v in values],color=color,label=mode.upper(),lw=1.6)
            if row==0:ax.set_title(f'{line.upper()} / bend {b}');ax.legend(fontsize=8)
            if row==2:ax.axhline(1,color='#9a3030',ls='--',lw=.8);ax.axhline(-1,color='#9a3030',ls='--',lw=.8);ax.set_xlabel('bend position [%]')
            if col==0:ax.set_ylabel(label)
    fig.suptitle('Native slip, speed and tracking on both bends — unchanged Drive = 1',fontsize=13)
    fig.text(.5,.012,'Final two laps, 0.5 m bin means for display. All acceptance decisions use native peaks, full footprint and between-step bounds.',ha='center',fontsize=8)
    finish(fig,'slip-speed-error')

def report(a):
    cases=a['Cases'];by={c['Name']:c for c in cases}
    def row(label,key,fmt='.3f'):
        return '|'+label+'|'+'|'.join(('TAK' if c[key] else 'NIE') if isinstance(c[key],bool) else format(c[key],fmt) if isinstance(c[key],(float,int)) else str(c[key]) for c in cases)+'|'
    rows=[row('RMS boczny [m]','RmsError'),row('Max boczny [m]','MaxError'),row('RMS kierunku v [°]','RmsHeadingDegrees'),row('Max kierunku v [°]','MaxHeadingDegrees'),
        row('RMS błędu a normalnego w łuku [m/s²]','NormalErrorRms'),row('Min pełnego footprintu: kreda [m]','MinimumInner'),row('Min pełnego footprintu: banda [m]','MinimumOuter'),
        row('Between-step: kreda [m]','ConservativeInner'),row('Between-step: banda [m]','ConservativeOuter'),row('Średni czas łuku [s], diagnostyczny','Seconds'),
        row('UNREACHABLE: czas łuków [%]','UnreachablePercent'),row('AMBIGUOUS: czas łuków [%]','AmbiguousPercent'),row('UNREACHABLE: kroki całego8lap','UnreachableCount','d'),
        row('AMBIGUOUS: kroki całego8lap','AmbiguousCount','d'),row('Okresowość last3 max/min','PeriodRatio','.7f'),row('Bezpieczeństwo z between-step','BoundarySafe'),row('Ocena końcowa','Outcome')]
    grows=[]
    for line in ('inner','wide'):
        for group in (1,2,3):
            pair=[g for g in a['FrozenSpatialGroups'] if g['Line']==line and g['FrozenGroup']==group]
            if not pair:continue
            x,y=pair
            grows.append(f"|{line}/{group}|{x['Bins']}|{x['RmsError']:.3f}→{y['RmsError']:.3f}|{x['MaxError']:.3f}→{y['MaxError']:.3f}|{x['NormalErrorRms']:.3f}→{y['NormalErrorRms']:.3f}|{x['MeanSpeed']:.3f}→{y['MeanSpeed']:.3f}|")
    force_cost=load(DATA/'force-cost.json');ratios=[s['Modes'][1]['Milliseconds']/s['Modes'][0]['Milliseconds'] for s in force_cost['Samples']]
    native_check=load(DATA/'native-envelope-validation.json');verified=sum(c['CheckedNonSolvedNativeRows'] for c in native_check['Cases']);queries=sum(c['IndependentContactQueries'] for c in native_check['Cases'])
    speed_rows=['|'+c['Name']+f"|{c['EntryKph']:.2f}|{c['MiddleKph']:.2f}|{c['ExitKph']:.2f}|{c['MeanBetaDegrees']:.2f}/{c['MaxBetaDegrees']:.2f}|{c['MaxWheelspin']:.4f}|" for c in cases]
    cost_rows=[]
    for r in a['Runs']:
        cost_rows.append(f"|{r['Name']}|{r['ActualInverseCalls']}|{r['ContactEvaluations']}|{r['ContactEvaluations']/r['ActualInverseCalls']:.2f}/{r['MaximumContactEvaluations']}|{r['ElapsedMillisecondsIncludingNativeExports']/1000:.3f}|")
    text=f'''# Coupled Slip Inverse — kontrolowany eksperyment (#108)

**Wynik wariantu: FAIL. Ocena wykonania: A.** Nowa odwrotność działa zgodnie z oryginalnym kontaktem w przebadanym zakresie numerycznym, ale nie rozwiązuje błędów prowadzenia. RMS poprawia się nieznacznie, błąd maksymalny rośnie, prześwit od kredy maleje. Wariant pozostaje domyślnie OFF, wyłącznie do badań; **nie jest kandydatem do produkcji**.

Baza: Draft #107, HEAD **{a['BaseHead']}**, baza #106 **5648c4263739e46df40a366c96fabe4978cc1b3c**, zweryfikowane przed pracą. Jeden nowy Draft, bez zmian wcześniejszych PR i bez merge. Produkcyjne `src/CoreSim` pozostaje bez zmian.

## Protokół i matematyka przed jazdą

`SlipCoupledDynamics.Forces` oblicza istniejące żądanie `demand`, po czym stara ścieżka używa `ratio=clamp(m·demand/B,±0.95)` i `atan(δy/V·ratio/sqrt(1−ratio²))`. To odwrotność wcześniejszej pojedynczej Fy. Nie uwzględnia sprzężenia rear Fx/Fy ani normalnej składowej Fx. Dalej ten sam yaw PD dostaje `targetSlip−actualSlip`, z niezmienionymi czasami, bezwładnością, authority, limitem momentu i guardem pracy.

Nowe `EnableCoupledSlipInverse` jest OFF i nie serializuje dodatkowego pola przy OFF. ON jest ograniczone do pojedynczej trajektorii C3 bez traffic, z oryginalnym rear wheel i path-frame guidance. Zmienia **wyłącznie targetSlip**. Przy badaniu kandydackiego β zamrażamy zaakceptowane v, ω, surface/B, masę, promień/inercję koła, available/requested/delivered torque i rzeczywisty h. Osie ciała obracają się hipotetycznie względem kierunku v; otrzymujemy u/w i δy z tej samej oryginalnej funkcji. `RearWheelDynamics.Evaluate` liczy niezmieniony skończony kontakt, jego implicit midpoint i podział60/40. Nie wykonuje Body Step, Prepare/Commit, prognozy przyszłego świata, RNG lub wear.

Rozwiązywane równanie, ze znakiem skrętu zgodnym z lewą normalną n=(-vy,vx)/|v|:

`Fn(β)=dot(forward(β)·Fx_rear + side(β)·(Fy_front+Fy_rear) + Fres, n)=m·demand`.

W idealnej arytmetyce jest to `Fx·sinβ+(Fy_front+Fy_rear)·cosβ`; implementacja projektuje pełny oryginalny wektor float. Fres pozostaje przeciwne v. Opona, napęd, wheelspin, yaw, gains/preview, h, Egg Mild/progressive-b i C3 XY nie zmieniają się. Nie ma drugiego modelu opony ani dodanej siły.

Domena kątów **[-45°,45°]**, zamrożona przed wynikami.24 interwały plus refinement każdego wykrytego minimum/maksimum (20 iteracji złotego podziału), następnie wszystkie przedziały zmiany znaku i bisekcja do24 iteracji. Nie zakładamy monotoniczności; test pokazuje dwa rozwiązania i żądanie poniżej skalarnego B bez rozwiązania. Wybieramy najmniejsze |β|, przy dokładnym remisie mniejszy podpisany β: preferowana gałąź ma mniejsze ustawienie bokiem i nie zależy od historii wykonania. Tolerancja siły **0,05N** (≈0,00034m/s² przy147kg), kąta1e-6rad; pas niepewności obwiedni0,25N. `SOLVED` wymaga sprawdzonej reszty; brak pierwiastka poza obwiednią jest `UNREACHABLE` w tej domenie. Tangencje/pas niepewności/niedookreślony kierunek małej v trafiają do `AMBIGUOUS`. Absolutny limit pętli to1682 ocen (25+23·23+47·24), a zaobserwowany w obu światach93; koszt dla innych konfiguracji nie jest zatwierdzony.

To **ograniczona numeryczna ocena obwiedni**, nie uniwersalny dowód interval-arithmetic dla dowolnej konfiguracji. Przed jazdą sprawdzono128 stanów (V5/18/24/35, rω/V0,5/1/1,4/2, Drive0/1, h0/5/10/20ms),1152 żądania i niezależną siatkę1441 kątów na stan. Największy miss obwiedni0,000366N, reszta rozwiązania0,049988N. Po przejazdach dodatkowo sprawdzono **każdy z{verified} nie-SOLVED stanów final2** obu trybów, niezależną siatką: {queries} offline zapytań, max miss0,000733N, **zero fałszywych odrzuceń względem siatki**. Nie zmieniano zamrożonych etykiet. Niezbadane konfiguracje wymagają ponownej walidacji tego ograniczonego solvera.

Obsługa `UNREACHABLE` i `AMBIGUOUS` była ustalona przed jazdą: zachować **dokładnie stary ograniczony targetSlip**. Każdy fallback jest zapisany w `*-decisions.csv.gz`, z żądaniem, obwiednią, starym/wybranym kątem i liczbą ocen. Lokalnie SOLVED nie oznacza wykonalnej całej trajektorii: yaw i ω wciąż muszą rzeczywiście przejść do nowego stanu.

[freeze.json](coupled-slip-inverse/freeze.json), [matematyka](coupled-slip-inverse/mathematics.json), [weryfikacja native](coupled-slip-inverse/native-envelope-validation.json).

## Dwa rzeczywiste światy i wyniki

Reuse pełnych native LEGACY #106/#107; **0 powtórzeń starej macierzy**. Tylko dwa główne nowe światy COUPLED: INNER/WIDE, jeden motocykl, Grip1/Ruts0/Moisture0,5, Drive1, start reference32 / v18m/s / ω=18/0,33, h10ms z oryginalnymi podziałami granic,8lap. Initial snapshot ciała i koła jest dokładnie identyczny pomiędzy trybami. Krótkie scenariusze deterministyczności testów nie są ukrywane jako reuse.

Statystyki trackingu i normalnej siły: końcowe dwa pełne okrążenia; normalna siła oznacza faktycznie przyłożony F względem v początku interwału. Bezpieczeństwo obejmuje cały8lap i pełny footprint, z oryginalnym konserwatywnym bound między krokami. Dane pokazują momenty/siły z zaakceptowanych stanów. Nie używamy sztucznie narzuconego β podczas jazdy.

|Metryka|INNER LEGACY|INNER COUPLED|WIDE LEGACY|WIDE COUPLED|
|---|---|---|---|---|
{chr(10).join(rows)}

INNER: RMS **−2,57%**, max **+17,43mm**, accepted min od kredy **−19,02mm**. WIDE: RMS **−4,06%**, max **+14,46mm**, min od kredy **−15,08mm**. WIDE w obu trybach ma dodatni prześwit w zaakceptowanych końcach interwałów, ale ujemny konserwatywny inner bound. To **brak certyfikatu pomiędzy krokami**, a nie zaobserwowane przecięcie kredy. Oba8lap ukończone; okresowość dobra, lecz żaden COUPLED nie przechodzi wszystkich progów. **Nie zmieniono progów #106 i nie kwalifikujemy wyników jako PARTIAL wyłącznie dzięki RMS. Czasy są diagnostyczne; nie ma rankingu.**

|Próba|entry km/h|middle km/h|exit km/h|β mean/max °|κ max|
|---|---|---|---|---|---|
{chr(10).join(speed_rows)}

![Zamierzona i wykonana trajektoria](coupled-slip-inverse/native-trajectories.png)

## Zamrożone grupy i te same położenia

**Przed COUPLED** sklasyfikowano wszystkie oryginalne native stany LEGACY, zachowując źródłowe hashe. Final2 baseline zamieniono na stałe biny0,5m reference, z zamrożonym przypisaniem:1=jednolicie SOLVED,2=jednolicie UNREACHABLE,3=mieszane/niejednoznaczne/brak danych. `baseline-bins.json` powstał przed światami, a jego SHA zapisano w obu runs. Nie klasyfikujemy według późniejszego wyniku COUPLED. W tabeli poniżej oba tryby używają **tych samych wspólnych binów i równej wagi binu**, więc różna v nie przestawia wag czasowych porównania. Liczby dotyczą binów w łukach. Aktualna osiągalność każdego trybu jest dodatkowo wyliczona i raportowana niezależnie.

|Linia / grupa|biny|RMS XY LEGACY→COUPLED [m]|max [m]|RMS a error [m/s²]|v mean [m/s]|
|---|---|---|---|---|---|
{chr(10).join(grows)}

W grupie1 RMS XY poprawia się **13,48% INNER /11,13% WIDE**, ale max także rośnie, a RMS błędu a normalnego **1,288→1,338** i **1,450→1,510m/s²** pogarsza się o≈3,9/4,2%. Nie ma dowodu, że dokładniejsza matematyczna odwrotność usunęła dynamiczny błąd wykorzystania siły. W grupie2 max całego przejazdu pozostaje i rośnie. Zmiana wcześniejszych stanów wpływa także na późniejsze fallbacki.

Przy aktualnym stanie raw żądanie regulatora jest UNREACHABLE przez23,30→22,63% czasu łuków INNER i25,30→24,95% WIDE; to **nie jest** procent błędu XY przyczynowo przypisany fizyce. Żądanie zawiera feed-forward oraz korekcję PD i różni się od `v²κ_path` badanego w #107. Odrzucenia występują na obu wejściach: około4–30% łuku INNER i5–33% WIDE. Pełne lokalizacje i wszystkie zdarzenia, nie tylko zakres min/max, są w [unreachable-locations.csv](coupled-slip-inverse/unreachable-locations.csv) oraz native `*-decisions.csv.gz`. Grupa3 pozostaje osobno:2 biny INNER/1 WIDE; jej mieszane etykiety nie zostały zaliczone do osiągalnych.

![Wymagane i rzeczywiste a normalne](coupled-slip-inverse/normal-acceleration.png)

## Yaw, stabilność i energia

Reszta odwrotności ≤0,05N dotyczy kandydata β z zamrożonym zaakceptowanym ω i h. Błąd rzeczywistej normalnej siły jest większy, bo motocykl nie przeskakuje do β. Korelacja β(t) z targetβ(t−lag), na siatce0–0,5s co10ms, daje LEGACY≈0,19s; COUPLED INNER≈0,20s, WIDE≈0,19s (korelacje>0,993). Jest to diagnostyka fazy w tym przejeździe, nie uniwersalna identyfikacja czasu odpowiedzi. RMS yaw/slip error w łukach INNER2,386→2,384°, WIDE2,848→2,897°. Oryginalny clamp momentu i guard energetyczny były aktywowane przez **0%** czasu łuków; opóźnienie nie jest dowodem ich saturacji.

Niezmieniony yaw feed-forward nadal projektuje tylko `side·Fy`, pomijając normalną składową Fx. Z native wynika średnio≈0,40–0,41m/s² takiej składowej. To konkretna zachowana niespójność z pełną normalną siłą; **nie udowodniono**, że wyjaśnia cały błąd1,3–1,5m/s² w grupie1. Opóźnienie yaw, preview i wspólna ewolucja ω/β nadal mogą mieć udział. Nie przypisujemy pozostałego błędu pojedynczej przyczynie.

Oba COUPLED mają ograniczoną prędkość i okresowy ruch do8lap (last3 max/min1,0000714 /1,0001014). β≥3° utrzymuje się przez98,29% czasu łuków INNER i100% WIDE, w środkowych90% obu łuków zawsze100%. To potwierdza utrzymanie driftu w tych próbach. **Tylko INNER ma kompletny certyfikat bezpieczeństwa**; nie ogłaszamy bezpiecznej stabilnej WIDE. Nie wykonywano nowej perturbacyjnej macierzy i nie twierdzimy o globalnej stabilności.

Whole-system ledger body+yaw+wheel, z oryginalnymi engine/resistance/bearing/slip pracami: residual LEGACY INNER0,0338J, COUPLED−0,0485J; WIDE−0,9714J /+1,3878J. Każdy rzeczywisty Fx/Fy/globalF odtworzono oryginalnym kontaktem: **max różnica0N** we wszystkich czterech zapisach. Różnica zapisanej i odtworzonej wartości raw żądania przez float round-trip ≤0,000141N; runtime/offline etykiety są identyczne w każdym kroku. Wheel commits = solver calls; brak kontaktów, decyzji traffic, RNG/wear i drugiego naliczania skutków. Solver odwrotności nigdy nie zatwierdza koła ani ciała.

![Uślizg, prędkość i błąd na obu łukach](coupled-slip-inverse/slip-speed-error.png)

## Koszt i walidacja

|Nowy świat|wywołania odwrotności|dodatkowe Evaluate kontaktu|mean/max na wywołanie|czas z native exportem [s]|
|---|---|---|---|---|
{chr(10).join(cost_rows)}

Są to **dodatkowe** pytania o kontakt obok jednego prawdziwego kontaktu na krok i istniejących boundary ocen. Nie tworzą nowych Body Step. Jedna ocena kontaktu ma swój niezmieniony scalar wheel root (max8 iteracji); nie jest drugim integratorem. Liczniki zapisano per-step i zsumowano, więc koszt nie jest ukryty w `WorldWork.RearWheelForceEvaluations`. Offline klasyfikacja/weryfikacja to oddzielne zapytania po danych, nie koszty bieżącej jazdy.

Na czterech zaakceptowanych baseline stanach, ten sam pełny `SlipCoupledDynamics.Forces` z gotową guidance: **{min(ratios):.2f}–{max(ratios):.2f}×** czasu OFF, około20,6–24,4µs ON wobec3,0–4,5µs OFF; **0B alokacji** obu ścieżek na dedykowanym rozgrzanym wątku. Osobny koszt samej odwrotności≈24µs /82 kontaktów w próbce,≈88,5× kosztu pojedynczego Evaluate. To **nie** jest mnożnik całej gry ani Box2D: podane czasy światów zawierają export, nie mają nowego odpowiadającego legacy wall-time i nie są benchmarkiem wyścigu. Negatywny wynik nie uzasadnia obecnie optymalizowania solvera. [force-cost.json](coupled-slip-inverse/force-cost.json).

Release --warnaserror:0 warnings/errors. Testy celowane odwrotności obejmują znak/Fx, rzeczywisty h, gałęzie, brak rozwiązania poniżej B, niejednoznaczność, domyślny OFF/zakres,0B i krótki native replay z jednoznacznym commitem. Adekwatne dotychczasowe testy wheel/slip oraz stary fingerprint #100 sprawdzają legacy. Dokładne liczniki testów i standardowy końcowy Windows CI Draft są w [validation.json](coupled-slip-inverse/validation.json) i opisie PR. **{sum(a['HistoricalHashes'].values())}/{sum(a['HistoricalHashes'].values())} historycznych SHA-256 zachowane**, w tym cały #107. Manifest obejmuje wszystkie nowe surowe zapisy, analizy i wykresy. Nie uruchamiano historycznych macierzy #95–#107.

Reprodukcja w nowym katalogu: `coupled-slip-inverse DIR math`, `freeze`; helper Python `--data-dir DIR --freeze-bins`; następnie `runs`, `cost`, `verify-envelope`, `verify-forces`. TRX obu grup celowanych testów należy skierować do DIR przez `--ResultsDirectory:DIR` (8 inverse +32 wheel/slip/legacy fingerprint). Helper raportu `--data-dir DIR --report --plots` używa dostarczonych ścieżek i oryginalnych native danych; nie zawiera fizyki. Wykresy wymagają matplotlib. Zapisane8lap nie są ponawiane przy generowaniu raportu.

## Jednoznaczne odpowiedzi

1. **Stara odwrotność nie była dominującym źródłem problemu.** Ma mierzalny wkład w RMS XY (globalnie2,6/4,1%, w osiągalnych binach11–13,5%), ale jej zastąpienie nie poprawia max, bezpieczeństwa ani dynamicznego błędu siły w grupie1.
2. **Tracking: FAIL.** INNER RMS0,456→0,444m, max1,235→1,253m; WIDE0,535→0,513m, max1,311→1,325m. Margines przy kredzie zmalał. Zgodnie z kryteriami ten kompromis nie jest sukcesem.
3. **Drift pozostał okresowy w badanych8lap.** INNER jest bezpieczny; WIDE pozostaje niecertyfikowana pomiędzy krokami. Nie rozszerzamy tego wniosku na inne warunki.
4. **Limity sił i regulator rozdzielono, bez fałszywego procentowego podziału winy.** Aktualne żądania poza obwiednią:22,63/24,95% czasu łuków COUPLED. W grupie1 nadal duży dynamiczny błąd a i≈190–200ms opóźnienia. Wielkości te nie pozwalają przypisać określonego procentu RMS do jednej przyczyny; geometria/profil nie zostały zmienione.
5. **Nie promować do produkcji; zachować OFF tylko do badań.** Jeden następny eksperyment: przy zamrożonym COUPLED target porównać wyłącznie yaw feed-forward liczony z pełnej faktycznej normalnej siły kontaktu (łącznie z Fx), bez zmiany gains, guardów lub opony. Uzasadnienie: obecny feed-forward pomija zmierzone≈0,40m/s², a dokładniejsza odwrotność nie poprawiła dynamicznego wykorzystania siły. To test przyczyny, nie obietnica PASS.
'''
    (ROOT/'docs/experiments/coupled-slip-inverse.md').write_text(text,encoding='utf8')
    counters={p.name:ET.parse(p).getroot().find('{*}ResultSummary/{*}Counters').attrib for p in DATA.glob('*.trx')}
    assert sum(int(c['passed']) for c in counters.values())==40 and all(int(c['failed'])==0 for c in counters.values())
    save('validation.json',{'TargetedCSharp':40,'TargetedPython':4,'TestCounters':counters,'HistoricalHashes':sum(a['HistoricalHashes'].values()),
        'NewMainWorlds':2,'LegacyWorldsRerun':0,'CI':'Exact final HEAD and standard Draft Windows CI run in PR/final delivery.',
        'OriginalTyreWheelYawGainsUnchanged':True,'DefaultOff':True,'Outcome':'FAIL'})
    manifest={f.name:sha(f).lower() for f in sorted(DATA.iterdir()) if f.is_file() and f.name!='manifest.json'}
    save('manifest.json',manifest)

if __name__=='__main__' and '--report' in sys.argv:
    a,cache=analyze()
    if '--plots' in sys.argv:plots(a,cache)
    report(a)
    print('Native analysis/report updated; no physics or worlds executed.')
