// Static geometry facts and accepted native states; no simulation or target line.
export function awarenessReadout(run,time){
  const d=run.Replay.Decisions.filter(x=>x.Time<=time+1e-10).at(-1);
  const s=run.Replay.Samples.filter(x=>x.Input.Time<=time+1e-10).at(-1)??run.Replay.Samples[0];
  const chosen=d?.Candidates.find(c=>c.Action.SteeringRadians===d.Action.SteeringRadians&&c.Action.Drive===d.Action.Drive);
  const activation=d?run.Replay.Activations.find(a=>Math.abs(a.ObservedAt-d.Time)<1e-8):null;
  const observation=d?run.Replay.Samples.find(x=>Math.abs(x.Input.Time-d.Time)<1e-8):null;
  return {decision:d,sample:s,observation,knowledge:d?.TrackKnowledge??null,reachability:chosen?.TrackAwareness??null,activation};
}
export function knowledgeEdges(k,innerReferenceOffsetMeters=1){
  if(!k)return [];
  // At(ref,0) is the exported reference, one metre outwards from the kerb.
  return k.Profile.map(p=>({reference:p.ReferenceMeters,curvature:p.Curvature,inner:{x:p.X-p.OutwardX*innerReferenceOffsetMeters,y:p.Y-p.OutwardY*innerReferenceOffsetMeters},outer:{x:p.X+p.OutwardX*(p.WidthMeters-innerReferenceOffsetMeters),y:p.Y+p.OutwardY*(p.WidthMeters-innerReferenceOffsetMeters)}}));
}
// Exact actuator-only continuation for known pending commands, then a held
// extreme of the inherited +/-15deg action grid. Arrival time is an estimate;
// this does not predict the body's yaw or its trajectory.
export function actuatorEnvelope(input,seconds){
  const rate=Math.PI/2,limit=35*Math.PI/180,end=input.Time+Math.max(0,seconds);
  const low=Math.min(-15*Math.PI/180,input.Steering,input.Active.SteeringRadians),high=Math.max(15*Math.PI/180,input.Steering,input.Active.SteeringRadians);
  function held(target){
    let time=input.Time,angle=input.Steering,active=input.Active.SteeringRadians;
    const events=input.Pending.filter(c=>c.ObservedAt<input.Time-1e-8).map(c=>({time:c.ActivationAt,target:c.Action.SteeringRadians}));
    events.push({time:input.Time+.24,target});events.sort((a,b)=>a.time-b.time);
    const move=until=>{const dt=Math.max(0,until-time);angle+=Math.max(-rate*dt,Math.min(rate*dt,Math.max(-limit,Math.min(limit,active))-angle));time=until;};
    for(const e of events){if(e.time>end+1e-10)break;move(Math.max(input.Time,e.time));active=e.target;}
    move(end);return angle;
  }
  return {low:held(low),high:held(high),seconds,scope:'known pending then held grid extrema; actuator only, no yaw/trajectory guarantee'};
}
export function awarenessPanel(element,r){
  element.replaceChildren();const add=t=>{const p=document.createElement('p');p.textContent=t;element.append(p);};
  const k=r.knowledge,d=r.decision,s=r.sample,f=(v,n=2)=>v==null?'—':v.toFixed(n);
  if(!k){add('A: stara ocena jednego punktu geometrii za końcem H09. Brak nowego profilu w decyzji.');return;}
  add(`${k.Geometry} · obserwacja ${f(d.Time)}s · następny łuk od ref.${f(k.NextCornerStartReferenceMeters)} do ${f(k.NextCornerEndReferenceMeters)}m, długość ${f(k.NextCornerLengthMeters)}m, kierunek ${k.NextCornerDirection}; prosta za nim ${f(k.FollowingStraightMeters)}m.`);
  add(`Od obserwacji: do łuku ${f(k.DistanceToNextCornerMeters)}m / około ${f(k.SecondsToNextCorner)}s; do końca bieżącego łuku ${f(k.DistanceToCurrentCornerEndMeters)}m. Profil wiedzy ${f(k.KnowledgeRangeMeters)}m. Kolorowe poprzeczne kreski i brzegi to obszar geometrii; bez zadanej linii jazdy.`);
  const change=k.Profile.find(p=>Math.abs(p.Curvature-k.Profile[0].Curvature)>1e-6);
  add(`Najbliższa próbka zmiany κ o ponad0,000001: ${f(change?.MetricDistanceAtCurrentOffsetMeters)}m / około ${f(change?.ArrivalSecondsAtCurrentVelocity)}s. Próbki co2m oraz dokładne złączenia odcinków.`);
  const range=k.SecondsToNextCorner==null||!r.observation?null:actuatorEnvelope(r.observation.Input,k.SecondsToNextCorner);
  add(`Reakcja: ${f(k.ReactionDistanceMeters)}m w 0,24s; kierownica z obecną kolejką i potem utrzymaną skrajną akcją gridu: ${range?f(range.low*180/Math.PI)+'…'+f(range.high*180/Math.PI):'—'}°. Czas dojścia jest przybliżony; to nie zakres yaw/χ. Oryginalne pole ReachableSteering w archiwum pomijało aktywacje pending i nie służy do tej diagnozy.`);
  add(`Żądanie ${f(d.Time)}s → pending do ${f(d.Time+.24)}s → aktywacja ${r.activation&&r.activation.ActivationAt<=s.Input.Time?f(r.activation.ActivationAt)+'s':'oczekuje'}. Wykonany skręt ${f(s.Input.Steering*180/Math.PI)}°, yaw ${f(s.Input.Rider.AngularVelocity,3)}rad/s, χ ${f(Math.atan2(s.Input.Rider.Velocity.Y,s.Input.Rider.Velocity.X)*180/Math.PI)}°.`);
  const h=r.reachability;if(h)add(`Heurystyka osobno od H09: reszta prędkości ${f(h.ResidualSpeedMetersPerSecond)}m/s, geometria krytyczna za ${f(h.CriticalDistanceMeters)}m / ${f(h.SecondsToCriticalGeometry)}s; obserwowane odpuszczenie ${f(h.CoastDecelerationObserved)}m/s², yaw authority ${f(h.YawAccelerationObserved)}rad/s². Margines legacy ${f(h.TyreResponseSeconds)}s nie jest opóźnieniem opony w fizyce. ${h.Classification}.`);
  const svg=document.createElementNS('http://www.w3.org/2000/svg','svg');svg.setAttribute('viewBox','0 0 600 110');svg.setAttribute('role','img');svg.setAttribute('aria-label','Krzywizna w geometrycznym profilu wiedzy');
  const max=Math.max(.001,...k.Profile.map(p=>Math.abs(p.Curvature)));const path=document.createElementNS(svg.namespaceURI,'path');path.setAttribute('d',k.Profile.map((p,i)=>`${i?'L':'M'}${20+560*p.ReferenceDistanceMeters/k.KnowledgeRangeMeters},${90-70*Math.abs(p.Curvature)/max}`).join(' '));path.setAttribute('fill','none');path.setAttribute('stroke','#d99fe3');path.setAttribute('stroke-width','2');svg.append(path);element.append(svg);
  add(`Profil κ: 0…${f(max,4)} 1/m; szerokość ${f(Math.min(...k.Profile.map(p=>p.WidthMeters)))}…${f(Math.max(...k.Profile.map(p=>p.WidthMeters)))}m. Czasy dojścia zakładają obecną prędkość, nie prognozę całego łuku.`);
}
