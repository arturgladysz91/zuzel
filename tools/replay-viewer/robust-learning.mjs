// Four policies on the same sealed initial state. All displayed poses are native observations.
import {learningReplay,learningReadout} from './free-learning.mjs';
import {readGzip} from './recordings.mjs';
export const robustRoot='../../docs/experiments/robust-free-racing-learning/';
export const robustColors={'129-100':'#73dfdf','129-200':'#ffd27b','129-final':'#d99fe3','130-final':'#ff9c86','diag-original':'#d99fe3','diag-intervention':'#73dfdf'};
export const robustLabels={'129-100':'#129 po 100','129-200':'#129 po 200','129-final':'#129 końcowa','130-final':'#130 wybrana przed testem','diag-original':'Diagnostyka: oryginalna #129','diag-intervention':'Diagnostyka: jedna komenda z0,9s'};
export function robustReplay(document){const replay=learningReplay(document.Replay);replay.robust=document;return replay;}
export function robustReadout(replay,time){
  const base=learningReadout(replay,time);return {...base,metrics:replay.robust.Metrics,
    telemetry:replay.robust.Telemetry.filter(t=>t.Time<=time+1e-10).at(-1)??null};
}
export async function loadRobust(fetcher=fetch,name=null,root=robustRoot,groupId=null){
  const json=async url=>{const r=await fetcher(url);if(!r.ok)throw new Error('Brak danych '+url);return r.json();};
  const [index,bundle,geometry]=await Promise.all([json(root+'index.json'),
    fetcher(root+'selected-replays.json.gz').then(readGzip).then(JSON.parse),
    json('../../docs/experiments/progressive-bend-width/progressive-b-geometry.json')]);
  const requestedGroup=groupId?index.Groups.find(g=>g.Id===groupId):null;
  name=name??requestedGroup?.Runs[0].Name??index.DefaultRun;const document=bundle.Runs[name];if(!document)throw new Error('Ten pełny replay jest lokalny i nie należy do wybranych zapisów w Git.');
  const group=requestedGroup?.Runs.some(r=>r.Name===name)?requestedGroup:index.Groups.find(g=>g.Runs.some(r=>r.Name===name));if(!group)throw new Error('Brak wspólnego scenariusza.');
  const labels=index.Labels??robustLabels;const colors=index.Colors??robustColors;
  const comparisons=group.Runs.map(r=>({name:labels[r.Policy]??r.Policy,policy:r.Policy,color:colors[r.Policy]??'#73dfdf',planned:false,visible:true,
    points:bundle.Runs[r.Name].Replay.Samples.map(s=>({x:s.Input.Rider.Position.X,y:s.Input.Rider.Position.Y}))}));
  const replay=robustReplay(document);replay.robustPolicy=group.Runs.find(r=>r.Name===name).Policy;
  replay.robustLabel=labels[replay.robustPolicy]??replay.robustPolicy;replay.robustColor=colors[replay.robustPolicy];replay.horizon=index.Horizons?.[replay.robustPolicy]??.6;
  replay.episodeScope=document.Replay.Metrics.Outcome==='probe'?'Sonda diagnostyczna · zaplanowany koniec':group.TwoBends?
    (document.Replay.Metrics.Completed?'Dwa kolejne łuki + 1s kontroli na wyjściu':'Dwa kolejne łuki · próba nieukończona'):undefined;
  return {index,group,document,replay,geometry,comparisons,root,labels};
}
const el=(tag,text)=>{const e=document.createElement(tag);if(text!=null)e.textContent=text;return e;};
const svgEl=(tag,attributes)=>{const e=document.createElementNS('http://www.w3.org/2000/svg',tag);for(const [k,v] of Object.entries(attributes))e.setAttribute(k,String(v));return e;};
function telemetryCharts(element,run){
  const samples=run.Replay.Samples;const degrees=x=>x*180/Math.PI;const duration=run.Replay.Metrics.DurationSeconds;
  const charts=[
    ['Kierownica ° · żądana złota / wykonana turkusowa',samples.map(s=>[s.Input.Time,degrees(s.RequestedSteering),degrees(s.Input.Steering)])],
    ['Gaz % · żądany złoty / aktywny turkusowy',samples.map(s=>[s.Input.Time,s.RequestedDrive*100,s.Input.Rider.Drive*100])],
    ['β °',samples.map(s=>[s.Input.Time,degrees(s.Input.Rider.SlideRadians),degrees(s.Input.Rider.SlideRadians)])],
    ['ψ / χ ° · nadwozie złote / prędkość turkusowa',samples.map(s=>[s.Input.Time,degrees(s.Input.Rider.Heading),degrees(Math.atan2(s.Input.Rider.Velocity.Y,s.Input.Rider.Velocity.X))])],
    ['Yaw rate rad/s',samples.map(s=>[s.Input.Time,s.Input.Rider.AngularVelocity,s.Input.Rider.AngularVelocity])],
    ['Prześwit m · kreda złota / banda turkusowa',samples.map(s=>[s.Input.Time,s.Inner,s.Outer])],
    ['Siły opon N · przód złoty / tył turkusowy',run.Telemetry.map(t=>[t.Time,Math.hypot(t.Forces.FrontForce.X,t.Forces.FrontForce.Y),Math.hypot(t.Forces.RearForce.X,t.Forces.RearForce.Y)])],
    ['Praca / bilans strat J na zakończony przedział',run.Telemetry.map(t=>[t.Time,t.Wheel.EngineWorkJoules,t.EnergyResidual])]
  ];
  for(const [title,points] of charts){
    const figure=el('figure');figure.append(el('figcaption',title));const svg=svgEl('svg',{viewBox:'0 0 600 150',role:'img','aria-label':title});
    const values=points.flatMap(p=>p.slice(1));let lo=Math.min(0,...values),hi=Math.max(1,...values);if(hi===lo)hi=lo+1;
    svg.append(svgEl('path',{d:'M40,15V125H590',stroke:'#48677a',fill:'none'}));
    const y=value=>125-(value-lo)/(hi-lo)*100;
    for(const [column,color] of [[1,'#ffd27b'],[2,'#73dfdf']])svg.append(svgEl('polyline',{points:points.map(p=>`${40+p[0]/duration*550},${y(p[column])}`).join(' '),fill:'none',stroke:color,'stroke-width':1.5}));
    const cursor=svgEl('line',{x1:40,x2:40,y1:12,y2:128,stroke:'#f4f7fa','stroke-dasharray':'3 3','data-robust-cursor':duration});svg.append(cursor);
    const label=svgEl('text',{x:45,y:145,fill:'#b9d2df','font-size':11});label.textContent=`0–${duration.toFixed(2)}s · zakres ${lo.toFixed(2)}…${hi.toFixed(2)}`;svg.append(label);
    figure.append(svg);element.append(figure);
  }
}
export function setupRobust(element,data,onRun,onOverlay){
  element.replaceChildren();element.append(el('h2',`${data.index.Title??'Robust Free Racing Learning'} · ${data.index.Grade}`));
  const controls=el('div');controls.className='audit-controls';
  const caseLabel=el('label','Wspólny stan '),caseSelect=el('select');caseSelect.id='robust-case';
  for(const group of data.index.Groups){const option=el('option',`${group.Id}: ${group.Reasons.join(', ')}`);option.value=group.Id;option.selected=group.Id===data.group.Id;caseSelect.append(option);}
  caseSelect.addEventListener('change',()=>{const group=data.index.Groups.find(g=>g.Id===caseSelect.value);onRun((group.Runs.find(r=>r.Policy==='131-final'||r.Policy==='130-final')??group.Runs[0]).Name,group.Id);});caseLabel.append(caseSelect);controls.append(caseLabel);
  const policyLabel=el('label','Polityka '),policySelect=el('select');policySelect.id='robust-policy';
  for(const r of data.group.Runs){const option=el('option',data.labels[r.Policy]??r.Policy);option.value=r.Name;option.selected=r.Name===data.document.Replay.Name;policySelect.append(option);}
  policySelect.addEventListener('change',()=>onRun(policySelect.value,data.group.Id));policyLabel.append(policySelect);controls.append(policyLabel);element.append(controls);
  const overlay=el('div');overlay.className='audit-controls';overlay.append(el('span','Pełne zapisane ślady: '));
  for(const line of data.comparisons){const label=el('label');label.style.color=line.color;const check=el('input');check.type='checkbox';check.checked=true;
    check.addEventListener('change',()=>onOverlay(line.policy,check.checked));label.append(check,document.createTextNode(' '+line.name));overlay.append(label);}element.append(overlay);
  element.append(el('p',`Ten sam stan ${data.group.Id}: ${data.group.Scenario.Speed}m/s, offset${data.group.Scenario.Offset}m, heading${data.group.Scenario.HeadingError}rad, v boczna${data.group.Scenario.OutwardSpeed}m/s, yaw${data.group.Scenario.YawRate}rad/s. Nakładki są całymi rzeczywistymi przejazdami, bez prognoz.`));
  element.append(el('p',data.group.Scope));
  element.append(el('p',`W Git są wybrane reprezentatywne replaye. Pełne surowe dane wszystkich prób pozostały lokalnie: ${data.index.RawDirectory}.`));
  const current=el('div');current.id='robust-telemetry';element.append(current);
  const details=el('details'),summary=el('summary','Przebiegi czasowe wybranego natywnego przejazdu');details.append(summary);const charts=el('div');charts.className='audit-charts';telemetryCharts(charts,data.document);details.append(charts);element.append(details);
  for(const [file,title] of data.index.Charts??[['ab-learning.svg','Uczenie A/B i utrzymywanie umiejętności'],['sealed-test.svg','Wszystkie12 nowych przypadków, cztery polityki'],['native-trajectories.svg','Wybrane nakładki na identycznych stanach']]){
    const d=el('details'),s=el('summary',title),img=el('img');img.src=(data.root??robustRoot)+file;img.alt=title;img.style.width='100%';d.append(s,img);element.append(d);
  }
  const report=el('a','Pełny raport, audyt granicy i koszt');report.href=(data.root??robustRoot)+'README.md';element.append(report);
}
export function updateRobustPanel(element,replay,time){
  const {run,sample:s,decision:d,metrics:m,telemetry:t}=robustReadout(replay,time),v=s.Input.Rider;
  element.replaceChildren();const lines=[`${replay.robustLabel??robustLabels[replay.robustPolicy]} · ${run.Name} · ${m.Native.Outcome} · bramka ${m.Native.Completed?m.Native.GateSeconds.toFixed(2)+'s':m.Native.Outcome==='probe'?'— (zaplanowana sonda)':'— (DNF)'}`,
    `Gaz żądany / aktywny ${(s.RequestedDrive*100).toFixed(1)} / ${(v.Drive*100).toFixed(1)}% · skręt żądany / wykonany ${(s.RequestedSteering*180/Math.PI).toFixed(2)} / ${(s.Input.Steering*180/Math.PI).toFixed(2)}°`,
    `β ${(v.SlideRadians*180/Math.PI).toFixed(2)}° · ψ ${(v.Heading*180/Math.PI).toFixed(2)}° · χ ${(Math.atan2(v.Velocity.Y,v.Velocity.X)*180/Math.PI).toFixed(2)}° · yaw ${v.AngularVelocity.toFixed(3)}rad/s`,
    `Obrys: kreda ${s.Inner.toFixed(4)}m / banda ${s.Outer.toFixed(4)}m · klasyfikacja końcowa: ${m.BoundaryClass} · pending ${s.Input.Pending.length}`];
  if(d)lines.push(d.Candidates.length===0?`Diagnostyczne utrzymanie komendy od ${d.Time.toFixed(2)}s; ten odcinek nie jest decyzją wyuczonej polityki.`:`Decyzja ${d.Time.toFixed(2)}s, reakcja0,24s, ${d.Candidates.length} działań: ${d.NoSafeContinuation?'BRAK bezpiecznej opcji w horyzoncie':`dostępna opcja bezpieczna tylko w horyzoncie ${replay.horizon??.6}s`}.`);
  if(t)lines.push(`Ostatni zakończony przedział ${(t.Time-t.Seconds).toFixed(3)}–${t.Time.toFixed(3)}s: Fprzód (${t.Forces.FrontForce.X.toFixed(1)}, ${t.Forces.FrontForce.Y.toFixed(1)})N, Ftył (${t.Forces.RearForce.X.toFixed(1)}, ${t.Forces.RearForce.Y.toFixed(1)})N; yaw moment przód/tył ${t.Forces.FrontTorque.toFixed(1)}/${t.Forces.RearTorque.toFixed(1)}Nm; praca silnika ${t.Wheel.EngineWorkJoules.toFixed(2)}J, bilans strat ${t.EnergyResidual.toFixed(2)}J; certyfikat ${t.AcceptedCertificate.toFixed(4)}m.`);
  for(const text of lines)element.append(el('p',text));
  for(const cursor of document.querySelectorAll('[data-robust-cursor]')){const x=40+time/Number(cursor.getAttribute('data-robust-cursor'))*550;cursor.setAttribute('x1',x);cursor.setAttribute('x2',x);}
}
