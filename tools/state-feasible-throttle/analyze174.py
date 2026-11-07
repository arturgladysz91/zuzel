"""Sealed #174 results only. No forecasts, native worlds, or tuning."""
from pathlib import Path
import gzip,hashlib,io,json,math,statistics,sys,time,zipfile
ROOT=Path(__file__).resolve().parents[2]
sys.path.insert(0,str(ROOT/'tools/three-lap-global-planning'))
from analyze172 import cross,queue_audit
from audit171 import trace_of,section
from report171 import high_drive

def load(p):return json.loads(p.read_bytes())
def percentile(values,p):
    values=sorted(values);i=(len(values)-1)*p;a=int(i);b=min(a+1,len(values)-1)
    return values[a]+(values[b]-values[a])*(i-a)
def timing(values):return dict(median=statistics.median(values),p95=percentile(values,.95),p99=percentile(values,.99),maximum=max(values))

def main():
    began=time.process_time();e=ROOT.parent/'evidence174';out=ROOT/'docs/experiments/state-feasible-throttle';out.mkdir(exist_ok=True)
    states=[];model={}
    for phase in ['c1','x1']:
        with gzip.open(e/(phase+'-states.jsonl.gz'),'rt',encoding='utf-8') as source:
            for line in source:
                row=json.loads(line);states.append(row);model[row['Id']]=row['Evaluation']
    rows=[];traces={};full_baselines={}
    with zipfile.ZipFile(ROOT/'docs/experiments/three-lap-global-planning/172-protocol.zip') as z:geometry=json.loads(z.read('geometry.json'))
    with zipfile.ZipFile(ROOT/'docs/experiments/three-lap-global-planning/172-validation-K4.zip') as outer:
        for path in sorted(e.glob('d1-*.zip')):
            if path.name.endswith('-source.zip'):continue
            with zipfile.ZipFile(path) as z:
                native=json.loads(z.read('native.json'));result=json.loads(z.read('result.json'))
                decisions=json.loads(z.read('decisions.json'));admissions=json.loads(z.read('admissions.json'));branch=json.loads(z.read('branch.json'))
            assert len(admissions)==1 and admissions[0]['Admitted'] and result['ReachedStop']
            assert branch['PhysicalPrefixEqual'] and branch['StateHashMatches']
            queue_audit(native,decisions)
            with zipfile.ZipFile(io.BytesIO(outer.read(branch['State']['Trial']+'.zip'))) as z:old=json.loads(z.read('native.json'))
            ident=branch['Id'];old_trace=trace_of(old);new_trace=trace_of(native);full_baselines[ident]=old_trace
            goal=result['Stop'];start=branch['State']['Input']['Time'];activation=start+.24
            arms={}
            for name,trace in [('BASE',old_trace),('NEW',new_trace)]:
                goal_point=cross(trace,goal);end=goal_point['t'];clip=[q for q in trace if q['t']<=end+1e-8]
                lo=geometry['Segments'][5]['StartReferenceMeters'];hi=geometry['Segments'][8]['StartReferenceMeters']
                a,b=cross(trace,lo),cross(trace,hi);bend=[q for q in clip if lo<=q['s']<=hi]
                straight=[q for q in clip if geometry['Segments'][4]['StartReferenceMeters']<=q['s']<=lo]
                high=high_drive(trace,cross(trace,geometry['Segments'][4]['StartReferenceMeters'])['t'],a['t'])
                after=[q for q in clip if q['t']>=activation]
                arms[name]=dict(common_goal_seconds=end,entry_seconds=a['t'],exit_seconds=b['t'],entry_kph=a['kph'],exit_kph=b['kph'],
                    bend_seconds=b['t']-a['t'],minimum_bend_kph=min(q['kph'] for q in bend),straight_vmax_kph=max(q['kph'] for q in straight),
                    high_intervals=high,high_seconds=sum(w['end']-w['start'] for w in high),distance=goal_point['distance'],
                    max_beta=max(abs(q['beta']) for q in clip),min_pose=min(min(q['inner'],q['outer']) for q in clip),
                    min_swept=min(q['swept'] for q in clip if q['swept'] is not None),
                    branch_min_swept=min(q['swept'] for q in after if q['swept'] is not None),
                    sections=[section(trace,segment) for segment in geometry['Segments'][5:8]])
                traces[(ident,name)]=clip
            times=[d['DecisionMilliseconds'] for d in decisions]
            added_forecasts=sum(1+len(a['Short'])+sum((a[k] or {}).get('Forecasts',0) for k in ['BaselinePath','ProposedPath']) for a in admissions)
            rows.append(dict(id=ident,branch=branch,arms=arms,local_gain_seconds=arms['BASE']['common_goal_seconds']-arms['NEW']['common_goal_seconds'],
                gain_to_entry_seconds=arms['BASE']['entry_seconds']-arms['NEW']['entry_seconds'],
                gain_in_bend_seconds=arms['BASE']['bend_seconds']-arms['NEW']['bend_seconds'],
                timing_ms=timing(times),first_decision_ms=times[0],maximum_warm_ms=max(times[1:]),
                forecasts=dict(base_controller=native['Forecasts'],state_feasibility=added_forecasts,total=native['Forecasts']+added_forecasts),
                cpu=dict(total_native_run=native['TotalCpuSeconds'],controller=native['ControllerCpuSeconds'],feasibility=admissions[0]['CpuSeconds']),
                model_predicted_gain_seconds=admissions[0]['BaselinePath']['Seconds']-admissions[0]['ProposedPath']['Seconds']))
    compact=[]
    for row in states:
        a=row['Evaluation'];copy={**row,'Evaluation':{**a}}
        for name in ['BaselinePath','ProposedPath']:
            if a[name]:copy['Evaluation'][name]={k:v for k,v in a[name].items() if k!='Points'}
        proposed=a['ProposedPath']
        if proposed and not proposed['Reached']:
            points=proposed['Points'];violation=None
            for j,q in enumerate(points):
                beta=abs(q['Rider']['SlideRadians']);pose=min(q['Inner'],q['Outer']);sweep=float('inf')
                if j:
                    prev=points[j-1];dt=q['Time']-prev['Time'];sweep=min(pose,prev['Inner'],prev['Outer'])-.5*dt*(max(prev['Rider']['Speed'],q['Rider']['Speed'])+1.05*max(abs(prev['Rider']['AngularVelocity']),abs(q['Rider']['AngularVelocity'])))-.0005
                if beta>math.pi/4 or pose<.65 or sweep<.65:
                    violation=dict(time=q['Time'],reference=q['Rider']['TotalReferenceMeters'],beta_degrees=math.degrees(beta),pose=pose,swept=sweep);break
            copy['first_model_violation']=violation
        compact.append(copy)
    held=load(e/'held174.json');closed=load(e/'native-closed.json')
    summary=dict(verdict='PARTIAL',functional='NOT ESTABLISHED: no 8-pair S3 after performance gate failed',sport='NOT TESTED; threshold remains 5 percent',
        performance='FAIL 200ms period; warm admission costs exceed 700ms',states=compact,development=rows,
        validation=[dict(**s,BASE='NOT_RUN',NEW='NOT_RUN',reason='development decision-cost gate failed; heldout unconsumed') for s in held],
        settings=load(e/'protocol174.json'),closure=closed,scope='All three native runs end before first finish; no new complete S3',
        provisional_cpu_seconds=load(e/'archive-proof.json')['CpuSeconds']+sum(load(p)['CpuSeconds'] for p in e.glob('*.done.json')))
    (out/'summary174.json').write_text(json.dumps(summary,ensure_ascii=False,indent=2,allow_nan=False),encoding='utf-8')
    tables=['# Tabele #174 — krótkie próby, nie S3','', '| Próba | Model dopuszcza | BASE cel [s] | NEW cel [s] | Zysk lokalny [s] | V wej BASE/NEW | V min BASE/NEW | V wyj BASE/NEW | Swept NEW globalny / po impulsie | Ocena [ms] |','|---|---|---:|---:|---:|---|---|---|---|---:|']
    for r in rows:
        a,b=r['arms']['BASE'],r['arms']['NEW'];tables.append(f"| {r['id']} | tak, natywnie legalny | {a['common_goal_seconds']:.6f} | {b['common_goal_seconds']:.6f} | {r['local_gain_seconds']:.6f} | {a['entry_kph']:.2f}/{b['entry_kph']:.2f} | {a['minimum_bend_kph']:.2f}/{b['minimum_bend_kph']:.2f} | {a['exit_kph']:.2f}/{b['exit_kph']:.2f} | {b['min_swept']:.3f}/{b['branch_min_swept']:.3f} | {r['branch']['MaximumDecisionMs']:.1f} |")
    tables+=['','## Archiwalne stany: wynik modelu, nie natywny certyfikat','','| Stan | Gaz historyczny | Propozycja | Stary CanPrepare | Decyzja modelu | Zysk modelu [s] | CPU [ms] | Powód |','|---|---:|---:|---|---|---:|---:|---|']
    for r in compact:
        a=r['Evaluation'];gain=a['BaselinePath']['Seconds']-a['ProposedPath']['Seconds'] if a['ProposedPath'] and a['ProposedPath']['Reached'] else None
        tables.append(f"| {r['Id']} | {100*a['Historical']['Drive']:.2f}% | {100*a['Proposed']['Drive']:.0f}% | {a['OldCanPrepare']} | {a['Admitted']} | {gain if gain is not None else 'UNKNOWN'} | {1000*a['CpuSeconds']:.1f} | {a['Reason']} |")
    tables+=['','## Osiem odłożonych par — niewykonane','','| Stan | Pole | Start [m] | Offset [m] | BASE | NEW |','|---|---|---:|---:|---|---|']
    tables += [f"| {s['Id']} | {s['Gate']} | {s['Start']} | {s['Offset']} | NOT_RUN | NOT_RUN |" for s in held]
    tables+=['','Powód: niespełniona bramka kosztu z fazy D. Nie ma mediany S3, trzech czasów okrążeń ani poprawy sportowej dla #174. Pełne dane sekcji, czasu gazu ≥95%, drogi, prognoz oraz percentyli CPU zawiera summary174.json.']
    (out/'TABELE.md').write_text('\n'.join(tables)+'\n',encoding='utf-8')
    figures(out,geometry,rows,traces,full_baselines,compact)
    (e/'analysis.cpu.json').write_text(json.dumps({'CpuSeconds':time.process_time()-began,'NativeWorlds':0,'Stage':'sealed analysis and plots'}),encoding='utf-8')
    print(json.dumps({'native':len(rows),'states':len(compact),'cpu':time.process_time()-began,'verdict':'PARTIAL'}))

def figures(out,geometry,rows,traces,full,states):
    import matplotlib
    matplotlib.use('Agg')
    import matplotlib.pyplot as plt
    plt.rcParams.update({'font.size':9})
    fig,axes=plt.subplots(1,3,figsize=(15,5),layout='constrained')
    for ax,r in zip(axes,rows):
        for edge in ['Inner','Outer']:
            pts=[p[edge] for s in geometry['Segments'] for p in s['Sections']];ax.plot([p['X'] for p in pts],[p['Y'] for p in pts],color='.55',lw=.8)
        for name,color in [('BASE','#b64c4c'),('NEW','#1579ad')]:
            t=traces[(r['id'],name)];ax.plot([q['x'] for q in t],[q['y'] for q in t],color=color,label=name,lw=1.3)
        ax.set_aspect('equal');ax.set_title(r['id']+' · do następnego łuku +20m');ax.legend();ax.set_xlabel('X [m]');ax.set_ylabel('Y [m]')
    fig.suptitle('Oryginalna geometria i rzeczywiste XY · trzy krótkie próby, nie S3');fig.savefig(out/'174-xy.png',dpi=160);plt.close(fig)
    panels=[('kph','Prędkość [km/h]'),('active','Gaz aktywny'),('requested','Gaz żądany'),('beta','β [°]'),('yaw','Yaw [rad/s]'),('slip','Wheelspin κ'),('rear_fx','Fx tył [N]'),('rear_fy','Fy tył [N]'),('front_fy','Fy przód [N]'),('swept','Swept [m]')]
    fig,axes=plt.subplots(len(panels),3,figsize=(15,19),layout='constrained')
    for col,r in enumerate(rows):
        for j,(key,label) in enumerate(panels):
            ax=axes[j,col]
            for name,color in [('BASE','#b64c4c'),('NEW','#1579ad')]:
                t=traces[(r['id'],name)];ax.plot([q['t'] for q in t],[q[key] for q in t],color=color,label=name,lw=1)
            ax.axvline(r['branch']['State']['Input']['Time']+.24,color='.3',ls='--',lw=.8);ax.set_ylabel(label);ax.grid(alpha=.2)
            if key=='swept':ax.axhline(.5,color='red',ls=':')
            if j==0:ax.set_title(r['id']);ax.legend()
            if j==len(panels)-1:ax.set_xlabel('Czas od startu [s]')
    fig.suptitle('Przerywana: aktywacja impulsu po 0,24s · brak resetu lub nowej fizyki');fig.savefig(out/'174-telemetria.png',dpi=145);plt.close(fig)
    fig,axes=plt.subplots(3,1,figsize=(12,8),layout='constrained')
    for ax,r in zip(axes,rows):
        t=full[r['id']];n=traces[(r['id'],'NEW')];ax.plot([q['t'] for q in t],[q['kph'] for q in t],color='.5',label='archiwalny BASE pełne S3 + rezerwa')
        ax.plot([q['t'] for q in n],[q['kph'] for q in n],color='#1579ad',label='NEW tylko zarejestrowany odcinek');ax.axvspan(n[-1]['t'],t[-1]['t'],color='#eee1bd',alpha=.5,label='NEW UNKNOWN / nie uruchomiono');ax.set_ylabel(r['id']+' km/h');ax.legend(loc='lower right',fontsize=8);ax.grid(alpha=.2)
    axes[-1].set_xlabel('Czas [s]');fig.suptitle('Brak pełnej serii #174: nie dopisujemy wyników po końcu prób');fig.savefig(out/'174-zakres-S3.png',dpi=160);plt.close(fig)
    fig,axes=plt.subplots(1,2,figsize=(12,4),layout='constrained')
    axes[0].bar([r['Id'] for r in states],[r['Evaluation']['Milliseconds'] for r in states],color=['#2c86a0' if r['Evaluation']['Admitted'] else '#b97550' for r in states]);axes[0].axhline(200,color='red',ls='--',label='okres 200ms');axes[0].set_ylabel('Ocena całego łuku [ms]');axes[0].legend()
    axes[1].bar([r['id'] for r in rows],[r['branch']['MaximumDecisionMs'] for r in rows]);axes[1].axhline(200,color='red',ls='--');axes[1].set_ylabel('Najdłuższa rzeczywista decyzja [ms]');fig.suptitle('Bramka wydajności nie przeszła · bez gotowości czasu rzeczywistego');fig.savefig(out/'174-koszt.png',dpi=160);plt.close(fig)

if __name__=='__main__':main()
