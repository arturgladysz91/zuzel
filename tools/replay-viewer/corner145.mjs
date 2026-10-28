// All curves and points come from accepted native states. No synthesized riding.
import {loadStanding144,setupStanding144} from './standing144.mjs';
import {readGzip} from './recordings.mjs';
export const cornerRoot='../../docs/experiments/corner-speed-loss/';
const palette=['#70e2e4','#ffb36d','#e5a5f5','#b5d878','#f4df72','#ee86ae','#9daaf6'];
export async function loadCorner145(fetcher=fetch,name='K1',root=cornerRoot){
  const data=await loadStanding144(fetcher,name,root);
  data.cornerCurves=JSON.parse(await readGzip(await fetcher(root+'comparison.json.gz')));
  data.cornerAnalysis=await (await fetcher(root+'saved-analysis.json')).json();
  data.scoreAudit=await (await fetcher(root+'native/score-audit.json')).json();
  return data;
}
export function cornerWindow(curves,lap=2,bend=2,whole=false){
  return curves.map((run,index)=>({...run,color:palette[index%palette.length],points:run.points.filter(p=>whole||(p.lap===lap&&p.bend===bend))}));
}
function svgChart(parent,series,xKey,yKey,label,onJump,step=false){
  const ns='http://www.w3.org/2000/svg',svg=document.createElementNS(ns,'svg');svg.setAttribute('viewBox','0 0 1000 170');svg.style.width='100%';svg.style.background='#101f2b';
  svg.setAttribute('role','img');svg.setAttribute('aria-label',label);const all=series.flatMap(s=>s.points);if(!all.length)return;
  const xmin=Math.min(...all.map(p=>p[xKey])),xmax=Math.max(...all.map(p=>p[xKey]));
  let ymin=Math.min(...all.map(p=>p[yKey])),ymax=Math.max(...all.map(p=>p[yKey]));if(ymin===ymax)ymax=ymin+1;
  const x=p=>55+(p[xKey]-xmin)/Math.max(.01,xmax-xmin)*925,y=p=>140-(p[yKey]-ymin)/(ymax-ymin)*105;
  const text=(a,b,t)=>{const el=document.createElementNS(ns,'text');el.setAttribute('x',a);el.setAttribute('y',b);el.setAttribute('fill','#d7e7ef');el.setAttribute('font-size','12');el.textContent=t;svg.append(el);};
  text(8,18,label);text(2,48,ymax.toFixed(2));text(2,140,ymin.toFixed(2));text(55,163,xmin.toFixed(2));text(920,163,xmax.toFixed(2));
  for(const run of series){
    const path=document.createElementNS(ns,'path');let d='';run.points.forEach((p,i)=>{d+=(i?(step?`H${x(p)}V${y(p)}`:`L${x(p)},${y(p)}`):`M${x(p)},${y(p)}`);});
    path.setAttribute('d',d);path.setAttribute('fill','none');path.setAttribute('stroke',run.color);path.setAttribute('stroke-width','1.5');path.dataset.run=run.name;svg.append(path);
  }
  // Jump only within the currently selected native run; other curves keep their own clocks.
  svg.addEventListener('click',event=>{const rect=svg.getBoundingClientRect();const target=xmin+((event.clientX-rect.left)/rect.width*1000-55)/925*(xmax-xmin);onJump(target,xKey);});
  parent.append(svg);
}
export function setupCorner145(element,data,onRun,onJump,onTrails){
  setupStanding144(element,data,onRun,onJump);
  element.querySelector('h2').textContent='#145 · Straty w łukach · '+data.run.Name+' · '+data.run.Outcome;
  const panel=document.createElement('section');element.append(panel);
  const controls=document.createElement('div');controls.className='audit-controls';panel.append(controls);
  const lap=document.createElement('select');lap.setAttribute('aria-label','Okrążenie wykresów');for(const n of [1,2,3]){const o=document.createElement('option');o.value=n;o.textContent='Okrążenie '+n;o.selected=n===2;lap.append(o);}
  const bend=document.createElement('select');bend.setAttribute('aria-label','Łuk wykresów');for(const [n,label] of [[1,'Łuk 1'],[2,'Łuk 2'],[0,'Całe trzy okrążenia']]){const o=document.createElement('option');o.value=n;o.textContent=label;o.selected=n===2;bend.append(o);}
  controls.append(lap,bend);const enabled=new Set(data.cornerCurves.map(q=>q.name));
  data.cornerCurves.forEach((q,index)=>{const label=document.createElement('label'),check=document.createElement('input');check.type='checkbox';check.checked=true;label.style.color=palette[index%palette.length];label.append(check,document.createTextNode(q.name));check.addEventListener('change',()=>{check.checked?enabled.add(q.name):enabled.delete(q.name);draw();});controls.append(label);});
  const charts=document.createElement('div');panel.append(charts);
  const note=document.createElement('p');note.textContent='Porównanie po wspólnej pozycji referencyjnej i po rzeczywistym czasie. Żądanie i aktywacja są osobnymi krzywymi. Kliknięcie wykresu przesuwa wybrany replay; sekwencje ręczne są tylko diagnostyką.';panel.append(note);
  function draw(){
    charts.replaceChildren();const series=cornerWindow(data.cornerCurves,Number(lap.value),Number(bend.value),bend.value==='0').filter(q=>enabled.has(q.name));
    const jump=(value,key)=>{if(key==='t')onJump(value);else{const current=series.find(s=>s.name===data.run.Name);if(current?.points.length)onJump(current.points.reduce((a,b)=>Math.abs(a.reference-value)<Math.abs(b.reference-value)?a:b).t);}};
    svgChart(charts,series,'t','speed','Prędkość km/h · czas s',jump);
    svgChart(charts,series,'reference','speed','Prędkość km/h · pozycja referencyjna m',jump);
    for(const [key,label] of [['gas','Gaz aktywny 0–1'],['requestedGas','Gaz żądany 0–1'],['steering','Kierownica wykonana °'],['requestedSteering','Kierownica żądana °'],['beta','Kąt β °'],['slip','Wheelspin / slip ratio']])svgChart(charts,series,'reference',key,label,jump,key.includes('Gas')||key==='gas');
    onTrails(series.map(s=>({points:s.points.map(p=>({x:p.x,y:p.y})),color:s.color,policy:s.name})));
  }
  lap.addEventListener('change',draw);bend.addEventListener('change',draw);draw();
  const hotspots=document.createElement('div');hotspots.className='audit-controls';panel.append(hotspots);
  for(const c of data.cornerAnalysis.corners.filter(c=>c.run===data.run.Name)){
    for(const [label,point] of [['V min',c.minimum],['Max opóźnienie',c.worst_deceleration]]){
      const b=document.createElement('button');b.textContent=`Okr.${c.lap} łuk${c.bend} · ${label} · ${point.time_s.toFixed(3)}s`;b.addEventListener('click',()=>onJump(point.time_s));hotspots.append(b);
    }
  }
  const audit=document.createElement('details'),summary=document.createElement('summary');summary.textContent='Dlaczego wybrano gaz 0? Pełna punktacja decyzji K1';audit.append(summary);panel.append(audit);
  for(const decision of data.scoreAudit.Audits.filter(q=>q.Run==='K1')){
    const details=document.createElement('details'),title=document.createElement('summary');title.textContent=`${decision.Time.toFixed(2)} s · wybrano gaz ${decision.Chosen.Drive.toFixed(3)}, skręt ${(decision.Chosen.SteeringRadians*180/Math.PI).toFixed(1)}°`;details.append(title);
    const table=document.createElement('table'),head=document.createElement('tr');for(const label of ['Skręt °','Gaz','Siatka','Safety','Score','Progress','Kierunek','Yaw','Zmiana skrętu','Clearance','Unsafe']){const th=document.createElement('th');th.textContent=label;head.append(th);}table.append(head);
    for(const row of decision.Leaves){const tr=document.createElement('tr');if(row.Selected)tr.style.background='#37554e';for(const value of [row.Action.SteeringRadians*180/Math.PI,row.Action.Drive,row.InOriginalGrid?'tak':'próba',row.Safety,row.Score,row.Terms.Progress,row.Terms.Direction,row.Terms.Yaw,row.Terms.Steering,row.Terms.Clearance,row.Terms.Unsafe]){const td=document.createElement('td');td.textContent=typeof value==='number'?value.toFixed(3):value;tr.append(td);}table.append(tr);}
    const wrap=document.createElement('div');wrap.style.overflowX='auto';wrap.append(table);details.append(wrap);audit.append(details);
  }
}
