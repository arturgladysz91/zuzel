// Displays accepted Box2D states. It cannot train, forecast or change a policy.
import {freeRacingReplay} from './free-racing.mjs';
import {readGzip} from './recordings.mjs';
export const learningRoot='../../docs/experiments/free-racing-learning/';
export const learningColors=['#73dfdf','#ffd27b','#d99fe3','#8fc8ff','#b4d985','#ff9c86','#e8eff5'];
export const learningLabels={'overlay-initial':'Początkowa · DNF','overlay-first-improvement':'Pierwsza poprawa · generacja3','overlay-first-safe-bend':'Pierwszy bezpieczny łuk · generacja3','overlay-after-100':'Po100 próbach','overlay-after-200':'Po200 próbach','overlay-final':'Końcowa · generacja32'};
export function learningReplay(run){
  const replay=freeRacingReplay(run);delete replay.freeRacing;replay.learning=run;
  for(const state of replay.tracks.values())for(const p of state)p.source='native Free Racing Learning';
  return replay;
}
export function learningReadout(replay,time){
  const run=replay.learning;
  return {run,sample:run.Samples.filter(s=>s.Input.Time<=time+1e-10).at(-1)??run.Samples[0],
    decision:run.Decisions.filter(d=>d.Time<=time+1e-10).at(-1)};
}
export async function loadLearning(fetcher=fetch,name='overlay-final'){
  const json=async path=>{const r=await fetcher(path);if(!r.ok)throw new Error(`Brak zapisu ${path}`);return r.json();};
  const episode=async id=>JSON.parse(await readGzip(await fetcher(learningRoot+id+'.json.gz')));
  const [index,geometry,run]=await Promise.all([json(learningRoot+'index.json'),json('../../docs/experiments/progressive-bend-width/progressive-b-geometry.json'),episode(name)]);
  const overlays=await Promise.all(index.Milestones.map(episode));
  const comparisons=overlays.map((r,i)=>({name:learningLabels[r.Name]??r.Name,color:learningColors[i%learningColors.length],planned:false,
    points:r.Samples.map(s=>({x:s.Input.Rider.Position.X,y:s.Input.Rider.Position.Y}))}));
  const baseline=await json(learningRoot+'matched-124.json');
  comparisons.push({name:`#124 · identyczny stan · ${baseline.Completed?'ukończony':'DNF'}`,color:'#ffb36d',planned:false,
    points:baseline.States.map(s=>({x:s.Riders[0].Position.X,y:s.Riders[0].Position.Y}))});
  return {index,geometry,run,replay:learningReplay(run),comparisons};
}
export function setupLearning(element,data,onRun){
  element.replaceChildren();const title=document.createElement('h2');title.textContent=`Free Racing Learning · ${data.index.Grade}`;element.append(title);
  const label=document.createElement('label');label.textContent='Natywny przejazd ';const select=document.createElement('select');select.id='learning-run';
  const milestoneGroup=document.createElement('optgroup');milestoneGroup.label='Kamienie milowe · identyczny stan 9m/s, offset4m';
  const otherGroup=document.createElement('optgroup');otherGroup.label='Trening i walidacja · różne warunki początkowe';
  for(const name of [...data.index.Milestones,...data.index.Runs.map(r=>r.Name).filter(n=>!data.index.Milestones.includes(n))]){
    const option=document.createElement('option');option.value=name;option.textContent=learningLabels[name]??name;option.selected=name===data.run.Name;
    (data.index.Milestones.includes(name)?milestoneGroup:otherGroup).append(option);
  }
  select.append(milestoneGroup,otherGroup);select.addEventListener('change',()=>onRun(select.value));label.append(select);element.append(label);
  const note=document.createElement('p');note.textContent='Wybór przejazdu zmienia wyłącznie odtwarzany zapis. Nakładki kamieni milowych i #124 pochodzą z identycznego stanu wejściowego. Wynik DNF nie ma czasu ukończenia.';element.append(note);
  const telemetry=document.createElement('div');telemetry.id='learning-telemetry';element.append(telemetry);
  for(const [file,caption] of [['learning-progress.svg','Postęp, ukończenia i czas — kolory oznaczają poziomy curriculum'],['policy-parameters.svg','Zmiany ośmiu parametrów'],['termination-causes.svg','Przyczyny zakończenia prób'],['corner-speeds.svg','Prędkości wejścia, minimum i wyjścia'],['native-trajectories.svg','Rzeczywiste trajektorie na wspólnym stanie']]){
    const details=document.createElement('details'),summary=document.createElement('summary'),img=document.createElement('img');summary.textContent=caption;img.src=learningRoot+file;img.alt=caption;img.style.width='100%';details.append(summary,img);element.append(details);
  }
  const report=document.createElement('a');report.href=learningRoot+'README.md';report.textContent='Pełny raport i ograniczenia';element.append(report);
}
export function updateLearningPanel(element,replay,time){
  const {sample:s,decision:d,run}=learningReadout(replay,time),v=s.Input.Rider,m=run.Metrics;
  element.replaceChildren();const status=['bezpieczne w horyzoncie','niepewne','niebezpieczne'];
  const lines=[`${learningLabels[run.Name]??run.Name} · ${run.PolicyId>=0?'polityka '+run.PolicyId:'odtworzona polityka'} · początkowo ${run.Scenario.Speed}m/s / offset ${run.Scenario.Offset}m`,
    `Wynik: ${m.Outcome} · czas bramki ${m.Completed?m.GateSeconds.toFixed(2)+'s':'— (DNF)'} · legalny postęp ${m.LegalProgress.toFixed(2)}m`,
    `Gaz żądany / aktywny ${(s.RequestedDrive*100).toFixed(1)}% / ${(v.Drive*100).toFixed(1)}% · skręt żądany / wykonany ${(s.RequestedSteering*180/Math.PI).toFixed(3)}° / ${(s.Input.Steering*180/Math.PI).toFixed(3)}°`,
    `Kreda / banda (cały obrys): ${s.Inner.toFixed(4)} / ${s.Outer.toFixed(4)}m · yaw ${v.AngularVelocity.toFixed(4)}rad/s · koło ${s.Input.Omega.toFixed(3)}rad/s · pending ${s.Input.Pending.length}`,
    `Parametry: ${JSON.stringify(Object.fromEntries(Object.entries(run.Policy).filter(([key])=>key!=='Values')))}`];
  if(d)lines.push(`Decyzja z ${d.Time.toFixed(2)}s: ${status[d.Safety]} na 0,6s; ${d.NoSafeContinuation?'BRAK bezpiecznej kontynuacji w ocenionym zbiorze':'dostępna bezpieczna kontynuacja w horyzoncie'}. Reakcja 0,24s.`);
  for(const text of lines){const p=document.createElement('p');p.textContent=text;element.append(p);}
  if(!d)return;
  const details=document.createElement('details'),summary=document.createElement('summary'),table=document.createElement('table');
  summary.textContent=`${d.Candidates.length} rozpatrzonych działań · ${d.ForceEvaluations} ocen kontaktu`;const head=document.createElement('tr');
  for(const text of ['Skręt °','Gaz %','Bezpieczeństwo','Postęp m','Prześwit m','v m/s','β °','Koszt']){const th=document.createElement('th');th.textContent=text;head.append(th);}table.append(head);
  for(const c of d.Candidates){const row=document.createElement('tr');for(const x of [c.Action.SteeringRadians*180/Math.PI,c.Action.Drive*100,status[c.Safety],c.Progress,c.Clearance,c.ExitSpeed,c.ExitBeta*180/Math.PI,c.Score]){const td=document.createElement('td');td.textContent=typeof x==='number'?x.toFixed(3):x;row.append(td);}table.append(row);}
  details.append(summary,table);element.append(details);
}
