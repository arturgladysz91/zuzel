// Read-only native replay. Predictions and recovery plans never become displayed poses.
import {loadRobust} from './robust-learning.mjs';
import {readGzip} from './recordings.mjs';
export const constraintsRoot='../../docs/experiments/racing-constraints-speed/';
export async function loadConstraints143(fetcher=fetch,name=null,root=constraintsRoot,group=null){
  const data=await loadRobust(fetcher,name,root,group);
  data.constraintsDecisions=JSON.parse(await readGzip(await fetcher(root+data.group.DecisionFile)));
  data.replay.episodeScope=data.group.Scope;
  data.replay.constraintsData=data;
  return data;
}
export function constraintsReadout(data,time){
  return data.comparisonRuns.map(r=>{
    const samples=r.run.Replay.Samples,last=samples.at(-1);
    const sample=samples.findLast(s=>s.Input.Time<=time+1e-9)??samples[0];
    return {...r,sample,ended:time>last.Input.Time+1e-9,
      summary:data.group.Summary[r.policy],
      decision:data.constraintsDecisions[r.policy].findLast(d=>d.Time<=time+1e-9)??null};
  });
}
export function constraintsPanel(element,data,time){
  if(!element)return;element.replaceChildren();
  const add=text=>{const p=document.createElement('p');p.textContent=text;element.append(p);};
  add('#143 · PARTIAL · pełna jazda autonomiczna. H09 / review 0,2s / reakcja 0,24s. Nakładki i punkty to wyłącznie natywne zapisy.');
  add(data.group.Scope);
  for(const r of constraintsReadout(data,time)){
    const {sample:s,summary:m,decision:d}=r;
    add(`${r.name}: pełny czas ${m.Completed?m.FullTimeSeconds.toFixed(3)+'s':'N/D — '+m.Outcome}; dawna bramka ${m.HistoricalGateSeconds?.toFixed(3)??'N/D'}s. ${r.ended?'KONIEC ZAPISU — pozycja zamrożona.':''}`);
    add(`Stan ${s.Input.Time.toFixed(2)}s: ${(s.Input.Rider.Speed*3.6).toFixed(2)}km/h; gaz żądany / aktywny ${(s.RequestedDrive*100).toFixed(1)} / ${(s.Input.Active.Drive*100).toFixed(1)}%; β ${(s.Input.Rider.SlideRadians*180/Math.PI).toFixed(2)}°; yaw ${s.Input.Rider.AngularVelocity.toFixed(3)}rad/s; chwilowy gap ${Math.min(s.Inner,s.Outer).toFixed(4)}m.`);
    if(d)add(`Wybór ${d.Time.toFixed(2)}s: ${d.CpuSeconds.toFixed(4)} CPU-s, ${d.Forecasts} prognoz. ${d.Reason} Łącznie ${m.ChangedDecisions} wyborów innych niż A na tych samych obserwacjach; to nie liczba porównań różnych trajektorii.`);
  }
  add('F usuwa geometryczną preferencję; B preferencję gazu; E karę zmiany skrętu; D karę yaw. Brak certyfikacji ≠ uderzenie w bandę. Szybszy nieukończony przejazd nie jest zwycięstwem.');
}
