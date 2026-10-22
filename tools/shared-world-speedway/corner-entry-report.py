"""Analyse #119 accepted records only, reusing the independent historical energy ledger."""
import csv,gzip,hashlib,importlib.util,json,math,argparse,subprocess
from pathlib import Path

ROOT=Path(__file__).resolve().parents[2]
DATA=ROOT/'docs/experiments/feasible-500cc-corner-entry'
spec=importlib.util.spec_from_file_location('performance',Path(__file__).with_name('motorcycle-performance-report.py'))
old=importlib.util.module_from_spec(spec);spec.loader.exec_module(old)
NAMES=['partial','wide','combined','ai']
ENTRY,MIDDLE,EXIT=35.,83.6946875,132.389375
def load(name):return json.loads((DATA/name).read_text(encoding='utf-8-sig'))
def read(name):
    p=DATA/(name+'.csv');f=open(p,newline='') if p.exists() else gzip.open(str(p)+'.gz','rt',newline='')
    with f:return [{k:float(v) for k,v in r.items()} for r in csv.DictReader(f)]
def save(name,value):(DATA/name).write_text(json.dumps(value,indent=2,allow_nan=False)+'\n',encoding='utf-8',newline='\n')
def analyze():
    hashes={}
    for manifest in sorted((ROOT/'docs/experiments').glob('*/manifest.json')):
        if manifest.parent==DATA:continue
        entries=json.loads(manifest.read_text(encoding='utf-8-sig'))
        if not isinstance(entries,dict) or not all(isinstance(x,str) and len(x)==64 for x in entries.values()):continue
        for name,digest in entries.items():
            f=manifest.parent/name;assert hashlib.sha256(f.read_bytes()).hexdigest().upper()==digest.upper(),str(f)
            hashes[str(f.relative_to(ROOT)).replace('\\','/')]=digest
    unchanged=['Engine500.cs','LoadBasedTyreGrip.cs','RearWheelDynamics.cs','TwoContactBikeDynamics.cs','TravelDirectionSteering.cs','SlipCoupledDynamics.cs','ExperimentalTrackShape.cs']
    for name in unchanged:
        subprocess.run(['git','diff','--exit-code',CornerBase,'--','tools/shared-world-speedway/'+name],cwd=ROOT,check=True)
    cache={};out={}
    for name in NAMES:
        run=load(name+'.json');rows=read(name);q=read(name+'-control');geo=read(name+'-geometry');cache[name]=(run,rows,q,geo)
        assert len(rows)==len(q)==len(geo)==run['Result']['Work']['SolverCalls']==run['Result']['Work']['RearWheelCommits']
        assert run['Energy']['MaximumExcessBeyondEngineJoules']==0
        energy=old.old.old.energy_from_new(rows,run);impulse=old.old.impulse_work(rows,run)
        unexplained=energy['Residual']-impulse['MeasuredNativeImpulseErrorWorkJoules'];assert abs(unexplained)<1e-6
        assert old.minimum_total_tyre_loss(rows,run)>=-1e-6
        assert run['Energy']['MaximumLinearImpulseResidualNs']<.001 and run['Energy']['MaximumAngularImpulseResidualNms']<.00003
        for r,c in zip(rows,q):
            assert abs(r['front_lateral'])<=c['front_capacity']*(1+4*2**-23)
            assert math.hypot(r['rear_longitudinal'],r['rear_lateral'])<=c['rear_capacity']*(1+4*2**-23)
            assert abs(r['steer_after']-r['steer_before'])<=math.pi/2*r['dt']+1e-6
        final=run['Result']['Riders'][0]
        endpoint=dict(s=final['TotalReferenceMeters'],speed=final['Speed'],t=run['Result']['TimeSeconds'],distance=final['DistanceMeters'])
        points={}
        for label,s in [('Entry',ENTRY),('Middle',MIDDLE),('Exit',EXIT)]:
            if endpoint['s']<s:points[label]=None;continue
            points[label]={key:old.crossing(rows,'s',s,key) for key in ['t','speed','beta','wheelspin','drive']}
            points[label]['SpeedKph']=points[label]['speed']*3.6
        duration=sum(r['dt'] for r in rows)
        region=[i for i,r in enumerate(rows) if ENTRY<=r['s']<=EXIT]
        out[name]=dict(StopReason=run['StopReason'],Gate=load(name+'-gate.json'),Landmarks=points,
            End=endpoint,PeakSpeedKph=max(r['speed'] for r in rows)*3.6,MinimumSpeedKph=min(r['speed'] for r in rows)*3.6,
            PeakBetaDegrees=max(abs(r['beta']) for r in rows+[dict(beta=final['SlideRadians'])])*180/math.pi,
            PeakWheelspin=max(r['wheelspin'] for r in rows),FinalWheelspin=rows[-1]['wheelspin'],
            MaximumOffset=max(g['offset'] for g in geo),MinimumOffset=min(g['offset'] for g in geo),
            CornerTime=None if not points['Exit'] else points['Exit']['t']-points['Entry']['t'],
            Energy=energy,NativeEnergy=run['Energy'],UnexplainedEnergyJoules=unexplained,
            ContactQueries=sum(c['queries'] for c in q),Steps=len(rows),Reviews=run['Result']['Work'].get('CornerEntryReviews',0),
            ForceUnreachableFraction=sum(r['dt']*c['unreachable'] for r,c in zip(rows,q))/duration,
            Fingerprint=run['Result']['Fingerprint'])
    a,b=cache['combined'],cache['ai']
    # The AI must execute the proven explicit prefix, not a different low-speed shortcut.
    assert a[1]==b[1][:len(a[1])]
    assert a[2]==b[2][:len(a[2])]
    assert out['combined']['Gate']['Pass'] and out['ai']['Gate']['Pass']
    decisions=cache['ai'][0]['Result']['Decisions'];assert len(decisions)==2
    for d in decisions:assert abs(d['ActualActivation']-d['DecisionAt']-.24)<1e-8
    assert decisions[-1]['Drive']==1
    result=dict(BaseHead=CornerBase,DiagnosticWorlds=4,ManualVariants=3,FullLaps=0,HistoricalFiles=len(hashes),
        HistoricalSha256=hashes,PhysicsSourcesUnchanged=unchanged,Trials=out,AiDecisions=decisions,
        AiProvenPrefixExact=True,LatestStartTimeCertified=False,
        Rule='Three explicit preparations, then one gated AI trial; verification runs are targeted tests, not new parameter variants. No second bend. Safe slow passage does not certify fast speedway cornering.')
    save('analysis.json',result)
    print(json.dumps({k:{a:v[a] for a in ['StopReason','Landmarks','PeakBetaDegrees','CornerTime','UnexplainedEnergyJoules']} for k,v in out.items()},indent=2))
    return result,cache
def plots(result,cache):
    import matplotlib;matplotlib.use('Agg')
    import matplotlib.pyplot as plt
    plt.rcParams.update({'font.size':10,'axes.spines.top':False,'axes.spines.right':False,'axes.titlesize':12,'axes.titleweight':'bold'})
    colors={'partial':'#c5722a','wide':'#ba5555','combined':'#227596','ai':'#298661'}
    labels={'partial':'gaz 0,15; offset 1 m','wide':'pełny gaz; offset 5 m','combined':'gaz 0,15 + offset 5 m','ai':'AI (ten sam udany prefiks)'}
    fig,axes=plt.subplots(3,2,figsize=(13,11),layout='constrained');fig.suptitle('Pierwszy łuk: bezpieczne przygotowanie okupione dużą utratą prędkości',fontsize=16,fontweight='bold')
    geom=json.loads((ROOT/'docs/experiments/progressive-bend-width/progressive-b-geometry.json').read_text())
    pts=[p for seg in geom['segments'] for p in seg['sections'] if p['inner']['x']>=20]
    for edge in ['inner','outer']:axes[0,0].plot([p[edge]['x'] for p in pts],[p[edge]['y'] for p in pts],color='#aaa',lw=.8)
    for name,(run,rows,q,g) in cache.items():
        kw=dict(color=colors[name],label=labels[name],ls='--' if name=='ai' else '-',lw=1.8)
        s=[r['s'] for r in rows]
        axes[0,0].plot([r['x'] for r in rows],[r['y'] for r in rows],**kw)
        axes[0,1].plot(s,[r['speed']*3.6 for r in rows],**kw)
        axes[1,0].plot(s,[r['beta']*180/math.pi for r in rows],**kw)
        axes[1,1].plot(s,[r['drive'] for r in rows],**kw)
        axes[2,0].plot(s,[r['wheelspin'] for r in rows],**kw)
    _,rows,q,g=cache['ai'];s=[r['s'] for r in rows]
    axes[2,1].plot(s,[r['inner'] for r in rows],label='wewnętrzny',color='#227596')
    axes[2,1].plot(s,[r['outer'] for r in rows],label='zewnętrzny',color='#c5722a');axes[2,1].axhline(0,color='#333',lw=.7)
    axes[0,0].set(aspect='equal',xlabel='X [m]',ylabel='Y [m]',title='Rzeczywiste XY; zatrzymanie przy pierwszym FAIL')
    for ax,title,y in [(axes[0,1],'Prędkość','km/h'),(axes[1,0],'Uślizg całego motocykla β','°'),(axes[1,1],'Rzeczywisty gaz po reakcji 0,24 s','Drive'),(axes[2,0],'Wheelspin tylnego kontaktu','κ'),(axes[2,1],'Prześwity sylwetki AI','m')]:
        ax.set(xlabel='Współrzędna referencyjna s [m]',ylabel=y,title=title)
        for x in [ENTRY,MIDDLE,EXIT]:ax.axvline(x,color='#999',lw=.6,ls=':')
    for ax in axes.flat:ax.grid(alpha=.2);ax.legend(fontsize=8)
    fig.savefig(DATA/'entry-comparison.png',dpi=145);plt.close(fig)
    approach=load('approach.json')['Points'];fig,axes=plt.subplots(2,2,figsize=(13,8),layout='constrained')
    fig.suptitle('Granice kontaktu i fizyczny koszt przejazdu — bez zmian opon',fontsize=16,fontweight='bold')
    for key,label in [('RequiredNormal','wymaganie linii'),('Budget','absolutny budżet'),('AllSteeringSampledNormal','maks. próbka: dowolny kąt'),('NextIntervalSampledNormal','maks. próbka: tempo kierownicy')]:
        axes[0,0].plot([r['S'] for r in approach],[r[key] for r in approach],label=label)
    axes[0,0].set(xlabel='s [m]',ylabel='N',title='Zapisany #117: utrata zapasu na wejściu')
    for key,label in [('required_normal','żądanie linii'),('actual_normal','rzeczywista siła normalna')]:axes[0,1].plot(s,[r[key] for r in q],label=label)
    axes[0,1].axhline(1852.339,ls='--',color='#777',label='absolutny B')
    axes[0,1].set(xlabel='s [m]',ylabel='N',title='Udany AI: cel nie jest wymuszoną trajektorią')
    for key,label in [('rear_longitudinal','Fx tyłu'),('rear_lateral','Fy tyłu'),('front_lateral','Fy przodu')]:axes[1,0].plot(s,[r[key] for r in rows],label=label)
    axes[1,0].set(xlabel='s [m]',ylabel='N',title='Oryginalne siły kontaktów')
    e=result['Trials']['combined']['Energy']
    amounts=[e['Engine'],e['FrontLoss']+e['RearLateralLoss'],e['RearLongitudinalLoss'],e['Bearing']-e['ResistanceWork'],e['BodyChange']+e['WheelChange']]
    labels=['praca silnika','straty boczne','wheelspin','opory + łożysko','zmiana E ciała i koła']
    axes[1,1].barh(labels,[v/1000 for v in amounts],color=['#298661','#c5722a','#c5722a','#c5722a','#227596'])
    for i,v in enumerate(amounts):axes[1,1].text(v/1000+(1 if v>=0 else -1),i,f'{v/1000:.2f}',va='center',ha='left' if v>=0 else 'right',fontsize=9)
    axes[1,1].axvline(0,color='#777',lw=.7);axes[1,1].set(xlabel='kJ',xlim=(-20,49),title='Combined: bilans ciała + koła (reszta 0,126 J)')
    for ax in [axes[0,0],axes[0,1],axes[1,0]]:ax.grid(alpha=.2);ax.legend(fontsize=8)
    fig.savefig(DATA/'capacity-and-energy.png',dpi=145);plt.close(fig)
CornerBase='895cffe3df5a2486d6876c0798c494fc061f9308'
if __name__=='__main__':
    ap=argparse.ArgumentParser();ap.add_argument('--plots',action='store_true');ap.add_argument('--compact',action='store_true');args=ap.parse_args()
    result,cache=analyze()
    if args.plots:plots(result,cache)
    if args.compact:
        for p in DATA.glob('*.csv'):p.with_suffix('.csv.gz').write_bytes(gzip.compress(p.read_bytes(),mtime=0));p.unlink()
    save('manifest.json',{str(p.relative_to(DATA)).replace('\\','/'):hashlib.sha256(p.read_bytes()).hexdigest().upper() for p in sorted(DATA.rglob('*')) if p.is_file() and p.name!='manifest.json'})
