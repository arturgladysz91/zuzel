// Read-only presentation. Planned second controls are never represented as executed controls.
export function causalReadout(data,time){
  const variant=data.comparisonRuns.find(r=>r.policy!=='A').run;
  const plan=(data.causalPlans??[]).filter(p=>p.Time<=time+1e-9).at(-1)??null;
  const sample=variant.Replay.Samples.filter(s=>s.Input.Time<=time+1e-9).at(-1)??variant.Replay.Samples[0];
  const next=plan&&time>=plan.Time+.2-1e-9?variant.Replay.Decisions.find(d=>Math.abs(d.Time-plan.Time-.2)<1e-8)??null:null;
  const previousPlan=(data.causalPlans??[]).filter(p=>p.Second&&p.Time+.2<=time+1e-9).at(-1)??null;
  const previousActual=previousPlan?variant.Replay.Decisions.find(d=>Math.abs(d.Time-previousPlan.Time-.2)<1e-8)??null:null;
  return {plan,sample,actualNext:next,previousPlan,previousActual,variant};
}
export function causalMarkers(data){
  const variant=data.comparisonRuns.find(r=>r.policy!=='A').run;
  const colors={request:'#ffd27b',activation:'#73dfdf',correction:'#edb1ec',exit:'#b4d985',failure:'#ff8b87'};
  return (data.group.Events??[]).map(event=>{
    const sample=variant.Replay.Samples.reduce((a,b)=>Math.abs(b.Input.Time-event.Time)<Math.abs(a.Input.Time-event.Time)?b:a);
    const shortLabel={request:'Nowy gaz1 — A odrzuca',activation:'Aktywny gaz1',correction:'Żądanie korekty kierownicy',exit:event.Label,failure:'DNF — brak certyfikacji'}[event.Kind]??event.Label;
    return {x:sample.Input.Rider.Position.X,y:sample.Input.Rider.Position.Y,time:sample.Input.Time,label:event.Label,shortLabel,color:colors[event.Kind]??'#fff'};
  });
}
export function causalPanel(element,data,time){
  if(!element)return;element.replaceChildren();
  const add=text=>{const p=document.createElement('p');p.textContent=text;element.append(p);};
  add('#137 · A: oryginalne #134 A #16015. Nowy wariant sam wybiera pierwszą akcję co0,2s; gaz nie jest wymuszany. Rzeczywista kolejka reakcji0,24s pozostaje.');
  add(data.group.PairedSummary);
  const {plan,sample,actualNext,previousPlan,previousActual}=causalReadout(data,time);
  add(`Natywny stan nowego wariantu ${sample.Input.Time.toFixed(3)}s: ${(sample.Input.Rider.Speed*3.6).toFixed(2)}km/h · gaz żądany ${(sample.RequestedDrive*100).toFixed(1)}% / aktywny ${(sample.Input.Active.Drive*100).toFixed(1)}% · yaw ${sample.Input.Rider.AngularVelocity.toFixed(3)}rad/s · β ${(sample.Input.Rider.SlideRadians*180/Math.PI).toFixed(2)}°.`);
  if(plan){
    const action=a=>`gaz ${(a.Drive*100).toFixed(1)}%, skręt ${(a.SteeringRadians*180/Math.PI).toFixed(2)}°`;
    add(`W ${plan.Time.toFixed(2)}s wybrano: ${action(plan.First)}; aktywacja przez kolejkę ${ (plan.Time+.24).toFixed(2)}s. Koszt wyszukiwania ${plan.CpuSeconds.toFixed(4)}CPU-s, ${plan.Forecasts} prognoz, ${plan.Contacts} ocen kontaktu.`);
    if(plan.BaselineFirst)add(`Oryginalna ocena A na TEJ SAMEJ obserwacji nowego wariantu: ${action(plan.BaselineFirst)}. To porównanie decyzji na jednym stanie, nie utożsamienie późniejszych trajektorii A/B.`);
    if(plan.Second){
      add(`Prognozowana druga akcja w ${(plan.Time+.2).toFixed(2)}s: ${action(plan.Second)}, możliwa aktywacja ${(plan.Time+.44).toFixed(2)}s. Nie jest to zobowiązanie do jej wykonania.`);
      add(actualNext?`Rzeczywisty kolejny wybór na nowej obserwacji: ${action(actualNext.Action)}. ${actualNext.Action.Drive===plan.Second.Drive&&actualNext.Action.SteeringRadians===plan.Second.SteeringRadians?'Zgodny z poprzednio przewidzianą akcją.':'Różni się od poprzedniej prognozy — replanowanie zmieniło kontynuację.'}`:'Kolejna rzeczywista decyzja jeszcze nie nastąpiła w odtwarzanym czasie.');
    }
    if(plan.TerminalYawCost!=null)add(`Yaw cost terminalny ${plan.TerminalYawCost.toFixed(4)}, średni poH09 ${plan.MeanYawCost.toFixed(4)}; throttle ${plan.ThrottleCost.toFixed(4)}, steering ${plan.SteeringCost.toFixed(4)}. Wariant ${plan.Arm}; ${plan.Second?'obie planowane komendy mają koszt':'jedna utrzymywana komenda ma koszt'}.`);
    if(previousPlan&&previousActual)add(`Weryfikacja poprzedniej prognozy z ${previousPlan.Time.toFixed(2)}s: druga akcja miała mieć ${action(previousPlan.Second)}; rzeczywisty wybór w ${previousActual.Time.toFixed(2)}s to ${action(previousActual.Action)}. ${previousActual.Action.Drive===previousPlan.Second.Drive&&previousActual.Action.SteeringRadians===previousPlan.Second.SteeringRadians?'Prognoza zgodna.':'Replanowanie zmieniło kontynuację.'}`);
  }
  add('Znaczniki na mapie dotyczą rzeczywistych zdarzeń nowego wariantu na jego natywnym śladzie. Prognozy są w osobnym wykresie niżej; nie zastępują Native Box2D.');
  for(const m of causalMarkers(data))add(`${m.label}: ${m.time.toFixed(3)}s ${time>=m.time?'✓':'(przyszłe w odtwarzaniu)'}`);
}
