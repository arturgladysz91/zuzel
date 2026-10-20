"""#109 native evidence analysis. No contact solver, new world or integration."""
import csv
import importlib.util
import json
import math
from pathlib import Path
import statistics
import sys

ROOT=Path(__file__).resolve().parents[2]
DATA=ROOT/'docs/experiments/full-contact-yaw-feedforward'
SOURCE=ROOT/'docs/experiments/coupled-slip-inverse'
if '--data-dir' in sys.argv:DATA=Path(sys.argv[sys.argv.index('--data-dir')+1]).resolve()
spec=importlib.util.spec_from_file_location('coupled108',Path(__file__).with_name('coupled-slip-inverse-report.py'))
old=importlib.util.module_from_spec(spec)
spec.loader.exec_module(old)
load,sha,read=old.load,old.sha,old.read
mean,rms,percent,bend,bin_id=old.mean,old.rms,old.percent,old.bend,old.bin_id

def save(name,data):
    (DATA/name).write_text(json.dumps(data,indent=2,ensure_ascii=False,allow_nan=False)+'\n',encoding='utf8')

def analyze():
    frozen=load(SOURCE/'baseline-bins.json');freeze=load(DATA/'freeze.json')
    for item in freeze['Sources']:assert sha(SOURCE/item['File'])==item['Sha256']
    assert sha(ROOT/'tools/shared-world-speedway/CoupledSlipInverse.cs')==freeze['InverseSourceSha256']
    for name,key in [('native-mathematics.json','MathematicsSha256'),('off-reference.json','OffReferenceSha256')]:
        assert sha(DATA/name)==freeze[key]
    a,b=freeze['ControlParameters'],dict(freeze['FullParameters'])
    assert b.pop('EnableFullContactYawFeedForward') and a==b and a['EnableCoupledSlipInverse']
    cases=[];cache={};groups=[];phases=[];peaks=[]
    control_runs={r['Name']:r for r in load(SOURCE/'runs.json')}
    full_runs={r['Name']:r for r in load(DATA/'runs.json')}
    for line in ('inner','wide'):
        for mode in ('control','full'):
            name=mode+'-'+line;source_name=('coupled' if mode=='control' else mode)+'-'+line
            directory=SOURCE if mode=='control' else DATA
            summary=load(directory/(source_name+'.json'))
            tracking=read(directory/(source_name+'-tracking.csv.gz'));forces=read(directory/(source_name+'-forces.csv.gz'))
            decisions=read(directory/(source_name+'-decisions.csv.gz'));yaw=read(DATA/(name+'-yaw.csv.gz'))
            assert len(tracking)==len(forces)==len(decisions)==len(yaw)
            rows=[]
            for t,f,d,y in zip(tracking,forces,decisions,yaw):
                assert t['t']==f['t']==y['t'] and t['dt']==f['dt']==y['dt']
                # Different round-trip formatting of the same native clock; this is an analysis join, not a physics tolerance.
                assert abs(t['t']-d['t'])<=1e-12
                beta=math.degrees(math.atan2(math.sin(t['body_heading']-t['velocity_heading']),math.cos(t['body_heading']-t['velocity_heading'])))
                group=frozen['Cases'][line].get(str(bin_id(t['s'])),{'Group':3})['Group']
                rows.append(dict(t,**{k:v for k,v in y.items() if k not in t},status=d['status'],
                    normal_error=d['required']/f['mass']-y['actual_normal'],desired_normal=d['required']/f['mass'],
                    beta=beta,target_beta=beta+math.degrees(t['yaw_error']),yaw_error_degrees=math.degrees(t['yaw_error']),
                    heading_error_degrees=math.degrees(t['velocity_heading_error']),frozen_group=group,
                    force_limit=float(abs(t['yaw_requested'])>t['budget']*.5+1e-5),energy_limit=float(abs(t['yaw_authority']-t['yaw_energy'])>1e-5),
                    body_force_residual=f['body_force_residual'],yaw_residual=f['yaw_residual'],evaluations=d['evaluations']))
            final=old.window(rows,summary);bends=[r for r in final if bend(r['s'])[0]];assert final and bends
            work=summary['Result']['Work'];run=(control_runs if mode=='control' else full_runs)[source_name]
            count_key='ActualInverseCalls' if mode=='control' else 'InverseCalls'
            eval_key='ContactEvaluations' if mode=='control' else 'InverseContactEvaluations'
            assert len(rows)==run[count_key]==work['SolverCalls']==work['RearWheelForceEvaluations']==work['RearWheelCommits']
            assert sum(int(r['evaluations']) for r in rows)==run[eval_key]
            assert not summary['Result']['Contacts'] and not summary['Result']['Decisions']
            assert all(r['Drive']==1 for r in summary['Result']['Riders'])
            assert summary['Energy']['MaximumYawGuardExcessWatts']==0
            period=max(summary['LapSeconds'][-3:])/min(summary['LapSeconds'][-3:])
            c={'Name':name,'Mode':mode,'Line':line,'RmsError':rms(final,'lateral_error'),'MaxError':max(abs(r['lateral_error']) for r in final),
                'RmsHeadingDegrees':rms(final,'heading_error_degrees'),'MaxHeadingDegrees':max(abs(r['heading_error_degrees']) for r in final),
                'NormalErrorRms':rms(bends,'normal_error'),'YawErrorRmsDegrees':rms(bends,'yaw_error_degrees'),'YawLag':old.lag(final),
                'MeanBetaDegrees':mean(bends,'beta'),'MaxBetaDegrees':max(abs(r['beta']) for r in bends),
                'MeanWheelspin':mean(bends,'wheelspin'),'MaxWheelspin':max(r['wheelspin'] for r in bends),
                'MeanActualCurvature':mean(bends,'actual_curvature'),'MeanPlannedCurvature':mean(bends,'path_curvature'),
                'MeanFxNormalAcceleration':mean(bends,'fx_normal'),'MeanUsedTravelYaw':mean(bends,'legacy_yaw' if mode=='control' else 'full_yaw'),
                'UnreachablePercent':percent(bends,lambda r:int(r['status'])==1),'AmbiguousPercent':percent(bends,lambda r:int(r['status'])==2),
                'ForceLimitPercent':100*mean(bends,'force_limit'),'EnergyLimitPercent':100*mean(bends,'energy_limit'),
                'MinimumInner':summary['MinimumInner'],'MinimumOuter':summary['MinimumOuter'],
                'ConservativeInner':summary['ConservativeInner'],'ConservativeOuter':summary['ConservativeOuter'],'BoundarySafe':summary['BoundarySafe'],
                'Completed':summary['Result']['Completed'],'PeriodRatio':period,'Periodic':summary['Result']['Completed'] and period<=1.005,
                'EnergyResidualJoules':summary['Energy']['BalanceResidualJoules'],'MaximumYawGuardExcessWatts':summary['Energy']['MaximumYawGuardExcessWatts'],
                'MaximumBodyForceResidualNewtons':max(r['body_force_residual'] for r in rows),'MaximumYawResidualNewtonMeters':max(r['yaw_residual'] for r in rows),
                'MaximumBodyImpulseResidualNewtonSeconds':max(abs(r['body_force_residual']*r['dt']) for r in rows),
                'MaximumYawImpulseResidualNewtonMeterSeconds':max(abs(r['yaw_residual']*r['dt']) for r in rows),
                'NativeRows':len(rows),'InverseContactEvaluations':run[eval_key],'BodySteps':work['SolverCalls'],'WheelCommits':work['RearWheelCommits'],
                'ElapsedMillisecondsIncludingExports':run['ElapsedMillisecondsIncludingNativeExports' if mode=='control' else 'ElapsedMillisecondsIncludingExports'],
                'Fingerprint':summary['Result']['Fingerprint']}
            c['TrackingPass']=old.tracking_pass(c)
            for cycle in range(int(final[0]['s']//old.LAP)-1,int(final[-1]['s']//old.LAP)+1):
                for b in (0,1):
                    start=cycle*old.LAP+old.ENTRY+b*old.HALF;end=cycle*old.LAP+old.EXIT+b*old.HALF
                    if start<final[0]['s'] or end>final[-1]['s']:continue
                    phases.append({'Name':name,'Bend':b+1,'Cycle':cycle,'EntryKph':3.6*old.interp(rows,start,'speed'),
                        'MiddleKph':3.6*old.interp(rows,(start+end)/2,'speed'),'ExitKph':3.6*old.interp(rows,end,'speed')})
            own=[p for p in phases if p['Name']==name]
            c.update({key:statistics.mean(p[key] for p in own) for key in ('EntryKph','MiddleKph','ExitKph')})
            for label,predicate in [('entry',lambda x:0<=x<.15),('middle',lambda x:.15<=x<=.85),('exit',lambda x:.85<x<=1)]:
                local=[r for r in bends if predicate(bend(r['s'])[1])];peak=max(local,key=lambda r:abs(r['lateral_error']))
                peaks.append({'Name':name,'Phase':label,'RmsError':rms(local,'lateral_error'),'MaxError':abs(peak['lateral_error']),
                    'PeakBend':bend(peak['s'])[0],'PeakPositionPercent':100*bend(peak['s'])[1],'NormalErrorRms':rms(local,'normal_error'),
                    'YawErrorRmsDegrees':rms(local,'yaw_error_degrees'),'MeanBetaDegrees':mean(local,'beta')})
            cases.append(c);cache[name]={'Rows':rows,'Final':final,'Bends':bends,'Summary':summary}
        a,b=cases[-2:];a['Outcome']='FAIL';b['Outcome']=old.classify_outcome(a,b)
        assert cache['control-'+line]['Summary']['Initial']==cache['full-'+line]['Summary']['Initial']
        buckets={}
        for mode in ('control','full'):
            bins={}
            for r in cache[mode+'-'+line]['Final']:
                if bend(r['s'])[0]:bins.setdefault(bin_id(r['s']),[]).append(r)
            buckets[mode]=bins
        common=sorted(set(buckets['control'])&set(buckets['full']))
        for group in (1,2,3):
            ids=[i for i in common if frozen['Cases'][line].get(str(i),{'Group':3})['Group']==group]
            if not ids:continue
            for mode in ('control','full'):
                parts=[buckets[mode][i] for i in ids]
                groups.append({'Line':line,'Mode':mode,'FrozenGroup':group,'Bins':len(ids),
                    'RmsError':math.sqrt(statistics.mean(rms(r,'lateral_error')**2 for r in parts)),
                    'MaxError':max(abs(x['lateral_error']) for r in parts for x in r),
                    'NormalErrorRms':math.sqrt(statistics.mean(rms(r,'normal_error')**2 for r in parts)),
                    'YawErrorRmsDegrees':math.sqrt(statistics.mean(rms(r,'yaw_error_degrees')**2 for r in parts)),
                    'CurrentUnreachablePercent':statistics.mean(percent(r,lambda x:int(x['status'])==1) for r in parts)})
    history={}
    for folder in (ROOT/'docs/experiments').iterdir():
        if folder==DATA or not (folder/'manifest.json').exists():continue
        manifest=load(folder/'manifest.json')
        if not isinstance(manifest,dict) or not all(isinstance(v,str) for v in manifest.values()):continue
        for file,digest in manifest.items():assert sha(folder/file).lower()==digest.lower(),folder/file
        history[folder.name]=len(manifest)
    cost=load(DATA/'force-cost.json')
    for s in cost['Samples']:
        assert s['AdditionalContactEvaluations']==0 and s['TargetInverseUnchanged']
        assert all(m['AllocatedBytes']==0 for m in s['Modes'])
    result={'BaseHead':freeze['BaseHead'],'Cases':cases,'FrozenSpatialGroups':groups,'CornerPhases':phases,'PhaseErrors':peaks,
        'HistoricalHashes':history,'NewMainWorlds':2,'ControlMainWorldsRerun':0,'Mathematics':load(DATA/'native-mathematics.json'),'Cost':cost,
        'FrozenBinsSha256':sha(SOURCE/'baseline-bins.json'),'Outcome':'FAIL'}
    save('analysis.json',result)
    for name,rows in [('cases.csv',[{k:v for k,v in c.items() if not isinstance(v,dict)} for c in cases]),('groups.csv',groups),('corner-phases.csv',phases),('phase-errors.csv',peaks)]:
        with (DATA/name).open('w',newline='',encoding='utf8') as f:
            writer=csv.DictWriter(f,rows[0].keys());writer.writeheader();writer.writerows(rows)
    return result,cache

def plots(cache):
    import matplotlib
    matplotlib.use('Agg')
    import matplotlib.pyplot as plt
    plt.rcParams.update({'font.size':9,'axes.grid':True,'grid.alpha':.2,'svg.hashsalt':'full-contact109'})
    colors={'control':'#176395','full':'#d15b20'};frozen=load(SOURCE/'baseline-bins.json')['Cases']
    def finish(fig,name):
        fig.tight_layout(rect=(0,.035,1,.95));fig.savefig(DATA/(name+'.png'),dpi=160)
        fig.savefig(DATA/(name+'.svg'),metadata={'Date':None})
        p=DATA/(name+'.svg');p.write_text('\n'.join(s.rstrip() for s in p.read_text(encoding='utf8').splitlines())+'\n',encoding='utf8');plt.close(fig)
    def bins(name,b):
        parts={}
        for r in cache[name]['Bends']:
            if bend(r['s'])[0]==b:parts.setdefault(bin_id(r['s']),[]).append(r)
        return [(statistics.mean(bend(r['s'])[1] for r in rows)*100,rows) for k,rows in sorted(parts.items())]
    def shade(ax,line,b):
        for k,v in frozen[line].items():
            s=(int(k)+.5)*.5
            if bend(s)[0]!=b or v['Group']==1:continue
            x=bend(s)[1]*100;half=.25/(old.EXIT-old.ENTRY)*100
            ax.axvspan(x-half,x+half,color='#c74747' if v['Group']==2 else '#777777',alpha=.13,lw=0)
    fig,axes=plt.subplots(2,2,figsize=(12,7),sharex=True,sharey=True)
    for row,line in enumerate(('inner','wide')):
        for col,b in enumerate((1,2)):
            ax=axes[row,col];shade(ax,line,b)
            for mode,color in colors.items():
                # Native traces retain peaks; two final laps are both shown.
                rows=[r for r in cache[mode+'-'+line]['Bends'] if bend(r['s'])[0]==b]
                for cycle in sorted({int(r['s']//old.LAP) for r in rows}):
                    local=[r for r in rows if int(r['s']//old.LAP)==cycle]
                    ax.plot([bend(r['s'])[1]*100 for r in local],[r['lateral_error'] for r in local],color=color,lw=1.5,
                        label=mode.upper() if cycle==min(int(r['s']//old.LAP) for r in rows) else None)
            ax.axhline(1,ls='--',color='#983030',lw=.8);ax.axhline(-1,ls='--',color='#983030',lw=.8)
            ax.set_title(f'{line.upper()} / bend {b}');ax.set_ylabel('signed path error [m]');ax.set_xlabel('bend position [%]');ax.legend()
    fig.suptitle('Full-contact yaw: native path error, final two laps — both variants use COUPLED inverse',fontsize=12)
    fig.text(.5,.012,'Unchanged ±1 m peak gate. Red: frozen group 2; gray: group 3. RMS falls, but native peaks and inner safety margins worsen.',ha='center',fontsize=8)
    finish(fig,'trajectory-error')
    fig,axes=plt.subplots(3,4,figsize=(16,9),sharex='col')
    for col,(line,b) in enumerate((('inner',1),('inner',2),('wide',1),('wide',2))):
        for ax in axes[:,col]:shade(ax,line,b)
        for mode,color in colors.items():
            values=bins(mode+'-'+line,b);x=[p for p,v in values]
            axes[0,col].plot(x,[mean(v,'desired_normal') for p,v in values],':',color=color,label=mode.upper()+' demand')
            axes[0,col].plot(x,[mean(v,'actual_normal') for p,v in values],color=color,label=mode.upper()+' applied')
            axes[1,col].plot(x,[mean(v,'target_beta') for p,v in values],':',color=color,label=mode.upper()+' target')
            axes[1,col].plot(x,[mean(v,'beta') for p,v in values],color=color,label=mode.upper()+' actual')
            key='legacy_yaw' if mode=='control' else 'full_yaw'
            axes[2,col].plot(x,[mean(v,key) for p,v in values],color=color,label=mode.upper()+' used yaw')
        axes[0,col].set_title(f'{line.upper()} / bend {b}');axes[2,col].set_xlabel('bend position [%]')
        for row in range(3):axes[row,col].legend(fontsize=6,ncol=2)
    for ax,label in zip(axes[:,0],('normal acceleration [m/s²]','body slip β [deg]','travelYaw [rad/s]')):ax.set_ylabel(label)
    fig.suptitle('Native force, slip response and yaw feed-forward — full tyre force changes yaw only',fontsize=13)
    fig.text(.5,.012,'Spatial 0.5 m means for display, final two laps. Runtime inverse demand includes the unchanged path feedback. Frozen-state causal differences are in Stage A.',ha='center',fontsize=8)
    finish(fig,'normal-acceleration-yaw')

if __name__=='__main__':
    result,cache=analyze()
    if '--plots' in sys.argv:plots(cache)
    print(json.dumps({'Cases':result['Cases'],'Groups':result['FrozenSpatialGroups'],'PhaseErrors':result['PhaseErrors'],
        'HistoricalHashes':sum(result['HistoricalHashes'].values())},indent=2))
