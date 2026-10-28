// Saved native evidence and saved planner diagnostics only; never computes a control.
import {loadStanding144,setupStanding144} from './standing144.mjs';
import {readGzip} from './recordings.mjs';
export const planning146Root='../../docs/experiments/autonomous-corner-planning/';
export async function loadPlanning146(fetcher=fetch,name=null,root=planning146Root){
  const data=await loadStanding144(fetcher,name,root);
  [data.analysis146,data.curves146]=await Promise.all([fetcher(root+'analysis.json').then(r=>r.json()),fetcher(root+'comparison.json.gz').then(readGzip).then(JSON.parse)]);
  return data;
}
export function review146(data,time){
  return data.analysis146.plan_audits[data.run.Name]?.rows.findLast(r=>r.time_s<=time+1e-8)??null;
}
const number=(x,d=3)=>x==null?'N/D':Number(x).toFixed(d);
export function setupPlanning146(element,data,onRun,onJump,onTrails){
  setupStanding144(element,data,onRun,onJump);
  element.querySelector('h2').textContent='#146 · '+data.run.Name+' · '+data.run.Outcome;
  const p=document.createElement('p');p.textContent='Prognoza jest zapisem planu z chwili decyzji. Kolejne obserwacje mogą go zmienić; poniżej podano przyczynę każdej zmiany. Wymuszone walidacje sprawdzają model, nie autonomię.';element.append(p);
  const table=document.createElement('table'),head=document.createElement('tr');
  for(const t of ['Łuk','Czas s','Δ K1 s','V wej./min./wyj. km/h','β max °','Linia min./max. m']){const th=document.createElement('th');th.textContent=t;head.append(th);}table.append(head);
  for(const c of data.analysis146.corners.filter(c=>c.run===data.run.Name)){
    const tr=document.createElement('tr');for(const value of [`${c.lap}.${c.bend}`,number(c.seconds),number(c.delta_K1_s),[c.entry_kph,c.minimum_kph,c.exit_kph].map(v=>number(v,1)).join(' / '),number(c.beta_abs_max_deg,1),[c.offset_min_m,c.offset_max_m].map(v=>number(v,2)).join(' / ')]){const td=document.createElement('td');td.textContent=value;tr.append(td);}table.append(tr);
  }element.append(table);
  const audit=data.analysis146.plan_audits[data.run.Name];if(!audit)return;
  const stats=document.createElement('p');stats.textContent=`${audit.reviews} obserwacji · zachowano ${audit.followed_expected}/${audit.expected_reviews} kolejnych komend · kolejki zgodne: ${audit.queues_match} · max błąd XY ${number(audit.maximum_error.PositionMeters,6)}m · CPU p50/p95 ${number(audit.cpu_p50_s)}/${number(audit.cpu_p95_s)}s`;element.append(stats);
  const controls=document.createElement('div'),readout=document.createElement('pre');readout.style.whiteSpace='pre-wrap';controls.className='audit-controls';element.append(controls,readout);
  const show=row=>{
    onJump(row.time_s);
    readout.textContent=JSON.stringify({czas:row.time_s,powod:row.reason,sekwencja:row.selected,planowany_czas_celu:row.planned_goal_time_s,planowany_wyjazd:row.planned_exit_time_s,planowana_predkosc_wyjscia_kmh:row.planned_exit_kph,prognoza_legalna:row.forecast_legal,wykonana_komenda:row.actual_action,aktywacja:row.actual_activation,nastepna_zmiana:row.next_change_reason,blad_modelu:row.prediction_error},null,2);
    const palette=['#70e2e4','#ffb36d','#e5a5f5'];
    const lines=data.curves146.filter(c=>['K1','F',data.run.Name].includes(c.name)).map((c,k)=>({policy:c.name,color:palette[k%palette.length],points:c.points.map(p=>({x:p.x,y:p.y}))}));
    if(row.predicted_trace)lines.push({policy:'prognoza '+row.time_s,color:'#f4df72',points:row.predicted_trace.map(p=>({x:p[1],y:p[2]}))});onTrails(lines);
  };
  for(const row of audit.rows.filter((r,k)=>k===0||r.reason!=='retained legal suffix')){
    const b=document.createElement('button');b.textContent=`${number(row.time_s,2)}s · ${row.reason}`;b.addEventListener('click',()=>show(row));controls.append(b);
  }
}
