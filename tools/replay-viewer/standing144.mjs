// Presentation of accepted states only. No launch model, forecasting or trajectory synthesis.
import {Replay,authoredReplayDocument} from './model.mjs';
import {readGzip} from './recordings.mjs';
export const standingRoot='../../docs/experiments/standing-start-three-laps/';
export function standingReplay(run){
  const rows=run.Riders.flatMap(r=>r.Samples.map(s=>{
    const i=s.Input,v=i.Rider;
    return {t:i.Time,id:v.Id,x:v.Position.X,y:v.Position.Y,vx:v.Velocity.X,vy:v.Velocity.Y,speed:v.Speed,heading:v.Heading,
      reference:v.TotalReferenceMeters,offset:v.Road.OffsetMeters,target:null,drive:v.Drive,activity:v.Activity,steering:i.Steering,
      requestedSteering:s.RequestedSteering,requestedDrive:s.RequestedDrive,inner:s.Inner,outer:s.Outer,omega:i.Omega,crankOmega:s.CrankOmega,source:'native standing144'};
  }));
  const decisions=run.Riders.flatMap(r=>r.Decisions.map(d=>({RiderId:r.Spec.Profile.Id,OpponentId:0,Action:0,Intent:0,Channels:2,
    ObservedAt:d.Choice.Time,DecisionAt:d.Choice.Time,RequestedActivation:d.Choice.Time+.24,
    ActualActivation:r.Activations.find(a=>Math.abs(a.ObservedAt-d.Choice.Time)<1e-8)?.ActivationAt??null,
    TargetOffsetMeters:0,Drive:d.Choice.Action.Drive,Reason:`${run.Variant}: gaz ${(d.Choice.Action.Drive*100).toFixed(1)}%, skręt ${(d.Choice.Action.SteeringRadians*180/Math.PI).toFixed(2)}°${d.Choice.NoSafeContinuation?' · brak Safe w H09':''}`})));
  const initial=run.Riders.map(r=>r.Samples[0].Input.Rider),final=run.Riders.map(r=>r.Samples.at(-1).Input.Rider);
  const finish=Object.fromEntries(run.Riders.filter(r=>r.Outcome==='completed').map(r=>[r.Spec.Profile.Id,r.FinishSeconds]));
  const doc=authoredReplayDocument({Initial:{Riders:initial},Result:{TimeSeconds:run.EndSeconds,Riders:final,Decisions:decisions,Contacts:run.Contacts,FinishTimes:finish},CrossingSeconds:[]},3);
  doc.Run.Riders=run.Riders.map(r=>({Measurement:{RiderId:r.Spec.Profile.Id,CrossingSeconds:run.Crossings.filter(c=>c.RiderId===r.Spec.Profile.Id).map(c=>c.Time)}}));
  return new Replay(rows,doc);
}
export async function loadStanding144(fetcher=fetch,name=null,root=standingRoot){
  const json=async url=>{const r=await fetcher(url);if(!r.ok)throw Error('Brak danych '+url);return r.json();};
  const index=await json(root+'index.json');name=name??index.DefaultRun;
  const selected=index.Runs.find(r=>r.Id===name);if(!selected)throw Error('Nieznany zapis #144.');
  const [run,geometry,metrics]=await Promise.all([fetcher(root+selected.File).then(readGzip).then(JSON.parse),json(root+index.GeometryFile),json(root+'lap-metrics.json')]);
  const data={index,run,geometry,metrics:metrics.filter(m=>m.Run===name),root};data.replay=standingReplay(run);data.replay.standingData=data;return data;
}
export function standingReadout(data,time,riderId){
  const rider=data.run.Riders.find(r=>r.Spec.Profile.Id===riderId)??data.run.Riders[0];
  const sample=rider.Samples.findLast(s=>s.Input.Time<=time+1e-9)??rider.Samples[0];
  const decision=rider.Decisions.findLast(d=>d.Choice.Time<=time+1e-9)??null;
  return {rider,sample,decision,completedLaps:data.run.Crossings.filter(c=>c.RiderId===rider.Spec.Profile.Id&&c.Time<=time+1e-9).length};
}
export function standingMilestones(data,riderId){
  const r=data.run.Riders.find(q=>q.Spec.Profile.Id===riderId)??data.run.Riders[0],samples=r.Samples,L=data.run.LapMeters;
  const events=[{Label:'Taśma / start',Time:0,Kind:'start'}];
  for(let lap=0;lap<3;lap++)for(const [j,ref] of data.index.BendReferences.entries()){
    const s=samples.find(q=>q.Input.Rider.TotalReferenceMeters>=lap*L+ref);if(s)events.push({Label:`Okr.${lap+1}: łuk${j<2?1:2} ${j%2?'koniec':'początek'}`,Time:s.Input.Time,Kind:'bend'});
  }
  for(const c of data.run.Crossings.filter(q=>q.RiderId===r.Spec.Profile.Id))events.push({Label:`Start/meta — koniec okr.${c.Lap}`,Time:c.AfterTime,ScalarCrossingTime:c.Time,Kind:'crossing'});
  for(const d of r.Decisions.filter(q=>q.Choice.NoSafeContinuation))events.push({Label:'Brak Safe w H09 — obserwacja',Time:d.Choice.Time,Kind:'uncertain'});
  return events.sort((a,b)=>a.Time-b.Time);
}
export function setupStanding144(element,data,onRun,onJump){
  element.replaceChildren();const title=document.createElement('h2');title.textContent='#144 · v=0 → trzy okrążenia · '+data.run.Outcome;element.append(title);
  const controls=document.createElement('div');controls.className='audit-controls';const label=document.createElement('label');label.textContent='Natywny przejazd ';
  const select=document.createElement('select');select.id='standing-run';
  for(const run of data.index.Runs){const o=document.createElement('option');o.value=run.Id;o.textContent=run.Id+' · '+run.Riders+' motocykli · '+run.Outcome;o.selected=run.Id===data.run.Name;select.append(o);}
  select.addEventListener('change',()=>onRun(select.value));label.append(select);controls.append(label);
  for(let lap=1;lap<=3;lap++){
    const c=data.run.Crossings.find(c=>c.RiderId===data.run.Riders[0].Spec.Profile.Id&&c.Lap===lap-1),t=lap===1?0:c?.AfterTime;
    const b=document.createElement('button');b.id='standing-lap-'+lap;b.textContent='Okrążenie '+lap;b.disabled=t==null;b.addEventListener('click',()=>onJump(t));controls.append(b);
  }
  element.append(controls);const current=document.createElement('div');current.id='standing-telemetry';element.append(current);
  const details=document.createElement('details'),summary=document.createElement('summary');summary.textContent='Start, każdy łuk, każde przekroczenie start/meta i problematyczne decyzje';details.append(summary);
  for(const rider of data.run.Riders){
    const p=document.createElement('p');p.textContent=`R${rider.Spec.Profile.Id} · pole ${['A','B','C','D'][rider.Gate]} · ${rider.CompletedLaps}/3 · ${rider.Outcome}`;details.append(p);
    for(const event of standingMilestones(data,rider.Spec.Profile.Id)){
      const b=document.createElement('button');b.textContent=`R${rider.Spec.Profile.Id}: ${event.Label} · ${event.Time.toFixed(2)}s`;b.addEventListener('click',()=>onJump(event.Time,rider.Spec.Profile.Id));details.append(b);
    }
  }
  element.append(details);
  const link=document.createElement('a');link.href=data.root+'README.md';link.textContent='Pełne trzy okrążenia: metryki, zakres i ograniczenia';element.append(link);
  standingPanel144(current,data,0,data.run.Riders[0].Spec.Profile.Id);
}
export function standingPanel144(element,data,time,riderId){
  if(!element)return;element.replaceChildren();const add=text=>{const p=document.createElement('p');p.textContent=text;element.append(p);};
  const {rider:r,sample:s,decision:d,completedLaps}=standingReadout(data,time,riderId);const v=s.Input.Rider;
  add(`${data.run.Variant} · R${r.Spec.Profile.Id} · pole ${['A','B','C','D'][r.Gate]} · okrążenie ${Math.min(3,completedLaps+1)}/3, ukończono ${completedLaps}. Wynik całego zapisu: ${r.Outcome}.`);
  add(`Native ${s.Input.Time.toFixed(3)}s · ${(v.Speed*3.6).toFixed(2)}km/h · gaz żądany / aktywny ${(s.RequestedDrive*100).toFixed(1)} / ${(s.Input.Active.Drive*100).toFixed(1)}% · skręt żądany / wykonany ${(s.RequestedSteering*180/Math.PI).toFixed(2)} / ${(s.Input.Steering*180/Math.PI).toFixed(2)}°.`);
  add(`β ${(v.SlideRadians*180/Math.PI).toFixed(2)}° · yaw ${v.AngularVelocity.toFixed(3)}rad/s · koło ${s.Input.Omega.toFixed(2)}rad/s · wał ${(s.CrankOmega*30/Math.PI).toFixed(0)}RPM · pending ${s.Input.Pending.length}. Sterowanie próbki dotyczy ostatniego zakończonego przedziału.`);
  add(`Chwilowy gap kreda / banda ${s.Inner.toFixed(4)} / ${s.Outer.toFixed(4)}m · certyfikat ${s.Certificate?.toFixed(4)??'N/D'}m. Brak certyfikacji nie jest kolizją.`);
  if(d)add(`Decyzja ${d.Choice.Time.toFixed(2)}s · ${d.Choice.Forecasts} prognoz · ${d.CpuSeconds.toFixed(4)}CPU-s · ${data.analysis146?'ocena sterowania z wybranego zapisu':d.Choice.NoSafeContinuation?'brak Safe w H09':'klasa Safe dostępna w H09'}. To nie certyfikat dalszego okrążenia.`);
  if(s.Powertrain)add(`Sprzęgło ${s.Powertrain.ClutchTorque.toFixed(2)}Nm · slip ${s.Powertrain.SlipRadiansPerSecond.toFixed(2)}rad/s · energia wału / sprzęgła jest ciągła, bez resetu na mecie.`);
  const m=data.metrics.find(q=>q.RiderId===r.Spec.Profile.Id);
  if(m)add('Czasy okrążeń: '+m.Laps.map(q=>q.LapSeconds==null?'N/D':q.LapSeconds.toFixed(3)+'s').join(' / ')+'. Start nie liczy się jako ukończone okrążenie.');
}
