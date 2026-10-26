// Presentation only. Every pose, activation and control response comes from native samples.
export function planningReadout(plan,run,time){
  const events=(plan?.Actions??[]).map((a,j)=>{
    const observation=plan.Time+j*.2,activation=observation+plan.ReactionSeconds;
    const actual=run.Replay.Activations.find(x=>Math.abs(x.ObservedAt-observation)<1e-8&&x.Action.Drive===a.Drive&&x.Action.SteeringRadians===a.SteeringRadians);
    return {action:a,observation,activation,actual:actual?.ActivationAt??null,status:time<observation-1e-8?'przyszłe żądanie':time<activation-1e-8?'pending':actual&&actual.ActivationAt<=time+1e-8?'aktywna lub zastąpiona':'nie doszło do aktywacji'};
  });
  const sample=run.Replay.Samples.filter(s=>s.Input.Time<=time+1e-10).at(-1)??run.Replay.Samples[0];
  return {events,sample};
}
export function planningPanel(element,plan,run,time,group){
  if(!element)return;
  element.replaceChildren();const add=text=>{const p=document.createElement('p');p.textContent=text;element.append(p);};
  add('PR #135 · sekwencja przyczynowa wybrana wyłącznie w H09. Potem wraca autonomiczny #134 A. Oracle to osobna retrospektywna granica badanego zbioru.');
  add(group.PairedSummary);
  if(!plan){add('A: oryginalny #134 A #16015. Pojedyncza utrzymana komenda w prognozie, nowa decyzja natywna co 0,2 s.');return;}
  const {events,sample}=planningReadout(plan,run,time);
  add(`${plan.Arm}: ${plan.Basis}. Obserwacja ${plan.Time.toFixed(2)} s; reakcja ${plan.ReactionSeconds.toFixed(2)} s.`);
  for(const e of events)add(`Żądanie ${e.observation.toFixed(2)} → nominalna aktywacja ${e.activation.toFixed(2)} → natywna ${e.actual?.toFixed(2)??'brak'} s: gaz ${(e.action.Drive*100).toFixed(1)}%, skręt ${(e.action.SteeringRadians*180/Math.PI).toFixed(2)}° · ${e.status}.`);
  add(`Teraz ${sample.Input.Time.toFixed(3)} s: kolejka ${sample.Input.Pending.map(c=>`${c.ObservedAt.toFixed(2)}→${c.ActivationAt.toFixed(2)}`).join(', ')||'pusta'}. Rzeczywisty yaw ${sample.Input.Rider.AngularVelocity.toFixed(3)} rad/s. Cały wcześniejszy prefiks pochodzi z jednego ciągłego świata.`);
}
