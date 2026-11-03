from pathlib import Path
import gzip,json,hashlib,math,statistics,collections,time
R=Path(__file__).resolve().parents[2];D=R/'docs/experiments/s3-performance';S=R/'docs/experiments/trajectory-continuation'
def read(p):
 b=Path(p).read_bytes();return json.loads(gzip.decompress(b) if str(p).endswith('.gz') else b)
def save(p,v):
 p.parent.mkdir(parents=True,exist_ok=True);p.write_text(json.dumps(v,indent=2,ensure_ascii=False,allow_nan=False)+'\n',encoding='utf-8',newline='\n')
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def audit():
 start=time.process_time();old=read(S/'analysis.json');out=[];worst=None
 for n in old['native']:
  label=n['label'];ps=sorted((S/'policies'/label).glob('*.json.gz'));phase=collections.Counter();tree_sources=collections.Counter();generations=[];lengths=[];unique=[];commands=[];sources=[];prev=None;inc=[]
  for p in ps:
   q=read(p);r=q['Review'];assert q['Error'] is None and r['Independent'] and r['ConditionalCertificate'] and len(r['Tree'])==13 and len(r['Terminals'])==9
   for t in r['Profile']['Phases']:phase[t['Name']]+=t['CpuSeconds']
   assert abs(sum(t['CpuSeconds'] for t in r['Profile']['Phases'])-r['Profile']['MeasuredCpuSeconds'])<1e-8
   tree_sources.update(t['Source'] for t in r['Tree']);tapes=[r['Tapes'][t['Tape']] for t in r['Terminals']];lengths.extend(len(t['Actions']) for t in tapes);unique.append(len(set(t['Tape'] for t in r['Terminals'])))
   if q['Outcomes']:generations.append(dict(time=r['Time'],selected=q['SelectedOrigin'],outcomes=q['Outcomes']))
   c=q['Counters'];inc.append({k:c[k]-(prev[k] if prev else 0) for k in ['Forecasts','Contacts','TransitionRequests','SearchHits','IndependentHits','RolloutHits','IndependentForecasts','IndependentContacts','IndependentComparisons','PrimedPrefixes']});prev=c
   sources.append(dict(path=p.relative_to(R).as_posix(),sha256=sha(p)));commands.append(dict(time=r['Time'],origin=q['SelectedOrigin'],settings=q['ControlSettings'],action=r['Tree'][0]['Action'],goal=r['Goal'],safety_goal=r['SafetyGoal'],tape_minimum=r['Tapes'][r['Nominal']]['Safety'],counter_delta=inc[-1]))
  total=sum(phase.values());meta=read(S/'native'/f'{label}-result.json');raw=read(S/'native'/f'{label}.json.gz');ss=raw['Riders'][0]['Samples'];i=min(range(1,len(ss)),key=lambda i:ss[i]['Certificate']);a,b=ss[i-1:i+1];v=b['Input']['Rider'];pa=a['Input']['Rider'];dt=b['Input']['Time']-a['Input']['Time'];pose=min(a['Inner'],a['Outer'],b['Inner'],b['Outer']);rotation_radius=None
  w=dict(label=label,index=i,time=b['Input']['Time'],interval=[a['Input']['Time'],b['Input']['Time']],swept=b['Certificate'],minimum_endpoint_pose=pose,travel_plus_float_tube=pose-b['Certificate'],position=v['Position'],heading=v['Heading'],heading_deg=math.degrees(v['Heading']),chi=math.atan2(v['Velocity']['Y'],v['Velocity']['X']),beta=v['SlideRadians'],yaw=v['AngularVelocity'],speed=v['Speed'],reference=v['TotalReferenceMeters'],lap=math.floor(v['TotalReferenceMeters']/raw['LapMeters'])+1,segment=v['Road']['Segment'],offset=v['Road']['OffsetMeters'],lateral_velocity=sum(v['Velocity'][k]*v['Road']['Outward'][k] for k in ['X','Y']),before=a,after=b,decisions=[q for q in commands if b['Input']['Time']-3<=q['time']<b['Input']['Time']-1e-8],neighborhood_indices=[max(0,i-300),min(len(ss),i+201)],source=(S/'native'/f'{label}.json.gz').relative_to(R).as_posix())
  if worst is None or w['swept']<worst['swept']:worst=w
  out.append(dict(label=label,validation=n['validation'],finish_s=n['finish_s'],process_cpu_s=n['cpu_process_s'],callback_cpu_s=n['cpu_callbacks'],decision_count=len(ps),profile_cpu_s=total,phases_s=dict(phase),accepted_tree_sources=dict(tree_sources),phase_percent={k:100*v/total for k,v in phase.items()},outside_callback_cpu_s=n['cpu_process_s']-n['cpu_callbacks']['total'],last_counters=prev,counter_deltas_sum={k:sum(d[k] for d in inc) for k in inc[0]},terminal_commands=dict(min=min(lengths),median=statistics.median(lengths),max=max(lengths),sum=sum(lengths)),terminal_unique_ids=dict(min=min(unique),max=max(unique),duplicates=sum(9-u for u in unique)),generations=generations,all_generated_maneuvers=sum(len(g['outcomes']) for g in generations),recorded_maneuver_cpu_s=sum(x['CpuSeconds'] for g in generations for x in g['outcomes']),input_files=sources,source_sha256=sha(S/'native'/f'{label}.json.gz')))
 result=dict(base='85a5ee3295c2dd983fbacb21859649f794cc5ba5',scope='Read-only six archived161 worlds; zero forecasts, zero new certificates, zero native worlds.',runs=out,read_cpu_s=time.process_time()-start)
 save(D/'profile161.json',result);save(D/'tightest161.json',worst);print(json.dumps({'runs':len(out),'read_cpu_s':result['read_cpu_s'],'worst':{k:worst[k] for k in ['label','time','swept','segment','lap','lateral_velocity']},'conditional_cpu_s':sum(r['phases_s'].get('conditional_continuations',0) for r in out),'total_profile_cpu_s':sum(r['profile_cpu_s'] for r in out)}))
if __name__=='__main__':audit()
