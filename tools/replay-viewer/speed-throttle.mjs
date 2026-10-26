// Presentation only: every marker anchors to a recorded Native Box2D state.
export function throttleMarkers(data){
  const run=data.comparisonRuns?.find(r=>r.policy!=='A')?.run??data.document,group=data.group,events=group.Events;
  const samples=run.Replay.Samples;
  const nearest=t=>samples.reduce((a,b)=>Math.abs(b.Input.Time-t)<Math.abs(a.Input.Time-t)?b:a);
  const marks=[];
  const add=(sample,label,color)=>{if(sample){const p=sample.Input.Rider.Position;marks.push({x:p.X,y:p.Y,time:sample.Input.Time,label,color});}};
  if(events?.Landmarks?.Exit){
    const end=events.Landmarks.Exit.Time;
    add(nearest(end),'Geometryczny koniec łuku','#b4f1ba');
    // This is only the latency deadline; it is never claimed to be a proven safe maneuver.
    const deadline=samples.filter(s=>s.Input.Time<=end-.24+1e-9).at(-1);
    add(deadline,'Termin reakcji −0,24s (warunek czasowy)','#d99fe3');
  }
  if(events?.ObservedAt!=null)add(nearest(events.ObservedAt),'Żądanie gaz1 (diagnostyka)','#ffd27b');
  if(events?.ActivationAt!=null)add(nearest(events.ActivationAt),'Rzeczywista aktywacja gaz1','#73dfdf');
  return marks;
}
export function rejectedFull(run,time){
  const d=run.Replay.Decisions.filter(x=>x.Time<=time+1e-9).at(-1);
  if(!d)return null;
  const chosen=d.Candidates.find(x=>x.Action.Drive===d.Action.Drive&&x.Action.SteeringRadians===d.Action.SteeringRadians);
  const full=d.Candidates.find(x=>x.Action.Drive===1&&x.Action.SteeringRadians===d.Action.SteeringRadians);
  if(!full||!chosen||d.Action.Drive>=.99)return null;
  return {time:d.Time,chosen,full,sameSafety:full.Safety===chosen.Safety,moreProgress:full.Progress>chosen.Progress};
}
export function speedThrottlePanel(element,data,time){
  if(!element)return;element.replaceChildren();
  const add=text=>{const p=document.createElement('p');p.textContent=text;element.append(p);};
  add('#136 · A: oryginalne AI #16015. B/C/D: kontrolowana diagnostyka offline, potem powrót do A. Żaden wymuszony gaz nie jest nową wyuczoną polityką.');
  add(data.group.PairedSummary);
  const ss=data.document.Replay.Samples,s=ss.filter(x=>x.Input.Time<=time+1e-9).at(-1)??ss[0],v=s.Input.Rider;
  add(`Natywna prędkość ${(v.Speed*3.6).toFixed(2)} km/h · gaz żądany ${(100*s.RequestedDrive).toFixed(1)}% / aktywny ${(100*s.Input.Active.Drive).toFixed(1)}%.`);
  const t=data.document.Telemetry.filter(x=>x.Time<=time+1e-9).at(-1);
  if(t){const f=t.Forces,w=t.Wheel,speed=Math.max(.1,v.Speed),a=(f.NetForce.X*v.Velocity.X+f.NetForce.Y*v.Velocity.Y)/(147*speed);
    add(`Ostatni zakończony przedział: moment silnika dostępny ${f.Engine.CrankTorqueNewtonMeters.toFixed(2)} Nm · rzeczywisty moment na kole ${w.DriveTorqueNewtonMeters.toFixed(2)} Nm · ${(f.Engine.EngineRpm).toFixed(0)} RPM · moc na kole ${(w.EngineWorkJoules/w.Seconds/1000).toFixed(2)} kW · przyspieszenie styczne ${a.toFixed(3)} m/s².`);
  }
  for(const m of throttleMarkers(data))add(`${m.label}: ${m.time.toFixed(3)}s ${time>=m.time?'✓':'(przed zdarzeniem)'}`);
  add('Punkty zdarzeń odnoszą się do wariantu diagnostycznego na jego natywnym śladzie, także przy wybranym A. Fioletowy punkt jest kinematycznym terminem reakcji, nie dowodem, że manewr w tym stanie był bezpieczny. Geometryczny koniec łuku różni się od bramki pełnego obrysu.');
  const r=rejectedFull(data.document,time);
  if(r)add(`AI odrzuciło gaz1 przy tej samej kierownicy w ${r.time.toFixed(2)}s: koszt ${r.full.Score.toFixed(4)} wobec ${r.chosen.Score.toFixed(4)}; klasa ${r.full.Safety} wobec ${r.chosen.Safety}; postęp ${r.full.Progress.toFixed(3)} wobec ${r.chosen.Progress.toFixed(3)}m. ${r.sameSafety&&r.moreProgress?'Ta sama klasa i większy postęp, ale gorszy koszt.':'Różnicę bezpieczeństwa i składników sprawdź w costs.json.'}`);
}
