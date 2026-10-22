// Read-only native telemetry panel in the existing replay; forecasts remain separate evidence.
import { readGzip } from './recordings.mjs';
export const zigzagFile='../../docs/experiments/first-bend-zigzag/native-telemetry.json.gz';
export async function loadZigzag(fetcher=fetch) {
  const response=await fetcher(zigzagFile);
  if(!response.ok)throw new Error('Brak natywnej telemetrii audytu #127.');
  const data=JSON.parse(await readGzip(response));
  if(data.BetaConvention!=='psi-chi'||!data.Series?.baseline?.length||!data.Series?.candidate?.length)throw new Error('Niekompletny zapis audytu #127.');
  return data;
}
export function nativeAt(rows,time) {
  let a=0,b=rows.length-1;
  while(a<b){const m=Math.ceil((a+b)/2);if(rows[m].t<=time+1e-10)a=m;else b=m-1;}
  return rows[a];
}
const specs=[['Kierownica [°]',['steering','request']],['Orientacja ψ / kierunek χ [°]',['psi','chi']],['Uślizg β = ψ − χ [°]',['beta']],['Yaw rate [rad/s]',['yaw']],['Błąd normalny względem planu [m]',['error']],['Prędkość [km/h]',['speed']],['η = χ − styczna planu [°]',['eta']],['Rzeczywiście aktywny Drive [%]',['drive']]];
const fmt=(v,d=2)=>Number(v).toFixed(d),colors={baseline:'#82bfff',candidate:'#ffb36d'};
export function createZigzagPanel(element,data,{selected='candidate',windowId='main',onSeek,onCompare,onWindow}) {
  element.hidden=false;
  element.innerHTML=`<div class="audit-heading"><div><h2>Audyt zygzaka #127 · rzeczywiste stany Box2D</h2><p>Niebieski: baseline #124. Pomarańczowy: kandydat #126. Osobne przejazdy R1, wspólny zegar.</p></div><button id="zigzag-compare">Porównaj w tej samej sekundzie</button></div>
    <div class="audit-controls"><label>Przedział <select id="zigzag-window">${data.Windows.map(w=>`<option value="${w.Id}">${w.Label}</option>`).join('')}</select></label><output id="zigzag-clock"></output><span>Kliknij wykres lub znacznik, aby przewinąć replay. ←/→: natywne próbki.</span></div>
    <p class="audit-key">Kierownica: ciągła = stan, przerywana = żądanie. W ψ/χ: ciągła ψ, przerywana χ. Czerwone pasy: override 100 ms; pionowe kreski: podjęcie decyzji. Wykres łączy zapisane pomiary; nie wygładza motocykla.</p>
    <div id="zigzag-markers" class="audit-markers"></div><div id="zigzag-charts" class="audit-charts"></div>
    <p class="audit-causality">Duży cykl odchylenia: 3,90–9,20 s. Szczyty η: +34,18° w 5,63 s i −16,32° w 7,15 s. Ujemny yaw: 5,51–6,78 s; χ maleje w 5,70–6,82 s. η jest błędem stycznej, nie wyznaczonym optymalnym kierunkiem odzyskiwania linii.</p>
    <details><summary>Siły, przyczepność, energia i bieżąca próbka</summary><div id="zigzag-values" class="audit-values"></div></details>
    <p class="audit-limit">PARTIAL · bez nowego regulatora i bez nowej jazdy. Bias C i prognozy 0,6/0,9 s są wyłącznie w raporcie offline; nie zastępują pozycji motocykla.</p>`;
  const select=element.querySelector('#zigzag-window');select.value=windowId;
  let time=0,active=selected,lastSample=null;
  function update(t,force=false) {
    time=t;const window=data.Windows.find(w=>w.Id===select.value),u=(t-window.Start)/(window.End-window.Start);
    for(const cursor of element.querySelectorAll('.audit-cursor')){cursor.setAttribute('x1',52+u*656);cursor.setAttribute('x2',52+u*656);cursor.style.visibility=u>=0&&u<=1?'visible':'hidden';}
    const row=nativeAt(data.Series[active],t);element.querySelector('#zigzag-clock').textContent=`Replay ${fmt(t,3)} s · ${active==='candidate'?'#126':'#124'} · próbka ${fmt(row.t,3)} s`;
    if(!force&&lastSample===row)return;lastSample=row;
    const values=[['Próbka',`${fmt(row.t,3)} s`],['XY',`${fmt(row.x,3)}, ${fmt(row.y,3)} m`],['ψ / χ / β',`${fmt(row.psi)} / ${fmt(row.chi)} / ${fmt(row.beta)}°`],['Yaw / przyspieszenie yaw',`${fmt(row.yaw,3)} rad/s / ${fmt(row.yaw_acceleration,3)} rad/s²`],['Kierownica / żądanie',`${fmt(row.steering)} / ${fmt(row.request)}°`],['Błąd / η',`${fmt(row.error,3)} m / ${fmt(row.eta)}°`],['Prędkość / aktywny Drive',`${fmt(row.speed)} km/h / ${fmt(row.drive)}%`],['Siła boczna przód / tył',`${fmt(row.front_force,1)} / ${fmt(row.rear_force,1)} N`],['Budżet przód / tył',`${fmt(row.front_capacity,1)} / ${fmt(row.rear_capacity,1)} N`],['Wykorzystanie przód / tył łącznie',`${fmt(row.front_use*100,1)} / ${fmt(row.rear_use*100,1)}%`],['Straty opon / moc na kole',`${fmt(row.tyre_loss/1000,3)} / ${fmt(row.engine_power/1000,3)} kW`]];
    element.querySelector('#zigzag-values').innerHTML=values.map(([label,value])=>`<div><span>${label}</span><strong>${value}</strong></div>`).join('');
  }
  function render() {
    const window=data.Windows.find(w=>w.Id===select.value),start=window.Start,end=window.End;
    const x=t=>52+(t-start)/(end-start)*656,charts=element.querySelector('#zigzag-charts');charts.replaceChildren();
    const pulses=data.Activations.filter(a=>a.ActualActivation>=start-.1&&a.ActualActivation<=end).map(a=>`<rect x="${Math.max(52,x(a.ActualActivation))}" y="22" width="${Math.min(708,x(a.ActualActivation+.1))-Math.max(52,x(a.ActualActivation))}" height="112" fill="#fb8d87" opacity=".13"/>`).join('');
    const observed=data.Activations.filter(a=>a.ObservedAt>=start&&a.ObservedAt<=end).map(a=>`<line x1="${x(a.ObservedAt)}" x2="${x(a.ObservedAt)}" y1="22" y2="134" stroke="#b78b7c" stroke-dasharray="2 4" opacity=".6"/>`).join('');
    for(const [label,keys] of specs){
      const series=Object.entries(data.Series).map(([name,rows])=>[name,rows.filter(r=>r.t>=start-1e-8&&r.t<=end+1e-8)]);
      const values=series.flatMap(([,rows])=>rows.flatMap(r=>keys.map(k=>r[k])));let lo=Math.min(...values),hi=Math.max(...values);const pad=Math.max(.05,(hi-lo)*.08);lo-=pad;hi+=pad;
      const y=v=>134-(v-lo)/(hi-lo)*112,figure=document.createElement('figure');
      figure.innerHTML=`<svg viewBox="0 0 720 156" role="img" aria-label="${label}. Kliknij czas wykresu."><title>${label}</title><rect x="52" y="22" width="656" height="112" fill="#101d29"/>${pulses}${observed}<text x="52" y="14" fill="#d2e2ec" font-size="12">${label}</text>
        ${[0,.5,1].map(u=>{const v=lo+(hi-lo)*u;return `<line x1="52" x2="708" y1="${y(v)}" y2="${y(v)}" stroke="#284052"/><text x="47" y="${y(v)+4}" text-anchor="end" fill="#9eb3c5" font-size="10">${fmt(v,1)}</text>`;}).join('')}
        ${[0,.25,.5,.75,1].map(u=>`<text x="${x(start+(end-start)*u)}" y="151" text-anchor="middle" fill="#9eb3c5" font-size="10">${fmt(start+(end-start)*u)} s</text>`).join('')}
        ${series.map(([name,rows])=>keys.map((key,i)=>`<polyline fill="none" stroke="${colors[name]}" stroke-width="${name===active?1.9:1.2}" opacity="${name===active?1:.65}" ${i?'stroke-dasharray="4 3"':''} points="${rows.map(r=>`${x(r.t).toFixed(2)},${y(r[key]).toFixed(2)}`).join(' ')}"/>`).join('')).join('')}
        <line class="audit-cursor" y1="22" y2="134" stroke="#ecf7ff"/></svg>`;
      const svg=figure.querySelector('svg');svg.addEventListener('pointerdown',event=>{const r=svg.getBoundingClientRect(),u=Math.max(0,Math.min(1,((event.clientX-r.left)/r.width*720-52)/656));onSeek(Number((start+u*(end-start)).toFixed(3)));});charts.append(figure);
    }
    const markers=element.querySelector('#zigzag-markers');markers.replaceChildren();
    const entries=[...data.Activations.flatMap(a=>[{Time:a.ObservedAt,Label:'Decyzja −0,9°'},{Time:a.ActualActivation,Label:'Aktywacja −0,9°'},{Time:a.ActualActivation+.1,Label:'Powrót #117'}]),...data.Markers].filter(a=>a.Time>=start-1e-8&&a.Time<=end+1e-8).sort((a,b)=>a.Time-b.Time);
    for(const entry of entries){const button=document.createElement('button');button.textContent=`${fmt(entry.Time)} · ${entry.Label}`;button.addEventListener('click',()=>onSeek(entry.Time));markers.append(button);}
    update(time,true);
  }
  select.addEventListener('change',()=>{onWindow?.(select.value);render();onSeek(data.Windows.find(w=>w.Id===select.value).Start);});
  element.querySelector('#zigzag-compare').addEventListener('click',()=>onCompare(time));
  render();return {update};
}
