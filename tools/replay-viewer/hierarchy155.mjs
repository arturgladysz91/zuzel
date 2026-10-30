export const colors155={A:'#a8c2ff',B:'#f5b05c',C:'#4fe1b2'};
export function validate155(x){
 if(x.Study!==155||x.Verdict!=='PARTIAL'||x.NativeReference?.Study!==154||x.NativeReference.Kind!=='native-reference')throw Error('Study155 must identify its partial result and historical native reference');
 for(const c of x.Cases)for(const s of c.Series){
  if(s.Kind!=='forecast'||s.FullPolicy!==s.Passed||!s.Points?.length)throw Error('Forecast provenance or completeness missing');
  if(s.Passed&&(s.Nodes!==13||s.Leaves!==9))throw Error('Incomplete policy');
  if(s.Points[0].t<c.Time-1e-8||s.Points.some((p,i)=>!Number.isFinite(p.x+p.y+p.t)||i&&p.t<=s.Points[i-1].t))throw Error('Invalid physical sample times');
 }
 return x;
}
// Return a recorded sample only, and never extend a prefix beyond its last time.
export function sample155(series,absoluteTime){const a=series.Points;if(!a?.length||absoluteTime<a[0].t-1e-8||absoluteTime>a.at(-1).t+1e-8)return null;let lo=0,hi=a.length-1;while(lo<hi){const mid=Math.ceil((lo+hi)/2);if(a[mid].t<=absoluteTime+1e-8)lo=mid;else hi=mid-1;}return a[lo];}
export async function load155(fetcher=fetch){const r=await fetcher('../../docs/experiments/hierarchical-maneuvers/replay.json.gz');if(!r.ok)throw Error(`Replay HTTP ${r.status}`);const stream=r.body.pipeThrough(new DecompressionStream('gzip'));return validate155(await new Response(stream).json());}
if(typeof window!=='undefined'){
 const el=id=>document.getElementById(id);let data,geometry,selected;const canvas=el('track'),ctx=canvas.getContext('2d');
 const f=(n,d=2)=>n==null?'—':n.toFixed(d);
 function draw(){if(!selected)return;const rect=canvas.getBoundingClientRect(),dpr=devicePixelRatio||1;canvas.width=rect.width*dpr;canvas.height=rect.height*dpr;ctx.scale(dpr,dpr);ctx.fillStyle='#111c29';ctx.fillRect(0,0,rect.width,rect.height);
  const sections=geometry.segments.flatMap(s=>s.sections),all=sections.flatMap(s=>[s.inner,s.outer]);const minX=Math.min(...all.map(p=>p.x)),maxX=Math.max(...all.map(p=>p.x)),minY=Math.min(...all.map(p=>p.y)),maxY=Math.max(...all.map(p=>p.y));const scale=Math.min((rect.width-64)/(maxX-minX),(rect.height-64)/(maxY-minY));const xy=p=>[rect.width/2+(p.x-(minX+maxX)/2)*scale,rect.height/2-(p.y-(minY+maxY)/2)*scale];
  function line(points,color,width=1,dash=[]){ctx.beginPath();points.forEach((p,i)=>{const q=xy(p);i?ctx.lineTo(...q):ctx.moveTo(...q);});ctx.strokeStyle=color;ctx.lineWidth=width;ctx.setLineDash(dash);ctx.stroke();ctx.setLineDash([]);}
  ctx.beginPath();[...sections.map(s=>s.outer),...sections.toReversed().map(s=>s.inner)].forEach((p,i)=>{const q=xy(p);i?ctx.lineTo(...q):ctx.moveTo(...q);});ctx.closePath();ctx.fillStyle='#283443';ctx.fill();line(sections.map(s=>s.inner),'#738195',1.5);line(sections.map(s=>s.outer),'#738195',1.5);
  const t=selected.Time+Number(el('time').value),show=el('native').checked;
  if(show)line(data.NativeReference.Points,'#c8d0dc66',2,[3,5]);
  el('time-label').textContent=`+${f(t-selected.Time)} s · zegar ${f(t)} s`;
  for(const s of selected.Series){line(s.Points,colors155[s.Mode],2,s.Passed?[]:[6,5]);const p=sample155(s,t),status=el('state-'+s.Mode);if(!p){status.textContent='Koniec zapisanej prognozy';continue;}const q=xy(p);ctx.save();ctx.translate(...q);ctx.rotate(-p.heading);ctx.fillStyle=colors155[s.Mode];ctx.fillRect(-1.05*scale,-.15*scale,2.1*scale,.3*scale);ctx.fillRect(.63*scale,-.4*scale,.04*scale,.8*scale);ctx.restore();ctx.beginPath();ctx.arc(...q,4,0,2*Math.PI);ctx.strokeStyle=colors155[s.Mode];ctx.lineWidth=2;ctx.stroke();status.textContent=`${f(p.speed*3.6,1)} km/h · β ${f(p.beta*180/Math.PI,1)}° · gaz ${f(p.gas)} · obrys ${f(Math.min(p.inner,p.outer),3)} m`;}
 }
 function choose(){selected=data.Cases.find(x=>String(x.Time)===el('case').value);el('time').max=Math.max(...selected.Series.map(s=>s.Points.at(-1).t-selected.Time));el('time').value=0;for(const s of selected.Series){el('result-'+s.Mode).textContent=s.Passed?'13 węzłów / 9 rezerw · niezależny':'ABSTAIN · tylko legalny prefiks';el('cost-'+s.Mode).textContent=`${f(s.Cpu,3)} CPU-s · ${s.Passed?'cel w '+f(s.Arrival-selected.Time)+' s':'brak pełnej kontynuacji'}`;}draw();}
 Promise.all([load155(),fetch('../../docs/experiments/causal-yaw-throttle/progressive-b-viewer-geometry.json').then(r=>r.json())]).then(([x,g])=>{data=x;geometry=g;el('case').replaceChildren(...x.Cases.map(c=>new Option(`${f(c.Time,1)} s`,String(c.Time))));choose();el('load').textContent='Dane zweryfikowane · prognozy A/B/C + oryginalny zapis natywny #154';}).catch(e=>{el('load').textContent=e.message;el('load').classList.add('error');});el('case').addEventListener('change',choose);el('time').addEventListener('input',draw);el('native').addEventListener('change',draw);window.addEventListener('resize',draw);
}
