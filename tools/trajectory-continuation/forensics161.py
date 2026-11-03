"""One read-only audit of existing160 telemetry/policy inputs. Zero forecasts."""
from pathlib import Path
import gzip,hashlib,json,math,time
R=Path(__file__).resolve().parents[2];D=R/'docs/experiments/trajectory-continuation';OLD=R/'docs/experiments/dynamic-slip-preview'
def read(p):
 p=Path(p);return json.loads(gzip.decompress(p.read_bytes()) if p.suffix=='.gz' else p.read_bytes())
def save(p,v):
 p.parent.mkdir(parents=True,exist_ok=True);b=(json.dumps(v,ensure_ascii=False,indent=2,allow_nan=False)+'\n').encode();p.write_bytes(gzip.compress(b,mtime=0) if p.suffix=='.gz' else b)
def main():
 began=time.process_time();run=read(OLD/'native/native-dev-v5.json.gz');ss=run['Riders'][0]['Samples'];display=[]
 for i,s in enumerate(ss):
  if s['Input']['Time']<47:continue
  v=s['Input']['Rider'];road=v['Road'];vy=v['Velocity']['X']*road['Outward']['X']+v['Velocity']['Y']*road['Outward']['Y'];chi=math.atan2(v['Velocity']['Y'],v['Velocity']['X'])
  display.append(dict(index=i,t=s['Input']['Time'],x=v['Position']['X'],y=v['Position']['Y'],reference=v['TotalReferenceMeters'],offset=road['OffsetMeters'],lateral_velocity=vy,theta=v['Heading'],chi=chi,beta=v['SlideRadians'],yaw=v['AngularVelocity'],steering=s['Input']['Steering'],requested_steering=s['RequestedSteering'],gas=s['Input']['Active']['Drive'],requested_gas=s['RequestedDrive'],omega=s['Input']['Omega'],crank=s['CrankOmega'],pending=s['Input']['Pending'],pose=min(s['Inner'],s['Outer']),swept=s['Certificate']))
 policies=[]
 for p in sorted((OLD/'policies/native-dev-v5').glob('*.json.gz')):
  q=read(p);r=q['Review']
  if r['Time']<47:continue
  policies.append(dict(time=r['Time'],goal=r['Goal'],safety_goal=r['SafetyGoal'],certificate=r['ConditionalCertificate'],independent=r['Independent'],rejected=sum(not a['Legal'] for a in r['Attempts']),offset=q['Observed']['Input']['Rider']['Road']['OffsetMeters'],nominal=r['Nominal'],tape_count=len(r['Tapes']),cpu=r['CpuSeconds']))
 last=read(OLD/'policies/native-dev-v5/decision-0334.json.gz')['Review'];portfolio=[]
 for key,t in last['Tapes'].items():portfolio.append(dict(id=key,start=t['Start'],created=t['CreatedAt'],goal=t['Goal'],safety_goal=t['SafetyGoal'],actions=t['Actions'],arrival=t['Arrival'],safety=t['Safety']))
 tail=read(OLD/'forecasts/bounded-tail-v6.json.gz');rows=[]
 for a in tail['Actions']:
  tr=a['Trace'] or {};target=tr.get('Target',{});s=a['State']['Input'];v=s['Rider']
  rows.append(dict(time=s['Time'],reference=v['TotalReferenceMeters'],speed=v['Speed'],offset=v['Road']['OffsetMeters'],beta=v['SlideRadians'],target_speed=target.get('SpeedTarget'),action=a['Action'],pending=s['Pending']))
 spikes=[q for q in rows if q['target_speed'] is not None and q['target_speed']>2*q['speed']]
 result=dict(base='e8866e91c4c3818b8ca836260883ca847ccc4a8a',native_end=ss[-1],last20s=display,reviews=policies,v6_saved_tail=rows,first_target_spike=spikes[0],last_accepted_review=last['Time'],last_accepted_safety_goal=last['SafetyGoal'],required_next_safety_goal=tail['Goal'],portfolio=dict(selected=last['Nominal'],tapes=portfolio,tree=last['Tree'],terminals=last['Terminals'],physical_hash=last['PhysicalHash'],reconstruction='Physical native state and ordered policy inputs recovered. Remember clears and stores selected plus nine conditional tapes. Dense future state arrays, caches and the failed67s callback stack/portfolio snapshot were not stored. This is structural reconstruction, not a bit-exact replay of the complete decision process.'),native_worlds_created=0,forecasts_run=0,cpu_seconds=time.process_time()-began)
 save(D/'historical-forensics.json.gz',result);print(json.dumps(dict(first_spike=spikes[0],portfolio_tapes=len(portfolio),cpu_seconds=result['cpu_seconds']),indent=2))
if __name__=='__main__':main()
